using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antimatter
{
    
    public static class CitySceneArranger
    {
        private const float BlockSpacing = 28f;
        private const float SegmentLen = 18f;
    
        private const float RoadWidth = 7.0f;
        private const float SidewalkWidth = 2.5f;
    
        // Keep these tiny or you’ll see hover. If z-fighting happens, raise slightly.
        private const float RoadYOffset = 0.0015f;
        private const float SidewalkYOffset = 0.0025f;
    
        private const float RoadFalloff = 4.0f;
        private const float RoadBlend = 0.75f;
        private const float RoadCutDepth = 0.18f;
    
        private const float SidewalkFalloff = 2.0f;
        private const float SidewalkBlend = 0.70f;
        private const float SidewalkCutDepth = 0.10f;
    
        private const float ResampleStep = 3.0f;
        private const float MaxTurnDegrees = 40f;
        private const int CornerCutPasses = 3;
        private const int HeightSmoothPasses = 4;
    
        private const bool CleanupLegacySegments = true;
    
        private const string RoadRootName = "__AI_RoadMeshes__";
        private const string RoadNameMain = "__AI_MainRoad__";
        private const string RoadNameSideA = "__AI_SideRoadA__";
        private const string RoadNameSideB = "__AI_SideRoadB__";
        private const string SidewalkNameL = "__AI_SidewalkL__";
        private const string SidewalkNameR = "__AI_SidewalkR__";
    
        public static void ArrangeScene(Scene scene, Terrain terrain, string userPrompt)
        {
            if (!scene.IsValid()) return;
            if (terrain == null || terrain.terrainData == null) return;
    
            var buildings = FindByTokens(scene, "building", "house", "tower", "shop", "office", "block").ToList();
            if (buildings.Count == 0) return;
    
            Debug.Log($"[CitySceneArranger] RUN. Scene={scene.name} Terrain={terrain.name} Buildings={buildings.Count}");
    
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = terrain.terrainData.size;
    
            int bCount = Mathf.Max(8, buildings.Count);
            int ring = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(4, bCount))), 2, 10);
            float cityRadius = ring * BlockSpacing;
            cityRadius = Mathf.Min(cityRadius, Mathf.Min(tSize.x, tSize.z) * 0.35f);
    
            int seed = StableSeed(userPrompt);
    
            // Pick flattest region that can actually fit
            Vector3 cityCenter = FindBestCityCenter(terrain, cityRadius, seed);
    
            // margins so offsets never spill outside
            float roadMargin = RoadWidth * 0.5f + RoadFalloff + 3f;
            float sidewalkOffset = (RoadWidth * 0.5f) + (SidewalkWidth * 0.5f);
            float sidewalkMargin = sidewalkOffset + SidewalkWidth * 0.5f + SidewalkFalloff + 3f;
    
            // 1) main + branches
            List<Vector3> mainPath = BuildMainRoadPath(terrain, cityCenter, cityRadius, seed);
            if (mainPath == null || mainPath.Count < 2) return;
    
            var branchA = BuildBranch(terrain, mainPath, seed + 101, side: -1);
            var branchB = BuildBranch(terrain, mainPath, seed + 202, side: +1);
    
            // 2) smooth and clamp properly
            mainPath = MakeNicePath(terrain, mainPath, roadMargin);
            branchA = MakeNicePath(terrain, branchA, roadMargin);
            branchB = MakeNicePath(terrain, branchB, roadMargin);
    
            // 3) sidewalks
            var sidewalkLeftPath = OffsetPath(mainPath, -sidewalkOffset);
            var sidewalkRightPath = OffsetPath(mainPath, +sidewalkOffset);
    
            ClampPathToTerrain(terrain, sidewalkLeftPath, sidewalkMargin);
            ClampPathToTerrain(terrain, sidewalkRightPath, sidewalkMargin);
            SamplePathHeights(terrain, sidewalkLeftPath);
            SamplePathHeights(terrain, sidewalkRightPath);
            SmoothHeightsInPlace(sidewalkLeftPath, 2);
            SmoothHeightsInPlace(sidewalkRightPath, 2);
    
            // 4) cleanup old cubes
            if (CleanupLegacySegments)
            {
                DestroyByNamePrefix(scene, "MainRoad_Segment_");
                DestroyByNamePrefix(scene, "SideRoadA_Segment_");
                DestroyByNamePrefix(scene, "SideRoadB_Segment_");
                DestroyByNamePrefix(scene, "SidewalkL_Segment_");
                DestroyByNamePrefix(scene, "SidewalkR_Segment_");
            }
    
            // 5) root
            var roadRoot = GetOrCreateRoadRoot(scene);
    
            // 6) stamp terrain
            TerrainStamper.StampPath(terrain, mainPath, RoadWidth * 0.5f, RoadFalloff, RoadBlend, RoadCutDepth, RoadYOffset);
            TerrainStamper.StampPath(terrain, branchA, (RoadWidth * 0.75f) * 0.5f, 3.0f, 0.65f, RoadCutDepth, RoadYOffset);
            TerrainStamper.StampPath(terrain, branchB, (RoadWidth * 0.75f) * 0.5f, 3.0f, 0.65f, RoadCutDepth, RoadYOffset);
            TerrainStamper.StampPath(terrain, sidewalkLeftPath, SidewalkWidth * 0.5f, SidewalkFalloff, SidewalkBlend, SidewalkCutDepth, SidewalkYOffset);
            TerrainStamper.StampPath(terrain, sidewalkRightPath, SidewalkWidth * 0.5f, SidewalkFalloff, SidewalkBlend, SidewalkCutDepth, SidewalkYOffset);
    
            // 7) resample after stamping
            SamplePathHeights(terrain, mainPath);
            SamplePathHeights(terrain, branchA);
            SamplePathHeights(terrain, branchB);
            SamplePathHeights(terrain, sidewalkLeftPath);
            SamplePathHeights(terrain, sidewalkRightPath);
    
            // 8) mesh roads (terrain hugging)
            var mainRoadGo = RoadMeshBuilder.BuildOrUpdateRoad(scene, terrain, RoadNameMain, mainPath, RoadWidth * 0.5f, RoadYOffset);
            var sideAGo = RoadMeshBuilder.BuildOrUpdateRoad(scene, terrain, RoadNameSideA, branchA, (RoadWidth * 0.75f) * 0.5f, RoadYOffset);
            var sideBGo = RoadMeshBuilder.BuildOrUpdateRoad(scene, terrain, RoadNameSideB, branchB, (RoadWidth * 0.75f) * 0.5f, RoadYOffset);
            var swLGo = RoadMeshBuilder.BuildOrUpdateRoad(scene, terrain, SidewalkNameL, sidewalkLeftPath, SidewalkWidth * 0.5f, SidewalkYOffset);
            var swRGo = RoadMeshBuilder.BuildOrUpdateRoad(scene, terrain, SidewalkNameR, sidewalkRightPath, SidewalkWidth * 0.5f, SidewalkYOffset);
    
            ParentIfNotNull(mainRoadGo, roadRoot);
            ParentIfNotNull(sideAGo, roadRoot);
            ParentIfNotNull(sideBGo, roadRoot);
            ParentIfNotNull(swLGo, roadRoot);
            ParentIfNotNull(swRGo, roadRoot);
    
            // 9) place buildings (inside terrain, with spacing + slope filter)
            PlaceBuildingsNearRoadSpaced(terrain, buildings, mainPath, cityCenter, cityRadius, seed);
    
            // 10) flatten terrain under buildings (REAL pads)
            StampBuildingPadsMedianFlatten(terrain, buildings);
    
            // 11) snap buildings after pads
            SnapBuildingsToTerrain(buildings, terrain);
        }
    
        private static void ParentIfNotNull(GameObject child, GameObject parent)
        {
            if (child == null || parent == null) return;
            if (child.transform.parent == parent.transform) return;
            Undo.SetTransformParent(child.transform, parent.transform, "Parent Road Mesh");
        }
    
        private static GameObject GetOrCreateRoadRoot(Scene scene)
        {
            var root = GameObject.Find(RoadRootName);
            if (root != null) return root;
    
            root = new GameObject(RoadRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Road Root");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }
    
        // ---------------- FLAT CITY CENTER ----------------
    
        private static Vector3 FindBestCityCenter(Terrain t, float cityRadius, int seed)
        {
            var tp = t.transform.position;
            var td = t.terrainData;
            var sz = td.size;
    
            float margin = Mathf.Clamp(cityRadius + 10f, 20f, Mathf.Min(sz.x, sz.z) * 0.45f);
            float step = Mathf.Clamp(cityRadius * 0.25f, 18f, 45f);
    
            Vector3 best = tp + new Vector3(sz.x * 0.5f, 0f, sz.z * 0.5f);
            float bestScore = float.PositiveInfinity;
    
            float r = Mathf.Clamp(cityRadius * 0.35f, 18f, 60f);
            Vector2[] offsets =
            {
                new Vector2(0,0),
                new Vector2(+r,0), new Vector2(-r,0), new Vector2(0,+r), new Vector2(0,-r),
                new Vector2(+r*0.7f,+r*0.7f), new Vector2(-r*0.7f,+r*0.7f),
                new Vector2(+r*0.7f,-r*0.7f), new Vector2(-r*0.7f,-r*0.7f),
            };
    
            for (float x = tp.x + margin; x <= tp.x + sz.x - margin; x += step)
            {
                for (float z = tp.z + margin; z <= tp.z + sz.z - margin; z += step)
                {
                    float slopeSum = 0f;
                    float hSum = 0f, h2Sum = 0f;
    
                    for (int i = 0; i < offsets.Length; i++)
                    {
                        float cx = x + offsets[i].x;
                        float cz = z + offsets[i].y;
    
                        float nx = Mathf.InverseLerp(tp.x, tp.x + sz.x, cx);
                        float nz = Mathf.InverseLerp(tp.z, tp.z + sz.z, cz);
    
                        float slope = td.GetSteepness(nx, nz);
                        float h = t.SampleHeight(new Vector3(cx, 0f, cz));
    
                        slopeSum += slope;
                        hSum += h;
                        h2Sum += h * h;
                    }
    
                    float n = offsets.Length;
                    float slopeAvg = slopeSum / n;
                    float meanH = hSum / n;
                    float varH = Mathf.Max(0f, (h2Sum / n) - (meanH * meanH));
    
                    float score = slopeAvg + (Mathf.Sqrt(varH) * 6.0f);
                    score += (Hash01(seed, ((int)x * 73856093) ^ ((int)z * 19349663)) - 0.5f) * 0.05f;
    
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = new Vector3(x, 0f, z);
                    }
                }
            }
    
            best.x = ClampX(t, best.x, cityRadius + 6f);
            best.z = ClampZ(t, best.z, cityRadius + 6f);
            best.y = t.SampleHeight(best) + tp.y;
            return best;
        }
    
        // ---------------- PATH ----------------
    
        private static List<Vector3> MakeNicePath(Terrain t, List<Vector3> path, float margin)
        {
            if (path == null || path.Count < 2) return path;
    
            ClampPathToTerrain(t, path, margin);
            SamplePathHeights(t, path);
    
            path = ResamplePath(path, ResampleStep);
            path = CornerCut(path, MaxTurnDegrees, CornerCutPasses);
    
            ClampPathToTerrain(t, path, margin);
            SamplePathHeights(t, path);
    
            SmoothHeightsInPlace(path, HeightSmoothPasses);
    
            ClampPathToTerrain(t, path, margin);
            SamplePathHeights(t, path);
    
            return path;
        }
    
        private static List<Vector3> ResamplePath(List<Vector3> path, float stepMeters)
        {
            if (path == null || path.Count < 2) return path;
            stepMeters = Mathf.Max(0.5f, stepMeters);
    
            var outPath = new List<Vector3> { path[0] };
            float carry = 0f;
    
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1];
                Vector3 b = path[i];
                Vector3 ab = b - a;
    
                float len = new Vector2(ab.x, ab.z).magnitude;
                if (len < 0.0001f) continue;
    
                Vector3 dir = new Vector3(ab.x, 0f, ab.z) / len;
    
                float dist = carry;
                while (dist + stepMeters <= len)
                {
                    dist += stepMeters;
                    Vector3 p = a + dir * dist;
                    p.y = Mathf.Lerp(a.y, b.y, dist / len);
                    outPath.Add(p);
                }
    
                carry = len - dist;
            }
    
            if ((outPath[outPath.Count - 1] - path[path.Count - 1]).sqrMagnitude > 0.01f)
                outPath.Add(path[path.Count - 1]);
    
            return outPath;
        }
    
        private static List<Vector3> CornerCut(List<Vector3> path, float maxTurnDegrees, int passes)
        {
            if (path == null || path.Count < 3) return path;
            maxTurnDegrees = Mathf.Clamp(maxTurnDegrees, 5f, 175f);
    
            var p = new List<Vector3>(path);
    
            for (int pass = 0; pass < Mathf.Max(1, passes); pass++)
            {
                for (int i = 1; i < p.Count - 1; i++)
                {
                    Vector3 a = p[i - 1];
                    Vector3 b = p[i];
                    Vector3 c = p[i + 1];
    
                    Vector3 ab = (b - a); ab.y = 0f;
                    Vector3 bc = (c - b); bc.y = 0f;
    
                    if (ab.sqrMagnitude < 0.001f || bc.sqrMagnitude < 0.001f) continue;
    
                    float ang = Vector3.Angle(ab, bc);
                    if (ang > maxTurnDegrees)
                    {
                        Vector3 mid = (a + c) * 0.5f;
                        p[i] = Vector3.Lerp(p[i], mid, 0.55f);
                    }
                }
            }
    
            return p;
        }
    
        private static void SmoothHeightsInPlace(List<Vector3> path, int passes)
        {
            if (path == null || path.Count < 3) return;
    
            for (int pass = 0; pass < Mathf.Max(1, passes); pass++)
            {
                for (int i = 1; i < path.Count - 1; i++)
                {
                    var prev = path[i - 1];
                    var cur = path[i];
                    var next = path[i + 1];
    
                    float targetY = (prev.y + next.y) * 0.5f;
                    cur.y = Mathf.Lerp(cur.y, targetY, 0.40f);
                    path[i] = cur;
                }
            }
        }
    
        private static List<Vector3> OffsetPath(List<Vector3> path, float offset)
        {
            var outPath = new List<Vector3>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 fwd;
                if (i == 0) fwd = path[i + 1] - path[i];
                else if (i == path.Count - 1) fwd = path[i] - path[i - 1];
                else fwd = path[i + 1] - path[i - 1];
    
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                fwd.Normalize();
    
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                outPath.Add(path[i] + right * offset);
            }
            return outPath;
        }
    
        private static void ClampPathToTerrain(Terrain t, List<Vector3> path, float margin)
        {
            if (path == null) return;
            for (int i = 0; i < path.Count; i++)
            {
                var p = path[i];
                p.x = ClampX(t, p.x, margin);
                p.z = ClampZ(t, p.z, margin);
                path[i] = p;
            }
        }
    
        private static void SamplePathHeights(Terrain t, List<Vector3> path)
        {
            if (path == null) return;
            var tp = t.transform.position;
            for (int i = 0; i < path.Count; i++)
            {
                var p = path[i];
                p.y = t.SampleHeight(p) + tp.y;
                path[i] = p;
            }
        }
    
        // ---------------- ROADS GEN ----------------
    
        private static List<Vector3> BuildMainRoadPath(Terrain t, Vector3 center, float radius, int seed)
        {
            int pts = Mathf.Clamp(Mathf.RoundToInt((radius * 2f) / SegmentLen), 8, 64);
            var list = new List<Vector3>(pts);
    
            float z0 = center.z - radius;
            float z1 = center.z + radius;
    
            float x = center.x + Mathf.Lerp(-radius * 0.15f, radius * 0.15f, Hash01(seed, 1));
    
            for (int i = 0; i < pts; i++)
            {
                float t01 = i / (float)(pts - 1);
                float z = Mathf.Lerp(z0, z1, t01);
    
                float bestX = x;
                float bestH = SampleH(t, bestX, z);
    
                float scan = radius * 0.25f;
                int steps = 10;
                for (int s = 0; s <= steps; s++)
                {
                    float candX = x + Mathf.Lerp(-scan, scan, s / (float)steps);
                    float h = SampleH(t, candX, z);
                    if (h < bestH)
                    {
                        bestH = h;
                        bestX = candX;
                    }
                }
    
                float wiggle = Mathf.Lerp(-6f, 6f, Hash01(seed, 1000 + i));
                x = Mathf.Lerp(x, bestX + wiggle, 0.55f);
    
                x = ClampX(t, x, radius + 6f);
                z = ClampZ(t, z, radius + 6f);
    
                float y = SampleH(t, x, z) + t.transform.position.y;
                list.Add(new Vector3(x, y, z));
            }
    
            return SmoothPath(list, 2);
        }
    
        private static List<Vector3> BuildBranch(Terrain t, List<Vector3> main, int seed, int side)
        {
            if (main == null || main.Count < 6) return new List<Vector3>();
    
            int mid = Mathf.Clamp(main.Count / 2 + (side * Mathf.RoundToInt(Hash01(seed, 9) * 4f)), 2, main.Count - 3);
            Vector3 start = main[mid];
    
            int pts = 10;
            float step = SegmentLen * 0.9f;
            var list = new List<Vector3>(pts) { start };
    
            Vector3 dir = (side < 0) ? Vector3.left : Vector3.right;
    
            for (int i = 1; i < pts; i++)
            {
                Vector3 prev = list[i - 1];
    
                Vector3 best = prev;
                float bestH = -999f;
    
                for (int k = -2; k <= 2; k++)
                {
                    float ang = k * 18f;
                    Vector3 d = Quaternion.Euler(0f, ang, 0f) * dir;
                    Vector3 cand = prev + d.normalized * step;
    
                    cand.x = ClampX(t, cand.x, 8f);
                    cand.z = ClampZ(t, cand.z, 8f);
    
                    float h = SampleH(t, cand.x, cand.z);
                    if (h > bestH)
                    {
                        bestH = h;
                        best = cand;
                    }
                }
    
                best.y = bestH + t.transform.position.y;
                list.Add(best);
            }
    
            return SmoothPath(list, 1);
        }
    
        private static List<Vector3> SmoothPath(List<Vector3> p, int passes)
        {
            if (p == null || p.Count < 3) return p;
            var a = new List<Vector3>(p);
    
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 1; i < a.Count - 1; i++)
                {
                    Vector3 prev = a[i - 1];
                    Vector3 cur = a[i];
                    Vector3 next = a[i + 1];
                    a[i] = Vector3.Lerp(cur, (prev + next) * 0.5f, 0.35f);
                }
            }
            return a;
        }
    
        // ---------------- BUILDINGS + PADS ----------------
    
        private static void PlaceBuildingsNearRoadSpaced(
            Terrain t,
            List<GameObject> buildings,
            List<Vector3> mainPath,
            Vector3 center,
            float radius,
            int seed)
        {
            buildings.Sort((a, b) => WeightBuildingName(b.name).CompareTo(WeightBuildingName(a.name)));
    
            var placed = new List<(Vector2 pos, float r)>();
    
            bool IsAcceptableSlope(Vector3 pos, float maxSlopeDeg)
            {
                var tp = t.transform.position;
                var sz = t.terrainData.size;
                float nx = Mathf.InverseLerp(tp.x, tp.x + sz.x, pos.x);
                float nz = Mathf.InverseLerp(tp.z, tp.z + sz.z, pos.z);
                float slope = t.terrainData.GetSteepness(nx, nz);
                return slope <= maxSlopeDeg;
            }
    
            bool TryPlace(GameObject go, Vector3 pos, Vector3 lookDir, float lotRadius)
            {
                if (!IsAcceptableSlope(pos, 18f)) return false;
    
                Vector2 p2 = new Vector2(pos.x, pos.z);
                if (OverlapsPlaced(placed, p2, lotRadius)) return false;
    
                float y = t.SampleHeight(pos) + t.transform.position.y;
    
                Undo.RecordObject(go.transform, "Place Building");
                go.transform.position = new Vector3(pos.x, y + (GetHeight(go) * 0.5f), pos.z);
    
                if (lookDir.sqrMagnitude > 0.0001f)
                    go.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
    
                placed.Add((p2, lotRadius));
                return true;
            }
    
            for (int i = 0; i < buildings.Count; i++)
            {
                var go = buildings[i];
                if (go == null) continue;
    
                float rFoot = GetFootprintRadiusRobust(go);
                float clearance = Mathf.Clamp(rFoot * 0.60f, 3.5f, 14f);
                float lotRadius = Mathf.Max(6f, rFoot + clearance);
    
                bool big = WeightBuildingName(go.name) >= 2;
                float near = big ? 16f : 22f;
                float far = big ? 34f : 58f;
    
                bool placedOk = false;
    
                for (int attempt = 0; attempt < 140; attempt++)
                {
                    float tRoad = Mathf.Lerp(0.08f, 0.98f, Hash01(seed, 2000 + i * 97 + attempt));
                    int seg = Mathf.Clamp(Mathf.FloorToInt(tRoad * (mainPath.Count - 2)), 0, mainPath.Count - 2);
    
                    Vector3 a = mainPath[seg];
                    Vector3 b = mainPath[seg + 1];
                    Vector3 d = (b - a);
    
                    Vector3 fwd = new Vector3(d.x, 0f, d.z);
                    if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                    fwd.Normalize();
    
                    Vector3 rightV = Vector3.Cross(Vector3.up, fwd).normalized;
    
                    float side = (Hash01(seed, 3100 + i * 151 + attempt) < 0.5f) ? -1f : 1f;
                    float off = Mathf.Lerp(near, far, Hash01(seed, 3000 + i * 131 + attempt));
                    float along = Mathf.Lerp(-22f, 22f, Hash01(seed, 9000 + i * 19 + attempt));
    
                    Vector3 mid = (a + b) * 0.5f;
                    Vector3 pos = mid + fwd * along + rightV * (off * side);
    
                    float jx = Mathf.Lerp(-6f, 6f, Hash01(seed, 9100 + i * 23 + attempt));
                    float jz = Mathf.Lerp(-6f, 6f, Hash01(seed, 9200 + i * 29 + attempt));
                    pos += new Vector3(jx, 0f, jz);
    
                    pos = ClampToRadius(pos, center, radius);
    
                    pos.x = ClampX(t, pos.x, 6f);
                    pos.z = ClampZ(t, pos.z, 6f);
    
                    Vector3 lookDir = (-rightV * side);
    
                    if (TryPlace(go, pos, lookDir, lotRadius))
                    {
                        placedOk = true;
                        break;
                    }
                }
    
                if (placedOk) continue;
    
                for (int attempt = 0; attempt < 220; attempt++)
                {
                    Vector3 pos = center + new Vector3(
                        Mathf.Lerp(-radius, radius, Hash01(seed, 7000 + i * 31 + attempt)),
                        0f,
                        Mathf.Lerp(-radius, radius, Hash01(seed, 7100 + i * 37 + attempt))
                    );
    
                    pos.x = ClampX(t, pos.x, 6f);
                    pos.z = ClampZ(t, pos.z, 6f);
    
                    float yaw = Mathf.Lerp(0f, 360f, Hash01(seed, 7200 + i * 41 + attempt));
                    Vector3 lookDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    
                    if (TryPlace(go, pos, lookDir, lotRadius))
                        break;
                }
            }
        }
    
        private static void StampBuildingPadsMedianFlatten(Terrain terrain, List<GameObject> buildings)
        {
            var tp = terrain.transform.position;
    
            for (int i = 0; i < buildings.Count; i++)
            {
                var go = buildings[i];
                if (go == null) continue;
    
                float r = GetFootprintRadiusRobust(go);
                float padRadius = Mathf.Clamp(r * 1.6f, 10f, 30f);
                float falloff = Mathf.Clamp(padRadius * 0.35f, 3f, padRadius);
    
                Vector3 c = go.transform.position;
    
                Vector3[] samplePts =
                {
                    c,
                    c + new Vector3(+r,0,+r),
                    c + new Vector3(-r,0,+r),
                    c + new Vector3(+r,0,-r),
                    c + new Vector3(-r,0,-r),
                    c + new Vector3(+r,0,0),
                    c + new Vector3(-r,0,0),
                    c + new Vector3(0,0,+r),
                    c + new Vector3(0,0,-r),
                };
    
                float[] ys = new float[samplePts.Length];
                for (int k = 0; k < samplePts.Length; k++)
                    ys[k] = terrain.SampleHeight(samplePts[k]) + tp.y;
    
                Array.Sort(ys);
                float medianY = ys[ys.Length / 2];
    
                TerrainStamper.StampPad(
                    terrain,
                    center: c,
                    radius: padRadius,
                    falloff: falloff,
                    blend: 0.90f,
                    targetWorldY: medianY
                );
            }
        }
    
        private static void SnapBuildingsToTerrain(List<GameObject> buildings, Terrain terrain)
        {
            Vector3 tPos = terrain.transform.position;
    
            for (int i = 0; i < buildings.Count; i++)
            {
                var go = buildings[i];
                if (go == null) continue;
    
                Vector3 p = go.transform.position;
                float y = terrain.SampleHeight(p) + tPos.y;
    
                p.y = y + (GetHeight(go) * 0.5f);
    
                Undo.RecordObject(go.transform, "Snap Building To Terrain");
                go.transform.position = p;
            }
        }
    
        private static void DestroyByNamePrefix(Scene scene, string prefix)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var list = root.GetComponentsInChildren<Transform>(true).ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    var go = list[i].gameObject;
                    if (go == null) continue;
                    if (go.name.StartsWith(prefix, StringComparison.Ordinal))
                        Undo.DestroyObjectImmediate(go);
                }
            }
        }
    
        private static bool OverlapsPlaced(List<(Vector2 pos, float r)> placed, Vector2 p, float r)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                float rr = placed[i].r + r;
                if ((placed[i].pos - p).sqrMagnitude < rr * rr) return true;
            }
            return false;
        }
    
        private static float GetFootprintRadiusRobust(GameObject go)
        {
            var col = go.GetComponentInChildren<Collider>();
            if (col != null)
            {
                var e = col.bounds.extents;
                return Mathf.Max(2f, Mathf.Max(e.x, e.z));
            }
    
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var e = rend.bounds.extents;
                return Mathf.Max(2f, Mathf.Max(e.x, e.z));
            }
    
            return Mathf.Max(2f, Mathf.Max(go.transform.localScale.x, go.transform.localScale.z) * 0.5f);
        }
    
        private static float GetHeight(GameObject go)
        {
            var rend = go.GetComponentInChildren<Renderer>();
            if (rend != null) return Mathf.Max(0.1f, rend.bounds.size.y);
            return Mathf.Max(0.1f, go.transform.localScale.y);
        }
    
        private static int WeightBuildingName(string n)
        {
            n = (n ?? "").ToLowerInvariant();
            if (n.Contains("tower") || n.Contains("office") || n.Contains("landmark")) return 3;
            if (n.Contains("building") || n.Contains("block")) return 2;
            if (n.Contains("shop") || n.Contains("house")) return 1;
            return 0;
        }
    
        private static IEnumerable<GameObject> FindByTokens(Scene scene, params string[] tokens)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    string n = tr.name.ToLowerInvariant();
                    for (int i = 0; i < tokens.Length; i++)
                        if (n.Contains(tokens[i]))
                            yield return tr.gameObject;
                }
            }
        }
    
        private static float SampleH(Terrain t, float x, float z) => t.SampleHeight(new Vector3(x, 0f, z));
    
        private static float ClampX(Terrain t, float x, float margin)
        {
            var tp = t.transform.position;
            var sz = t.terrainData.size;
            return Mathf.Clamp(x, tp.x + margin, tp.x + sz.x - margin);
        }
    
        private static float ClampZ(Terrain t, float z, float margin)
        {
            var tp = t.transform.position;
            var sz = t.terrainData.size;
            return Mathf.Clamp(z, tp.z + margin, tp.z + sz.z - margin);
        }
    
        private static Vector3 ClampToRadius(Vector3 p, Vector3 center, float r)
        {
            Vector3 d = p - center;
            d.y = 0f;
            float m = d.magnitude;
            if (m <= r) return p;
            d = d / Mathf.Max(0.001f, m);
            return center + d * r;
        }
    
        private static int StableSeed(string s)
        {
            unchecked
            {
                if (string.IsNullOrEmpty(s)) return 1337;
                int h = 23;
                for (int i = 0; i < s.Length; i++)
                    h = h * 31 + s[i];
                return h;
            }
        }
    
        private static float Hash01(int seed, int salt)
        {
            unchecked
            {
                int h = unchecked((int)2166136261);
                h = (h ^ seed) * 16777619;
                h = (h ^ salt) * 16777619;
                int v = h & 0x00FFFFFF;
                return v / 16777216f;
            }
        }
    }
}
