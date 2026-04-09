// =========================
// RoadNetworkPlanner.cs (FULL)
// + 2-PASS: collect surface centerlines -> connect/snap/intersect -> stamp+build
// + Rounded junction patches + stop lines + dashed markings
// =========================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antimatter
{
    
    public static class RoadNetworkPlanner
    {
        [Serializable]
        public class RoadNetworkResult
        {
            public List<Vector3> hubsWorld = new List<Vector3>();
    
            public List<List<Vector3>> surfaceRoadCenterlines = new List<List<Vector3>>();
            public List<List<Vector3>> bridgeDeckCenterlines = new List<List<Vector3>>();
            public List<List<Vector3>> tunnelCenterlines = new List<List<Vector3>>();
    
            public void Clear()
            {
                hubsWorld.Clear();
                surfaceRoadCenterlines.Clear();
                bridgeDeckCenterlines.Clear();
                tunnelCenterlines.Clear();
            }
        }
    
        public static RoadNetworkResult LastResult { get; private set; } = new RoadNetworkResult();
    
        private struct Hub
        {
            public Vector3 world;
            public Vector2Int grid;
        }
    
        private struct Edge
        {
            public int a;
            public int b;
            public float w;
        }
    
        public static void GenerateRoadSystem(
            Scene scene,
            Terrain terrain,
            RoadGenSettings s,
            List<Vector3> explicitHubs = null,
            Material roadMaterial = null,
            bool alsoCreateTunnelRoadbed = false,
            Material roadMarkingMaterial = null // NEW: pass a white/yellow line material if you want markings
        )
        {
            if (terrain == null || terrain.terrainData == null)
                throw new Exception("No valid Terrain provided.");
    
            if (LastResult == null) LastResult = new RoadNetworkResult();
            LastResult.Clear();
    
            var grid = new TerrainCostGrid(terrain, s.gridResolution);
            grid.Build(s);
    
            var hubs = GenerateHubs(grid, s, explicitHubs);
            if (hubs.Count < 2)
                throw new Exception("Unable to place at least 2 hubs (try lowering hubMinSpacingWorld or hubCount).");
    
            var edges = BuildNetworkEdgesMstPlusExtras(grid, hubs, s);
    
            LastResult.hubsWorld.AddRange(hubs.Select(h => h.world));
    
            var roadsParent = EnsureGroup(scene, "AI_RoadNetwork");
            var bridgesParent = EnsureGroup(scene, "AI_BridgeNetwork");
            var tunnelsParent = EnsureGroup(scene, "AI_TunnelNetwork");
            var hubsParent = EnsureGroup(scene, "AI_RoadHubs");
            var junctionsParent = EnsureGroup(scene, "AI_RoadJunctions");
    
            ClearChildrenImmediate(roadsParent.transform);
            ClearChildrenImmediate(bridgesParent.transform);
            ClearChildrenImmediate(tunnelsParent.transform);
            ClearChildrenImmediate(hubsParent.transform);
            ClearChildrenImmediate(junctionsParent.transform);
    
            // PASS 1: collect surface polylines
            var pendingSurface = new List<List<Vector3>>();
    
            int roadIndex = 0;
    
            foreach (var e in edges)
            {
                var start = hubs[e.a].grid;
                var goal = hubs[e.b].grid;
    
                bool ok = AStarRouter.FindPath(grid, s, start, goal, allowTunnel: false, out var gridPath);
                if (!ok) ok = AStarRouter.FindPath(grid, s, start, goal, allowTunnel: true, out gridPath);
    
                if (!ok || gridPath == null || gridPath.Count < 2)
                    continue;
    
                // Grid -> world polyline
                List<Vector3> worldPath = new List<Vector3>(gridPath.Count);
                for (int i = 0; i < gridPath.Count; i++)
                {
                    Vector3 w = grid.GridToWorld(gridPath[i].x, gridPath[i].y, 0f);
                    worldPath.Add(w);
                }
    
                worldPath = PolylineUtils.SimplifyXZ(worldPath, s.simplifyToleranceWorld);
                worldPath = PolylineUtils.Chaikin(worldPath, s.chaikinIterations);
    
                PolylineUtils.ApplyGradeProfileToTerrain(grid, worldPath, s.roadYOffset, s.maxRoadGrade);
    
                SplitByTunnelCover(grid, worldPath, s, out var surfaceSegments, out var tunnelSegments);
    
                int segId = 0;
                int bridgeId = 0;
    
                foreach (var seg in surfaceSegments)
                {
                    if (seg == null || seg.Count < 2) continue;
    
                    SplitByBridgeClearance(grid, seg, s, out var normalSegs, out var bridgeSegs);
    
                    // Collect normal surface road centerlines (build later)
                    foreach (var sseg in normalSegs)
                    {
                        if (sseg == null || sseg.Count < 2) continue;
    
                        var poly = ClonePolyline(sseg);
                        LastResult.surfaceRoadCenterlines.Add(ClonePolyline(poly));
                        pendingSurface.Add(poly);
    
                        segId++;
                    }
    
                    // Bridges: build immediately (separate system)
                    foreach (var bseg in bridgeSegs)
                    {
                        if (bseg == null || bseg.Count < 2) continue;
    
                        string bName = $"Bridge_{roadIndex:000}_{bridgeId:00}";
    
                        var deck = new List<Vector3>(bseg.Count);
                        for (int i = 0; i < bseg.Count; i++)
                        {
                            Vector3 p = bseg[i];
                            deck.Add(new Vector3(p.x, p.y + Mathf.Max(0f, s.bridgeDeckClearance), p.z));
                        }
    
                        LastResult.bridgeDeckCenterlines.Add(ClonePolyline(deck));
    
                        var bridge = BridgeBuilder.BuildBridge(
                            scene,
                            terrain,
                            bName,
                            deckPath: deck,
                            halfWidth: s.roadHalfWidth,
                            deckThickness: s.bridgeDeckThickness,
                            pillarSpacing: s.bridgePillarSpacing,
                            pillarWidth: s.bridgePillarWidth,
                            deckMaterial: roadMaterial,
                            pillarMaterial: null,
                            markingMaterial: null,
                            railHeight: s.bridgeRailHeight,
                            railThickness: s.bridgeRailThickness,
                            markingWidth: s.bridgeMarkingWidth,
                            markingYOffset: s.bridgeMarkingYOffset,
                            dashLength: s.bridgeDashLength,
                            dashGap: s.bridgeDashGap,
                            enableTruss: s.bridgeEnableTruss,
                            trussHeight: s.bridgeTrussHeight,
                            trussThickness: s.bridgeTrussThickness,
                            trussPostSpacing: s.bridgeTrussPostSpacing
                        );
    
                        if (bridge != null)
                            bridge.transform.SetParent(bridgesParent.transform, true);
    
                        bridgeId++;
                    }
                }
    
                // Tunnels: build immediately (separate system)
                int tunId = 0;
                foreach (var seg in tunnelSegments)
                {
                    if (seg == null || seg.Count < 2) continue;
    
                    LastResult.tunnelCenterlines.Add(ClonePolyline(seg));
    
                    string tName = $"Tunnel_{roadIndex:000}_{tunId:00}";
                    var tunnel = TunnelBuilder.BuildTunnelWithRoad(
                        scene,
                        tName,
                        seg,
                        s.tunnelInnerRadius,
                        s.tunnelWallThickness,
                        s.tunnelPortalLength,
                        s.roadHalfWidth,
                        roadMaterial,
                        addRoadCollider: true,
                        terrainForClamping: terrain);
    
                    if (tunnel != null)
                        tunnel.transform.SetParent(tunnelsParent.transform, true);
    
                    if (alsoCreateTunnelRoadbed)
                    {
                        string trName = $"TunnelRoad_{roadIndex:000}_{tunId:00}";
                        var tunnelRoad = RoadMeshBuilder.BuildOrUpdateRoad(
                            scene,
                            terrain: null,
                            name: trName,
                            path: seg,
                            halfWidth: s.roadHalfWidth,
                            yOffset: 0f,
                            mat: roadMaterial,
                            mode: RoadMeshBuilder.RoadConformMode.TunnelNoProject,
                            addCollider: true);
    
                        if (tunnelRoad != null)
                            tunnelRoad.transform.SetParent(tunnelsParent.transform, true);
                    }
    
                    tunId++;
                }
    
                roadIndex++;
            }
    
            // CONNECT PASS: snap + intersections on centerlines
            var connect = new RoadCenterlineConnector.Settings
            {
                snapRadius = Mathf.Max(2f, s.roadHalfWidth * 1.25f),
                mergeEps = 0.25f,
                intersectionEps = Mathf.Max(0.75f, s.roadHalfWidth * 0.2f),
                flattenY = true
            };
    
            RoadCenterlineConnector.ConnectAll(pendingSurface, connect, out var intersectionPoints);
    
            // PASS 2: stamp + build meshes from connected centerlines
            for (int i = 0; i < pendingSurface.Count; i++)
            {
                var line = pendingSurface[i];
                if (line == null || line.Count < 2) continue;
    
                TerrainStamper.StampPath(
                    terrain,
                    line,
                    s.roadHalfWidth,
                    s.stampFalloff,
                    s.stampBlend,
                    s.stampCutDepth,
                    s.roadYOffset);
    
                string rName = $"Road_{i:000}";
                var road = RoadMeshBuilder.BuildOrUpdateRoad(
                    scene,
                    terrain,
                    rName,
                    line,
                    s.roadHalfWidth,
                    s.roadYOffset,
                    roadMaterial,
                    RoadMeshBuilder.RoadConformMode.SurfaceProjectToTerrain,
                    addCollider: true);
    
                if (road != null)
                    road.transform.SetParent(roadsParent.transform, true);
            }
    
            // Rounded junction patches + markings (stop lines + dashes)
            if (intersectionPoints != null && intersectionPoints.Count > 0)
            {
                IntersectionPatchBuilder.BuildPatchesFromCenterlines(
                    scene,
                    terrain,
                    parentName: "AI_RoadJunctions",
                    intersectionsWorld: intersectionPoints,
                    surfaceCenterlines: pendingSurface,
                    roadHalfWidth: s.roadHalfWidth,
                    yOffset: s.roadYOffset + 0.03f,
                    patchMaterial: roadMaterial,
                    markingMaterial: roadMarkingMaterial,
                    addCollider: false
                );
            }
    
            CreateHubMarkers(scene, hubs.Select(h => h.world).ToList(), hubsParent.transform);
        }
    
        // ---------------------------------------------------------------------
        // HUBS
        // ---------------------------------------------------------------------
        private static List<Hub> GenerateHubs(TerrainCostGrid grid, RoadGenSettings s, List<Vector3> explicitHubs = null)
        {
            var hubs = new List<Hub>();
    
            // If AI gives explicit hubs, use them directly
            if (explicitHubs != null && explicitHubs.Count >= 2)
            {
                foreach (var worldPos in explicitHubs)
                {
                    var snapped = new Vector3(worldPos.x, grid.HeightAtWorld(worldPos), worldPos.z);
                    var g = grid.WorldToGrid(snapped);
                    hubs.Add(new Hub { world = snapped, grid = new Vector2Int(g.x, g.y) });
                }
                return hubs;
            }
    
            var rng = new System.Random(s.seed);
            hubs.Capacity = s.hubCount;
    
            int attempts = Mathf.Max(500, s.hubCount * 250);
    
            for (int i = 0; i < attempts && hubs.Count < s.hubCount; i++)
            {
                int gx = rng.Next(0, grid.N);
                int gy = rng.Next(0, grid.N);
    
                Vector3 w = grid.GridToWorld(gx, gy, 0f);
    
                float slope = grid.slopeDeg[gx, gy];
                if (slope > s.maxSurfaceSlopeDeg * 1.15f) continue;
    
                bool tooClose = false;
                for (int k = 0; k < hubs.Count; k++)
                {
                    float d = Vector2.Distance(new Vector2(w.x, w.z), new Vector2(hubs[k].world.x, hubs[k].world.z));
                    if (d < s.hubMinSpacingWorld) { tooClose = true; break; }
                }
                if (tooClose) continue;
    
                float h = grid.height01[gx, gy];
                float acceptProb = Mathf.Lerp(0.9f, 0.2f, h);
                if ((float)rng.NextDouble() > acceptProb) continue;
    
                hubs.Add(new Hub { world = w, grid = new Vector2Int(gx, gy) });
            }
    
            if (hubs.Count < 2)
            {
                hubs.Clear();
                hubs.Add(new Hub { grid = new Vector2Int(grid.N / 4, grid.N / 4), world = grid.GridToWorld(grid.N / 4, grid.N / 4, 0f) });
                hubs.Add(new Hub { grid = new Vector2Int(3 * grid.N / 4, 3 * grid.N / 4), world = grid.GridToWorld(3 * grid.N / 4, 3 * grid.N / 4, 0f) });
            }
    
            return hubs;
        }
    
        // ---------------------------------------------------------------------
        // MST + EXTRAS
        // ---------------------------------------------------------------------
        private static List<Edge> BuildNetworkEdgesMstPlusExtras(TerrainCostGrid grid, List<Hub> hubs, RoadGenSettings s)
        {
            int n = hubs.Count;
    
            var all = new List<Edge>(n * (n - 1) / 2);
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                float dx = hubs[i].grid.x - hubs[j].grid.x;
                float dy = hubs[i].grid.y - hubs[j].grid.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
    
                float c = (grid.cost[hubs[i].grid.x, hubs[i].grid.y] + grid.cost[hubs[j].grid.x, hubs[j].grid.y]) * 0.5f;
                float w = dist * c;
    
                all.Add(new Edge { a = i, b = j, w = w });
            }
    
            all.Sort((x, y) => x.w.CompareTo(y.w));
    
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
    
            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }
    
            void Union(int a, int b)
            {
                a = Find(a);
                b = Find(b);
                if (a != b) parent[b] = a;
            }
    
            var chosen = new List<Edge>(n - 1 + s.extraConnections);
    
            foreach (var ed in all)
            {
                int ra = Find(ed.a);
                int rb = Find(ed.b);
                if (ra == rb) continue;
                chosen.Add(ed);
                Union(ra, rb);
                if (chosen.Count == n - 1) break;
            }
    
            var rng = new System.Random(s.seed ^ 0xBADC0DE);
            int added = 0;
            int guard = 0;
    
            while (added < s.extraConnections && guard++ < 4000)
            {
                int idx = rng.Next(0, Mathf.Max(1, all.Count / 2));
                var ed = all[idx];
    
                bool exists = chosen.Any(c => (c.a == ed.a && c.b == ed.b) || (c.a == ed.b && c.b == ed.a));
                if (exists) continue;
    
                float dx = hubs[ed.a].grid.x - hubs[ed.b].grid.x;
                float dy = hubs[ed.a].grid.y - hubs[ed.b].grid.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > grid.N * 0.55f) continue;
    
                chosen.Add(ed);
                added++;
            }
    
            return chosen;
        }
    
        // ---------------------------------------------------------------------
        // SPLIT: TUNNEL vs SURFACE
        // ---------------------------------------------------------------------
        private static void SplitByTunnelCover(
            TerrainCostGrid grid,
            List<Vector3> gradeLinePath,
            RoadGenSettings s,
            out List<List<Vector3>> surfaceSegments,
            out List<List<Vector3>> tunnelSegments)
        {
            var surfaces = new List<List<Vector3>>();
            var tunnels = new List<List<Vector3>>();
    
            bool InTunnel(Vector3 p)
            {
                float terrainY = grid.HeightAtWorld(p);
                float cover = terrainY - p.y;
                return cover >= s.tunnelTriggerCoverMeters;
            }
    
            if (gradeLinePath == null || gradeLinePath.Count < 2)
            {
                surfaceSegments = surfaces;
                tunnelSegments = tunnels;
                return;
            }
    
            List<Vector3> cur = new List<Vector3>();
            bool curTunnel = InTunnel(gradeLinePath[0]);
            cur.Add(gradeLinePath[0]);
    
            for (int i = 1; i < gradeLinePath.Count; i++)
            {
                bool t = InTunnel(gradeLinePath[i]);
    
                if (t != curTunnel)
                {
                    cur.Add(gradeLinePath[i]);
                    AddSegment(cur, curTunnel);
    
                    cur = new List<Vector3> { gradeLinePath[i] };
                    curTunnel = t;
                }
                else cur.Add(gradeLinePath[i]);
            }
    
            AddSegment(cur, curTunnel);
    
            surfaceSegments = surfaces;
            tunnelSegments = tunnels;
    
            void AddSegment(List<Vector3> seg, bool isTunnel)
            {
                if (seg == null || seg.Count < 2) return;
    
                if (isTunnel)
                {
                    for (int i = 0; i < seg.Count; i++)
                        seg[i] = new Vector3(seg[i].x, seg[i].y - 0.2f, seg[i].z);
    
                    tunnels.Add(seg);
                }
                else
                {
                    // FIX for your TerrainStamper: no + roadYOffset here
                    for (int i = 0; i < seg.Count; i++)
                    {
                        float y = grid.HeightAtWorld(seg[i]);
                        seg[i] = new Vector3(seg[i].x, y, seg[i].z);
                    }
                    surfaces.Add(seg);
                }
            }
        }
    
        // ---------------------------------------------------------------------
        // SPLIT: BRIDGE vs NORMAL
        // ---------------------------------------------------------------------
        private static void SplitByBridgeClearance(
            TerrainCostGrid grid,
            List<Vector3> path,
            RoadGenSettings s,
            out List<List<Vector3>> normalSegments,
            out List<List<Vector3>> bridgeSegments)
        {
            var normals = new List<List<Vector3>>();
            var bridges = new List<List<Vector3>>();
    
            bool IsBridge(Vector3 p)
            {
                float terrainY = grid.HeightAtWorld(p);
                float clearance = p.y - terrainY;
                return clearance >= s.bridgeTriggerDropMeters;
            }
    
            if (path == null || path.Count < 2)
            {
                normalSegments = normals;
                bridgeSegments = bridges;
                return;
            }
    
            List<Vector3> cur = new List<Vector3>();
            bool curBridge = IsBridge(path[0]);
            cur.Add(path[0]);
    
            for (int i = 1; i < path.Count; i++)
            {
                bool b = IsBridge(path[i]);
    
                if (b != curBridge)
                {
                    cur.Add(path[i]);
                    AddSegment(cur, curBridge);
    
                    cur = new List<Vector3> { path[i] };
                    curBridge = b;
                }
                else cur.Add(path[i]);
            }
    
            AddSegment(cur, curBridge);
    
            normalSegments = normals;
            bridgeSegments = bridges;
    
            void AddSegment(List<Vector3> seg, bool isBridge)
            {
                if (seg == null || seg.Count < 2) return;
    
                if (isBridge)
                {
                    float len = 0f;
                    for (int i = 1; i < seg.Count; i++)
                        len += Vector3.Distance(seg[i - 1], seg[i]);
    
                    if (len < s.minBridgeLength)
                    {
                        normals.Add(seg);
                        return;
                    }
    
                    bridges.Add(seg);
                }
                else normals.Add(seg);
            }
        }
    
        // ---------------------------------------------------------------------
        // SCENE HELPERS
        // ---------------------------------------------------------------------
        private static GameObject EnsureGroup(Scene scene, string name)
        {
            var go = GameObject.Find(name);
            if (go == null) go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
    
        private static void ClearChildrenImmediate(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(t.GetChild(i).gameObject);
        }
    
        private static void CreateHubMarkers(Scene scene, List<Vector3> hubs, Transform parent)
        {
            if (parent == null)
            {
                var root = GameObject.Find("AI_RoadHubs");
                if (root == null) root = new GameObject("AI_RoadHubs");
                SceneManager.MoveGameObjectToScene(root, scene);
                parent = root.transform;
            }
    
            for (int i = 0; i < hubs.Count; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = $"Hub_{i:00}";
                g.transform.SetParent(parent, false);
                g.transform.position = hubs[i] + Vector3.up * 2f;
                g.transform.localScale = Vector3.one * 6f;
    
                var col = g.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }
        }
    
        private static List<Vector3> ClonePolyline(List<Vector3> src)
        {
            if (src == null) return null;
            var dst = new List<Vector3>(src.Count);
            for (int i = 0; i < src.Count; i++) dst.Add(src[i]);
            return dst;
        }
    }
}
