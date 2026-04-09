using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antimatter
{
    
    public static class CityRootPlacer
    {
        public const string CityRootName = "__AI_CityRoot__";
    
        // Tuning knobs
        private const float TerrainMargin = 10f;     // keep city away from terrain edges
        private const int ScanGrid = 9;             // scan spots (9x9)
        private const float RoughSampleRadius = 10f;
        private const int RoughSamples = 9;         // per spot
    
        // Flatten behavior
        private const float FlattenBlend = 0.85f;
        private const float FlattenFalloff = 18f;
    
        // Decide what counts as “city stuff”
        private static readonly string[] CityTokens =
        {
            "building","house","tower","block",
            "road","mainroad","sideroad","street","avenue",
            "sidewalk","pavement","path",
            "city"
        };
    
        // Mesh roads we should NOT vertical-snap (they are already built in world space)
        private static bool IsGeneratedMeshRoad(GameObject go)
        {
            string n = (go.name ?? "").ToLowerInvariant();
            if (n.Contains("__ai_road") || n.Contains("__ai_sidewalk") || n.Contains("__ai_mainroad") || n.Contains("__ai_sideroad"))
                return true;
    
            // If it has meshfilter + mesh name matches our generator style, treat it as generated
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                string mn = mf.sharedMesh.name.ToLowerInvariant();
                if (mn.Contains("__ai_") || mn.Contains("road") || mn.Contains("sidewalk"))
                    return true;
            }
    
            return false;
        }
    
        /// <summary>
        /// Groups city objects into a root, moves root onto least-rough area of terrain,
        /// flattens the terrain under the city's footprint, and snaps city objects.
        /// </summary>
        public static void PlaceCityOnTerrainAndFlatten(Scene scene, Terrain terrain)
        {
            if (!scene.IsValid()) return;
            if (terrain == null || terrain.terrainData == null) return;
    
            // 1) Create / find CityRoot
            var root = GameObject.Find(CityRootName);
            if (root == null)
            {
                root = new GameObject(CityRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create City Root");
                SceneManager.MoveGameObjectToScene(root, scene);
            }
    
            // 2) Collect city objects (exclude terrain, camera/light)
            var cityObjects = CollectCityObjects(scene, terrain, root);
            if (cityObjects.Count == 0) return;
    
            // 3) Parent them under CityRoot without changing world positions
            foreach (var go in cityObjects)
            {
                if (go == null) continue;
                if (go.transform.parent == root.transform) continue;
                Undo.SetTransformParent(go.transform, root.transform, "Group City Under Root");
            }
    
            // 4) Compute city bounds in WORLD space (after parenting)
            if (!TryComputeBounds(cityObjects, out Bounds cityBounds))
                return;
    
            // 5) Find best placement spot on terrain (low roughness + fits bounds)
            Vector3 targetCenter = FindBestPlacement(terrain, cityBounds);
    
            // 6) Move CityRoot so city bounds center matches target center (XZ only)
            Vector3 currentCenter = cityBounds.center;
            Vector3 delta = new Vector3(targetCenter.x - currentCenter.x, 0f, targetCenter.z - currentCenter.z);
    
            Undo.RecordObject(root.transform, "Move City Root");
            root.transform.position += delta;
    
            // 7) Recompute bounds after move
            cityObjects = CollectCityObjects(scene, terrain, root); // refresh references
            TryComputeBounds(cityObjects, out cityBounds);
    
            // 8) Flatten terrain under city bounds (soft edge)
            float baseY = terrain.SampleHeight(cityBounds.center) + terrain.transform.position.y;
            TerrainStamper.StampRectPadWorld(
                terrain,
                cityBounds,
                baseY,
                falloff: FlattenFalloff,
                blend: FlattenBlend
            );
    
            // 9) Snap city objects to terrain height
            SnapCityObjectsToTerrain(cityObjects, terrain);
        }
    
        private static List<GameObject> CollectCityObjects(Scene scene, Terrain terrain, GameObject cityRoot)
        {
            var list = new List<GameObject>();
            var roots = scene.GetRootGameObjects();
    
            for (int i = 0; i < roots.Length; i++)
            {
                foreach (var tr in roots[i].GetComponentsInChildren<Transform>(true))
                {
                    if (tr == null) continue;
                    var go = tr.gameObject;
                    if (go == null) continue;
    
                    // Skip terrain + its children
                    if (go.GetComponent<Terrain>() != null) continue;
                    if (go.GetComponentInParent<Terrain>() != null) continue;
    
                    // Skip cameras / lights
                    if (go.GetComponent<Camera>() != null) continue;
                    if (go.GetComponent<Light>() != null) continue;
    
                    // Skip the root itself
                    if (cityRoot != null && go == cityRoot) continue;
    
                    // Skip UI/editor junk
                    string n = (go.name ?? "").ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(n)) continue;
    
                    // Must match city token
                    bool match = false;
                    for (int t = 0; t < CityTokens.Length; t++)
                    {
                        if (n.Contains(CityTokens[t]))
                        {
                            match = true;
                            break;
                        }
                    }
                    if (!match) continue;
    
                    list.Add(go);
                }
            }
    
            // Remove duplicates (same object can be found multiple times)
            return list.Distinct().ToList();
        }
    
        private static bool TryComputeBounds(List<GameObject> objs, out Bounds b)
        {
            b = new Bounds();
            bool has = false;
    
            for (int i = 0; i < objs.Count; i++)
            {
                var go = objs[i];
                if (go == null) continue;
    
                // Prefer renderer bounds (most reliable for meshes)
                var r = go.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    if (!has) { b = r.bounds; has = true; }
                    else b.Encapsulate(r.bounds);
                    continue;
                }
    
                // Fallback: collider bounds
                var c = go.GetComponentInChildren<Collider>();
                if (c != null)
                {
                    if (!has) { b = c.bounds; has = true; }
                    else b.Encapsulate(c.bounds);
                    continue;
                }
    
                // Final fallback: transform position
                if (!has)
                {
                    b = new Bounds(go.transform.position, Vector3.one);
                    has = true;
                }
                else
                {
                    b.Encapsulate(go.transform.position);
                }
            }
    
            return has;
        }
    
        private static Vector3 FindBestPlacement(Terrain terrain, Bounds cityBounds)
        {
            var td = terrain.terrainData;
            Vector3 tp = terrain.transform.position;
            Vector3 sz = td.size;
    
            float halfX = Mathf.Max(1f, cityBounds.extents.x);
            float halfZ = Mathf.Max(1f, cityBounds.extents.z);
    
            // Allowed placement rectangle inside terrain (with margins + city extents)
            float minX = tp.x + TerrainMargin + halfX;
            float maxX = tp.x + sz.x - TerrainMargin - halfX;
    
            float minZ = tp.z + TerrainMargin + halfZ;
            float maxZ = tp.z + sz.z - TerrainMargin - halfZ;
    
            // If city is bigger than terrain usable space, just clamp to center
            if (minX >= maxX || minZ >= maxZ)
            {
                float cx = tp.x + sz.x * 0.5f;
                float cz = tp.z + sz.z * 0.5f;
                float cy = terrain.SampleHeight(new Vector3(cx, 0, cz)) + tp.y;
                return new Vector3(cx, cy, cz);
            }
    
            Vector3 best = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            float bestScore = float.PositiveInfinity;
    
            for (int gz = 0; gz < ScanGrid; gz++)
            {
                float z01 = (ScanGrid == 1) ? 0.5f : (gz / (float)(ScanGrid - 1));
                float z = Mathf.Lerp(minZ, maxZ, z01);
    
                for (int gx = 0; gx < ScanGrid; gx++)
                {
                    float x01 = (ScanGrid == 1) ? 0.5f : (gx / (float)(ScanGrid - 1));
                    float x = Mathf.Lerp(minX, maxX, x01);
    
                    float rough = EstimateRoughness(terrain, new Vector3(x, 0f, z), RoughSampleRadius, RoughSamples);
                    if (rough < bestScore)
                    {
                        bestScore = rough;
                        best = new Vector3(x, 0f, z);
                    }
                }
            }
    
            best.y = terrain.SampleHeight(best) + tp.y;
            return best;
        }
    
        // Roughness = height variance in a small radius (lower is flatter)
        private static float EstimateRoughness(Terrain terrain, Vector3 center, float radius, int samples)
        {
            Vector3 tp = terrain.transform.position;
    
            float[] hs = new float[Mathf.Max(4, samples)];
            int idx = 0;
    
            // sample center + some around in a circle-ish pattern
            hs[idx++] = terrain.SampleHeight(center) + tp.y;
    
            for (int i = 1; i < hs.Length; i++)
            {
                float a = (i / (float)(hs.Length - 1)) * Mathf.PI * 2f;
                float r = radius * 0.85f;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                hs[idx++] = terrain.SampleHeight(p) + tp.y;
            }
    
            float mean = 0f;
            for (int i = 0; i < hs.Length; i++) mean += hs[i];
            mean /= hs.Length;
    
            float var = 0f;
            for (int i = 0; i < hs.Length; i++)
            {
                float d = hs[i] - mean;
                var += d * d;
            }
            var /= hs.Length;
    
            return var;
        }
    
        private static void SnapCityObjectsToTerrain(List<GameObject> objs, Terrain terrain)
        {
            Vector3 tp = terrain.transform.position;
    
            for (int i = 0; i < objs.Count; i++)
            {
                var go = objs[i];
                if (go == null) continue;
    
                string n = (go.name ?? "").ToLowerInvariant();
    
                // If it's our generated mesh roads/sidewalks, do NOT move them vertically
                if (IsGeneratedMeshRoad(go))
                    continue;
    
                bool isBuilding = n.Contains("building") || n.Contains("house") || n.Contains("tower") || n.Contains("block");
                bool isRoadish = n.Contains("road") || n.Contains("street") || n.Contains("avenue") || n.Contains("sidewalk");
    
                if (!isBuilding && !isRoadish) continue;
    
                Vector3 p = go.transform.position;
                float groundY = terrain.SampleHeight(p) + tp.y;
    
                Undo.RecordObject(go.transform, "Snap City To Terrain");
    
                if (isBuilding)
                {
                    float h = GetHeight(go);
                    go.transform.position = new Vector3(p.x, groundY + h * 0.5f, p.z);
                }
                else
                {
                    go.transform.position = new Vector3(p.x, groundY, p.z);
                }
            }
        }
    
        private static float GetHeight(GameObject go)
        {
            var r = go.GetComponentInChildren<Renderer>();
            if (r != null) return Mathf.Max(0.1f, r.bounds.size.y);
            return Mathf.Max(0.1f, go.transform.localScale.y);
        }
    }
}
