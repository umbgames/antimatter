using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antimatter
{
    public static class SettlementNetworkPlanner
    {
        public static void GenerateSettlements(
            Scene scene,
            Terrain terrain,
            RoadNetworkPlanner.RoadNetworkResult roads,
            SettlementGenSettings s,
            List<SettlementNode> explicitSettlements,
            string userPrompt,
            Material buildingMaterial = null,
            Material wallMaterial = null) // Kept for signature compatibility
        {
            if (!scene.IsValid()) throw new Exception("Invalid scene.");
            if (terrain == null || terrain.terrainData == null) throw new Exception("No valid terrain.");

            var root = EnsureGroup(scene, "AI_SettlementNetwork");
            ClearChildrenImmediate(root.transform);

            var rng = new System.Random(s.seed);

            // Determine locations to spawn
            List<Vector3> spawnCenters = new List<Vector3>();

            if (explicitSettlements != null && explicitSettlements.Count > 0)
            {
                foreach (var ext in explicitSettlements)
                {
                    spawnCenters.Add(SnapToTerrain(terrain, ext.center));
                }
            }
            else
            {
                // Auto-generate centers from road hubs
                if (roads != null && roads.hubsWorld != null)
                {
                    foreach (var hub in roads.hubsWorld)
                    {
                        if (rng.NextDouble() < s.hubTownChance)
                        {
                            spawnCenters.Add(SnapToTerrain(terrain, hub));
                        }
                    }
                }
                
                // Auto-generate some road-side villages
                if (s.enableRoadsideVillages && roads != null && roads.surfaceRoadCenterlines != null)
                {
                    foreach (var line in roads.surfaceRoadCenterlines)
                    {
                        if (line.Count < 2) continue;
                        if (rng.NextDouble() < s.roadsideVillageChance)
                        {
                            int idx = rng.Next(0, line.Count);
                            Vector3 pos = line[idx];
                            // Offset it from the road slightly
                            Vector3 offset = new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f).normalized * s.roadClearance;
                            spawnCenters.Add(SnapToTerrain(terrain, pos + offset));
                        }
                    }
                }
            }

            int settlementId = 0;
            foreach (var center in spawnCenters)
            {
                var setRoot = new GameObject($"Settlement_{settlementId++}");
                SceneManager.MoveGameObjectToScene(setRoot, scene);
                setRoot.transform.SetParent(root.transform, false);
                setRoot.transform.position = center;

                // Stamp the center
                TerrainStamper.StampPad(terrain, center, s.padRadius, s.padFalloff, Mathf.Max(s.padBlend, 0.95f));

                // Decide style dynamically or from settings
                BuildingType defaultBuildingType = DetermineBuildingType(s.style, rng);

                int buildingCount = (explicitSettlements != null && explicitSettlements.Count > 0) 
                                    ? s.buildingsPerTown : rng.Next(s.buildingsPerVillage, s.buildingsPerTown);

                GenerateCityLayout(scene, terrain, roads, setRoot.transform, center, s, buildingCount, defaultBuildingType, buildingMaterial, rng);
                
                // Generate outlying economic nodes (Farms, Mines)
                GenerateEconomyNodes(scene, terrain, setRoot.transform, center, s.settlementRadius, rng);
            }

            Debug.Log($"SettlementNetworkPlanner: Generated {spawnCenters.Count} settlements procedurally.");
        }

        private static void GenerateEconomyNodes(Scene scene, Terrain terrain, Transform parent, Vector3 center, float radius, System.Random rng)
        {
            // Spawn 1-3 resource nodes on the outskirts
            int numNodes = rng.Next(1, 4);
            for (int i=0; i<numNodes; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = radius * (1.2f + (float)rng.NextDouble() * 0.5f); // Outskirts
                Vector3 pos = center + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
                pos = SnapToTerrain(terrain, pos);

                float steepness = terrain.terrainData.GetSteepness(pos.x / terrain.terrainData.size.x, pos.z / terrain.terrainData.size.z);

                if (steepness > 30f)
                {
                    // Mine / Quarry
                    GameObject mine = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    mine.name = $"Mine_{i}";
                    mine.transform.SetParent(parent, true);
                    mine.transform.position = pos + Vector3.up * 2f;
                    mine.transform.localScale = new Vector3(8f, 5f, 8f);
                    
                    var rend = mine.GetComponent<Renderer>();
                    if (rend) rend.sharedMaterial.color = new Color(0.3f, 0.25f, 0.25f);
                    
                    // Mine shaft entrance
                    GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    shaft.name = "Mine_Shaft";
                    shaft.transform.SetParent(mine.transform, false);
                    shaft.transform.localPosition = new Vector3(0, 0.5f, 0);
                    shaft.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
                    var sRend = shaft.GetComponent<Renderer>();
                    if (sRend) sRend.sharedMaterial.color = Color.black;
                }
                else if (steepness < 15f)
                {
                    // Farm / Crop Field
                    GameObject farm = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    farm.name = $"Farm_{i}";
                    farm.transform.SetParent(parent, true);
                    farm.transform.position = pos + Vector3.up * 0.1f;
                    farm.transform.localScale = new Vector3(25f, 0.2f, 25f); // Large field
                    
                    var rend = farm.GetComponent<Renderer>();
                    if (rend) rend.sharedMaterial.color = new Color(0.7f, 0.6f, 0.2f); // Wheat color

                    // Silo
                    GameObject silo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    silo.name = "Silo";
                    silo.transform.SetParent(farm.transform, false);
                    silo.transform.localPosition = new Vector3(-0.4f, 15f, -0.4f); // relative to large scaled farm
                    silo.transform.localScale = new Vector3(0.15f, 15f, 0.15f);
                    var sRend = silo.GetComponent<Renderer>();
                    if (sRend) sRend.sharedMaterial.color = new Color(0.8f, 0.8f, 0.8f);
                }
            }
        }

        private static void GenerateCityLayout(
            Scene scene, 
            Terrain terrain, 
            RoadNetworkPlanner.RoadNetworkResult roads, 
            Transform parent, 
            Vector3 center, 
            SettlementGenSettings s, 
            int count, 
            BuildingType type,
            Material buildingMaterial, 
            System.Random rng)
        {
            List<Vector3> placedBuildings = new List<Vector3>();
            List<TerrainStamper.StampPadRequest> pendingPads = new List<TerrainStamper.StampPadRequest>();

            // 1. Generate local street network
            bool isGrid = s.style == SettlementStyle.ModernCityBlocks || (s.style == SettlementStyle.Auto && rng.NextDouble() > 0.5);
            
            List<Vector3> streetNodes = new List<Vector3>();
            streetNodes.Add(center); // Origin node
            
            List<List<Vector3>> localStreets = new List<List<Vector3>>();
            
            float blockSpacing = isGrid ? 40f : 50f;
            int numStreets = count / 3 + 2; // Rough number of street nodes

            for(int i = 0; i < numStreets; i++) {
                Vector3 parentNode = streetNodes[rng.Next(0, streetNodes.Count)];
                float angle = isGrid ? (rng.Next(0, 4) * 90f) * Mathf.Deg2Rad : (float)(rng.NextDouble() * Mathf.PI * 2);
                float dist = blockSpacing * (0.8f + (float)rng.NextDouble() * 0.4f);
                
                Vector3 newPos = parentNode + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
                newPos = SnapToTerrain(terrain, newPos);
                
                if (Vector3.Distance(newPos, center) < s.settlementRadius && IsBuildable(terrain, newPos, s)) {
                    streetNodes.Add(newPos);
                    localStreets.Add(new List<Vector3>() { parentNode, newPos });
                }
            }

            // Generate Central Plaza / Fountain at the core
            pendingPads.Add(new TerrainStamper.StampPadRequest { center = center, radius = 15f, falloff = s.padFalloff, blend = s.padBlend });
            GameObject plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plaza.name = "Central_Plaza";
            SceneManager.MoveGameObjectToScene(plaza, scene);
            plaza.transform.SetParent(parent, true);
            float plazaY = terrain.SampleHeight(center) + terrain.transform.position.y;
            plaza.transform.position = new Vector3(center.x, plazaY + 0.1f, center.z);
            plaza.transform.localScale = new Vector3(12f, 0.2f, 12f);
            var plazaRend = plaza.GetComponent<Renderer>();
            if (plazaRend) plazaRend.sharedMaterial.color = new Color(0.6f, 0.6f, 0.65f);

            GameObject monument = GameObject.CreatePrimitive(PrimitiveType.Cube);
            monument.name = "Monument";
            SceneManager.MoveGameObjectToScene(monument, scene);
            monument.transform.SetParent(parent, true);
            monument.transform.position = new Vector3(center.x, plazaY + 2.5f, center.z);
            monument.transform.localScale = new Vector3(1.5f, 5f, 1.5f);
            var monRend = monument.GetComponent<Renderer>();
            if (monRend) monRend.sharedMaterial.color = new Color(0.8f, 0.7f, 0.4f);

            placedBuildings.Add(center); // Reserve center space

            int attempts = count * 10;
            int placed = 0;

            for (int i = 0; i < attempts && placed < count; i++)
            {
                // Pick a random street node to spawn near
                Vector3 localStreet = streetNodes[rng.Next(0, streetNodes.Count)];
                
                float offsetAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float lotSize = Mathf.Lerp(s.lotMinSize, s.lotMaxSize, (float)rng.NextDouble());
                float offsetDist = lotSize * 0.6f + (float)rng.NextDouble() * lotSize; // Spacing from street center

                Vector3 pos = localStreet + new Vector3(Mathf.Cos(offsetAngle) * offsetDist, 0, Mathf.Sin(offsetAngle) * offsetDist);
                pos = SnapToTerrain(terrain, pos);

                if (Vector3.Distance(pos, center) > s.settlementRadius) continue;
                if (!IsBuildable(terrain, pos, s)) continue;

                // Check distance to main roads (don't overlap highways)
                if (DistanceToRoadsXZ(pos, roads?.surfaceRoadCenterlines) < s.roadClearance) continue;

                // Check overlap
                if (TooCloseToPlaced(pos, placedBuildings, lotSize)) continue;

                // Stamp the pad for the building
                pendingPads.Add(new TerrainStamper.StampPadRequest { center = pos, radius = Mathf.Max(lotSize * 0.6f, 4f), falloff = s.padFalloff, blend = s.padBlend });

                float width = lotSize * Mathf.Lerp(0.6f, 0.9f, (float)rng.NextDouble());
                float depth = lotSize * Mathf.Lerp(0.6f, 0.9f, (float)rng.NextDouble());
                float height = Mathf.Lerp(10f, lotSize * 2f, (float)rng.NextDouble());

                // Face the local street
                Vector3 toStreet = (localStreet - pos);
                toStreet.y = 0;
                
                // If it's a grid, snap building orientation to grid (90 degree increments)
                if (isGrid && toStreet.sqrMagnitude > 0.001f) {
                    float deg = Mathf.Atan2(toStreet.z, toStreet.x) * Mathf.Rad2Deg;
                    deg = Mathf.Round(deg / 90f) * 90f;
                    toStreet = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad));
                }

                Quaternion rot = toStreet.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toStreet.normalized) : Quaternion.identity;

                // Urban Zoning based on distance to center
                float distToCenter = Vector3.Distance(pos, center);
                float normalizedDist = distToCenter / s.settlementRadius;

                BuildingType actualType = type;
                if (normalizedDist < 0.35f) {
                    // Commercial Core (High Density)
                    actualType = BuildingType.Skyscraper;
                    height = Mathf.Lerp(25f, lotSize * 3.5f, (float)rng.NextDouble());
                    if (rng.NextDouble() > 0.6f) actualType = BuildingType.Shop;
                } else if (normalizedDist < 0.65f) {
                    // Mixed Zone (Medium Density)
                    actualType = BuildingType.Shop;
                    height = Mathf.Lerp(12f, lotSize * 2f, (float)rng.NextDouble());
                    if (rng.NextDouble() > 0.5f) actualType = BuildingType.House;
                } else {
                    // Residential Outskirts (Low Density)
                    actualType = BuildingType.House;
                    height = Mathf.Lerp(6f, lotSize * 1.2f, (float)rng.NextDouble());
                }

                GameObject bldg = ProceduralBuildingGenerator.GenerateBuilding(actualType, $"Building_{placed}", width, depth, height, buildingMaterial, rng);
                SceneManager.MoveGameObjectToScene(bldg, scene);
                bldg.transform.SetParent(parent, true);
                
                float groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;
                bldg.transform.position = new Vector3(pos.x, groundY, pos.z);
                bldg.transform.rotation = rot;

                placedBuildings.Add(pos);
                placed++;
            }

            // Perform batch stamp for all buildings
            TerrainStamper.StampPadsToTerrain(terrain, pendingPads);

            // Build Physical Local Streets
            for (int i = 0; i < localStreets.Count; i++) {
                GameObject roadGo = RoadMeshBuilder.BuildOrUpdateRoad(scene, terrain, $"{parent.name}_LocalStreet_{i}", localStreets[i], 2.5f, 0.05f, null, RoadMeshBuilder.RoadConformMode.TunnelNoProject, false);
                if (roadGo != null) roadGo.transform.SetParent(parent, true);
            }

            // Connect settlement core to nearest major highway
            if (roads != null && roads.surfaceRoadCenterlines != null) {
                Vector3 closestRoadPt = FindClosestRoadPoint(center, roads.surfaceRoadCenterlines);
                if (Vector3.Distance(closestRoadPt, center) > 1f) {
                    List<Vector3> connection = new List<Vector3>() { center, closestRoadPt };
                    GameObject connGo = RoadMeshBuilder.BuildOrUpdateRoad(scene, terrain, $"{parent.name}_HighwayConnection", connection, 3.5f, 0.08f, null, RoadMeshBuilder.RoadConformMode.TunnelNoProject, false);
                    if (connGo != null) connGo.transform.SetParent(parent, true);
                }
            }
        }

        private static BuildingType DetermineBuildingType(SettlementStyle style, System.Random rng)
        {
            switch (style)
            {
                case SettlementStyle.ModernCityBlocks: return BuildingType.Skyscraper;
                case SettlementStyle.Suburban: return BuildingType.House;
                case SettlementStyle.RuralVillage: return BuildingType.House;
                default: return rng.NextDouble() > 0.5f ? BuildingType.House : BuildingType.Shop;
            }
        }

        private static bool IsBuildable(Terrain terrain, Vector3 p, SettlementGenSettings s)
        {
            float slope = SampleSlopeDeg(terrain, p);
            if (slope > s.maxSlopeDeg) return false;
            return true;
        }

        private static float SampleSlopeDeg(Terrain terrain, Vector3 world)
        {
            var td = terrain.terrainData;
            Vector3 tp = terrain.transform.position;
            Vector3 ts = td.size;

            float nx = Mathf.InverseLerp(tp.x, tp.x + ts.x, world.x);
            float nz = Mathf.InverseLerp(tp.z, tp.z + ts.z, world.z);
            Vector3 n = td.GetInterpolatedNormal(nx, nz);
            return Vector3.Angle(Vector3.up, n);
        }

        private static Vector3 SnapToTerrain(Terrain terrain, Vector3 p)
        {
            float y = terrain.SampleHeight(p) + terrain.transform.position.y;
            return new Vector3(p.x, y, p.z);
        }

        private static float DistanceToRoadsXZ(Vector3 p, List<List<Vector3>> roadLines)
        {
            if (roadLines == null || roadLines.Count == 0) return 9999f;
            float minDist = float.MaxValue;
            Vector2 p2 = new Vector2(p.x, p.z);

            foreach (var line in roadLines)
            {
                if (line == null) continue;
                for (int i = 0; i < line.Count - 1; i++)
                {
                    Vector2 a = new Vector2(line[i].x, line[i].z);
                    Vector2 b = new Vector2(line[i+1].x, line[i+1].z);
                    float d = DistPointSegment2(p2, a, b);
                    if (d < minDist) minDist = d;
                }
            }
            return Mathf.Sqrt(minDist);
        }

        private static bool TooCloseToPlaced(Vector3 p, List<Vector3> placed, float minLotDistance)
        {
            float m2 = minLotDistance * minLotDistance;
            foreach (var pos in placed)
            {
                float d2 = (p.x - pos.x) * (p.x - pos.x) + (p.z - pos.z) * (p.z - pos.z);
                if (d2 < m2) return true;
            }
            return false;
        }

        private static float DistPointSegment2(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, Vector2.Dot(ab, ab));
            t = Mathf.Clamp01(t);
            Vector2 proj = a + t * ab;
            return (p - proj).sqrMagnitude;
        }

        private static Vector3 FindClosestRoadPoint(Vector3 p, List<List<Vector3>> roadLines)
        {
            if (roadLines == null || roadLines.Count == 0) return p;
            Vector3 closest = p;
            float minDist = float.MaxValue;

            foreach (var line in roadLines)
            {
                if (line == null) continue;
                for (int i = 0; i < line.Count - 1; i++)
                {
                    Vector3 proj = ClosestPointOnSegment(p, line[i], line[i+1]);
                    float d = (p - proj).sqrMagnitude;
                    if (d < minDist) {
                        minDist = d;
                        closest = proj;
                    }
                }
            }
            return closest;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, Vector3.Dot(ab, ab));
            t = Mathf.Clamp01(t);
            return a + t * ab;
        }

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
    }
}
