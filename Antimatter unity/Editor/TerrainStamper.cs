using System.Collections.Generic;
using UnityEngine;

namespace Antimatter
{
    
    public static class TerrainStamper
    {
        /// <summary>
        /// Stamps a path corridor into the terrain.
        /// This version matches RoadMeshBuilder by stamping to (pathY + yOffset) - cutDepthMeters.
        ///
        /// NOTE: yOffset is optional so old call sites still compile.
        /// </summary>
        public static void StampPath(
            Terrain terrain,
            List<Vector3> path,
            float halfWidth,
            float falloff,
            float blend,
            float cutDepthMeters,
            float yOffset = 0f,
            float sampleStepMeters = 2.5f)
        {
            if (terrain == null || terrain.terrainData == null) return;
            if (path == null || path.Count < 2) return;
    
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            var heights = td.GetHeights(0, 0, res, res);
    
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = td.size;
    
            halfWidth = Mathf.Max(0.01f, halfWidth);
            cutDepthMeters = Mathf.Max(0f, cutDepthMeters);
            falloff = Mathf.Max(0.01f, falloff);
            blend = Mathf.Clamp01(blend);
            float step = Mathf.Max(0.5f, sampleStepMeters);
    
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i];
                Vector3 b = path[i + 1];
    
                float segLen = Vector3.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / step));
    
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vector3 p = Vector3.Lerp(a, b, t);
    
                    // IMPORTANT: Match your road mesh generator:
                    // Road surface is roughly pathY + yOffset. Then optionally cut down.
                    float targetWorldY = (p.y + yOffset) - cutDepthMeters;
    
                    float targetN = (targetWorldY - tPos.y) / Mathf.Max(0.001f, tSize.y);
                    targetN = Mathf.Clamp01(targetN);
    
                    PaintCircle(
                        heights, res,
                        center: p,
                        tPos: tPos,
                        tSize: tSize,
                        radius: halfWidth,
                        falloff: falloff,
                        target: targetN,
                        blend: blend);
                }
            }
    
            td.SetHeights(0, 0, heights);
            terrain.Flush();
        }
    
        /// <summary>
        /// Old signature.
        /// Stamps a pad to the CURRENT terrain height at center (flatten-ish).
        /// </summary>
        public static void StampPad(
            Terrain terrain,
            Vector3 center,
            float radius,
            float falloff,
            float blend)
        {
            if (terrain == null || terrain.terrainData == null) return;
    
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            var heights = td.GetHeights(0, 0, res, res);
    
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = td.size;
    
            float groundY = terrain.SampleHeight(center) + tPos.y;
            float targetN = (groundY - tPos.y) / Mathf.Max(0.001f, tSize.y);
            targetN = Mathf.Clamp01(targetN);
    
            PaintCircle(
                heights, res,
                center: center,
                tPos: tPos,
                tSize: tSize,
                radius: radius,
                falloff: falloff,
                target: targetN,
                blend: Mathf.Clamp01(blend));
    
            td.SetHeights(0, 0, heights);
            terrain.Flush();
        }
    
        /// <summary>
        /// Explicit pad stamping to a specific WORLD Y.
        /// Useful if you want pads aligned to something other than current terrain.
        /// </summary>
        public static void StampPad(
            Terrain terrain,
            Vector3 center,
            float radius,
            float falloff,
            float blend,
            float targetWorldY)
        {
            if (terrain == null || terrain.terrainData == null) return;
    
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            var heights = td.GetHeights(0, 0, res, res);
    
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = td.size;
    
            float targetN = (targetWorldY - tPos.y) / Mathf.Max(0.001f, tSize.y);
            targetN = Mathf.Clamp01(targetN);
    
            PaintCircle(
                heights, res,
                center: center,
                tPos: tPos,
                tSize: tSize,
                radius: radius,
                falloff: falloff,
                target: targetN,
                blend: Mathf.Clamp01(blend));
    
            td.SetHeights(0, 0, heights);
            terrain.Flush();
        }
    
        /// <summary>
        /// Convenience: explicit name for "stamp to terrain at center".
        /// </summary>
        public static void StampPadToTerrain(
            Terrain terrain,
            Vector3 center,
            float radius,
            float falloff,
            float blend)
        {
            StampPad(terrain, center, radius, falloff, blend);
        }
    
        // =====================================================================
        // ✅ NEW: Flatten a whole rectangular world-space footprint (city area)
        // =====================================================================
    
        /// <summary>
        /// Flattens terrain under a world-space bounds rectangle to a targetWorldY,
        /// with a soft falloff region around the rectangle edges.
        ///
        /// This is the "flatten the entire city footprint" function.
        /// </summary>
        public static void StampRectPadWorld(
            Terrain terrain,
            Bounds worldBounds,
            float targetWorldY,
            float falloff,
            float blend)
        {
            if (terrain == null || terrain.terrainData == null) return;
    
            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            var heights = td.GetHeights(0, 0, res, res);
    
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = td.size;
    
            blend = Mathf.Clamp01(blend);
            falloff = Mathf.Max(0.01f, falloff);
    
            float targetN = (targetWorldY - tPos.y) / Mathf.Max(0.001f, tSize.y);
            targetN = Mathf.Clamp01(targetN);
    
            // Rectangle in world
            float innerXMin = worldBounds.min.x;
            float innerXMax = worldBounds.max.x;
            float innerZMin = worldBounds.min.z;
            float innerZMax = worldBounds.max.z;
    
            // Expand by falloff to blend edges
            float xMin = innerXMin - falloff;
            float xMax = innerXMax + falloff;
            float zMin = innerZMin - falloff;
            float zMax = innerZMax + falloff;
    
            // Convert to heightmap indices
            int x0 = Mathf.Clamp(WorldToHx(xMin, tPos.x, tSize.x, res), 0, res - 1);
            int x1 = Mathf.Clamp(WorldToHx(xMax, tPos.x, tSize.x, res), 0, res - 1);
            int z0 = Mathf.Clamp(WorldToHz(zMin, tPos.z, tSize.z, res), 0, res - 1);
            int z1 = Mathf.Clamp(WorldToHz(zMax, tPos.z, tSize.z, res), 0, res - 1);
    
            for (int z = z0; z <= z1; z++)
            {
                float wz = Mathf.Lerp(tPos.z, tPos.z + tSize.z, z / (float)(res - 1));
    
                for (int x = x0; x <= x1; x++)
                {
                    float wx = Mathf.Lerp(tPos.x, tPos.x + tSize.x, x / (float)(res - 1));
    
                    float w = RectFalloffWeight(wx, wz, innerXMin, innerXMax, innerZMin, innerZMax, falloff);
                    if (w <= 0f) continue;
    
                    float cur = heights[z, x];
                    heights[z, x] = Mathf.Lerp(cur, targetN, w * blend);
                }
            }
    
            td.SetHeights(0, 0, heights);
            terrain.Flush();
        }
    
        private static float RectFalloffWeight(
            float x, float z,
            float innerXMin, float innerXMax,
            float innerZMin, float innerZMax,
            float falloff)
        {
            // inside inner rectangle => full influence
            if (x >= innerXMin && x <= innerXMax && z >= innerZMin && z <= innerZMax)
                return 1f;
    
            // outside => fade based on distance to nearest edge
            float dx = 0f;
            if (x < innerXMin) dx = innerXMin - x;
            else if (x > innerXMax) dx = x - innerXMax;
    
            float dz = 0f;
            if (z < innerZMin) dz = innerZMin - z;
            else if (z > innerZMax) dz = z - innerZMax;
    
            float d = Mathf.Max(dx, dz); // square-ish falloff
            if (d >= falloff) return 0f;
    
            return 1f - (d / Mathf.Max(0.0001f, falloff));
        }
    
        // =====================================================================
        // Internal circle painter
        // =====================================================================
    
        private static void PaintCircle(
            float[,] h,
            int res,
            Vector3 center,
            Vector3 tPos,
            Vector3 tSize,
            float radius,
            float falloff,
            float target,
            float blend)
        {
            float r = Mathf.Max(0.01f, radius);
            float f = Mathf.Clamp(falloff, 0.01f, r);
    
            int cx = WorldToHx(center.x, tPos.x, tSize.x, res);
            int cz = WorldToHz(center.z, tPos.z, tSize.z, res);
    
            int radHx = Mathf.CeilToInt((r / tSize.x) * (res - 1));
            int radHz = Mathf.CeilToInt((r / tSize.z) * (res - 1));
    
            int x0 = Mathf.Clamp(cx - radHx, 0, res - 1);
            int x1 = Mathf.Clamp(cx + radHx, 0, res - 1);
            int z0 = Mathf.Clamp(cz - radHz, 0, res - 1);
            int z1 = Mathf.Clamp(cz + radHz, 0, res - 1);
    
            Vector2 c2 = new Vector2(center.x, center.z);
    
            for (int z = z0; z <= z1; z++)
            {
                float wz = Mathf.Lerp(tPos.z, tPos.z + tSize.z, z / (float)(res - 1));
    
                for (int x = x0; x <= x1; x++)
                {
                    float wx = Mathf.Lerp(tPos.x, tPos.x + tSize.x, x / (float)(res - 1));
    
                    float dist = Vector2.Distance(new Vector2(wx, wz), c2);
                    if (dist > r) continue;
    
                    float inner = Mathf.Max(0.0001f, r - f);
                    float w = (dist <= inner) ? 1f : Mathf.InverseLerp(r, inner, dist);
                    w = Mathf.Clamp01(w);
    
                    // Single blend (no double-lerp mush)
                    float cur = h[z, x];
                    h[z, x] = Mathf.Lerp(cur, target, w * blend);
                }
            }
        }
    
        private static int WorldToHx(float x, float tx, float sx, int res) =>
            Mathf.RoundToInt(((x - tx) / sx) * (res - 1));
    
        private static int WorldToHz(float z, float tz, float sz, int res) =>
            Mathf.RoundToInt(((z - tz) / sz) * (res - 1));
    }
}
