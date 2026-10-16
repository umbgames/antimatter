using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Antimatter
{
    /// <summary>
    /// Provides spatial-awareness capabilities to the AI agent.
    /// Allows querying terrain heights, normals, bounds, and slope data
    /// so the AI can make informed placement decisions before placing
    /// buildings, roads, or other objects.
    /// </summary>
    public static class TerrainQueryTool
    {
        /// <summary>
        /// Returns terrain bounds, resolution, and size.
        /// </summary>
        public static AgentToolResult ToolQueryTerrainInfo(JObject args)
        {
            var terrain = Object.FindObjectOfType<Terrain>();
            if (terrain == null || terrain.terrainData == null)
                return AgentToolResult.Fail("No terrain found in scene.");

            var td = terrain.terrainData;
            var tp = terrain.transform.position;

            var sb = new StringBuilder();
            sb.AppendLine("=== TERRAIN INFO ===");
            sb.AppendLine($"Position: ({tp.x:F1}, {tp.y:F1}, {tp.z:F1})");
            sb.AppendLine($"Size: ({td.size.x:F1}, {td.size.y:F1}, {td.size.z:F1})");
            sb.AppendLine($"Heightmap Resolution: {td.heightmapResolution}");
            sb.AppendLine($"World Bounds X: [{tp.x:F1} .. {tp.x + td.size.x:F1}]");
            sb.AppendLine($"World Bounds Z: [{tp.z:F1} .. {tp.z + td.size.z:F1}]");
            sb.AppendLine($"Max Height: {td.size.y:F1}");
            sb.AppendLine($"Terrain Layers: {(td.terrainLayers != null ? td.terrainLayers.Length : 0)}");

            return AgentToolResult.Ok(sb.ToString());
        }

        /// <summary>
        /// Samples height and slope at one or more world-space coordinates.
        /// Input: "points" array of {x, z} objects.
        /// Returns the terrain height (Y), surface normal, and slope angle at each point.
        /// </summary>
        public static AgentToolResult ToolSampleTerrain(JObject args)
        {
            var terrain = Object.FindObjectOfType<Terrain>();
            if (terrain == null || terrain.terrainData == null)
                return AgentToolResult.Fail("No terrain found in scene.");

            var pointsArr = args["points"] as JArray;
            if (pointsArr == null || pointsArr.Count == 0)
                return AgentToolResult.Fail("Provide a 'points' array with {x, z} objects.");

            if (pointsArr.Count > 64)
                return AgentToolResult.Fail("Max 64 sample points per call.");

            var td = terrain.terrainData;
            var tp = terrain.transform.position;
            var ts = td.size;

            var sb = new StringBuilder();
            sb.AppendLine("=== TERRAIN SAMPLES ===");

            foreach (var pt in pointsArr)
            {
                float wx = pt["x"]?.Value<float>() ?? 0f;
                float wz = pt["z"]?.Value<float>() ?? 0f;

                // Clamp to terrain bounds
                float cx = Mathf.Clamp(wx, tp.x, tp.x + ts.x);
                float cz = Mathf.Clamp(wz, tp.z, tp.z + ts.z);

                float height = terrain.SampleHeight(new Vector3(cx, 0f, cz)) + tp.y;

                // Normalized coords for normal lookup
                float nx = Mathf.InverseLerp(tp.x, tp.x + ts.x, cx);
                float nz = Mathf.InverseLerp(tp.z, tp.z + ts.z, cz);
                Vector3 normal = td.GetInterpolatedNormal(nx, nz);
                float slopeDeg = Vector3.Angle(Vector3.up, normal);

                bool inBounds = (wx >= tp.x && wx <= tp.x + ts.x && wz >= tp.z && wz <= tp.z + ts.z);

                sb.AppendLine($"  ({wx:F1}, {wz:F1}) => Y={height:F2}, slope={slopeDeg:F1}°, normal=({normal.x:F2},{normal.y:F2},{normal.z:F2}), inBounds={inBounds}");
            }

            return AgentToolResult.Ok(sb.ToString());
        }

        /// <summary>
        /// Returns a low-resolution grid of terrain heights for spatial overview.
        /// The AI can use this to understand the terrain shape before placing objects.
        /// </summary>
        public static AgentToolResult ToolTerrainHeightGrid(JObject args)
        {
            var terrain = Object.FindObjectOfType<Terrain>();
            if (terrain == null || terrain.terrainData == null)
                return AgentToolResult.Fail("No terrain found in scene.");

            int gridSize = args["gridSize"]?.Value<int>() ?? 8;
            gridSize = Mathf.Clamp(gridSize, 4, 16);

            var td = terrain.terrainData;
            var tp = terrain.transform.position;
            var ts = td.size;

            var sb = new StringBuilder();
            sb.AppendLine($"=== TERRAIN HEIGHT GRID ({gridSize}x{gridSize}) ===");
            sb.AppendLine($"World origin: ({tp.x:F1}, {tp.z:F1}), size: ({ts.x:F1}, {ts.z:F1})");
            sb.AppendLine($"Cell size: ({ts.x / gridSize:F1}, {ts.z / gridSize:F1})");
            sb.AppendLine();

            // Header row with Z coordinates
            sb.Append("       ");
            for (int gz = 0; gz < gridSize; gz++)
            {
                float wz = tp.z + (gz + 0.5f) * (ts.z / gridSize);
                sb.Append($"Z{wz,6:F0} ");
            }
            sb.AppendLine();

            for (int gx = 0; gx < gridSize; gx++)
            {
                float wx = tp.x + (gx + 0.5f) * (ts.x / gridSize);
                sb.Append($"X{wx,5:F0} ");

                for (int gz = 0; gz < gridSize; gz++)
                {
                    float wz = tp.z + (gz + 0.5f) * (ts.z / gridSize);
                    float h = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + tp.y;
                    sb.Append($"{h,7:F1} ");
                }
                sb.AppendLine();
            }

            return AgentToolResult.Ok(sb.ToString());
        }

        /// <summary>
        /// Scans the terrain to find the exact world coordinates for specific features like 'HighestPeak', 'LowestValley', or 'LargestFlatArea', or near an object.
        /// </summary>
        public static AgentToolResult ToolAnalyzeTerrainFeatures(JObject args)
        {
            var terrain = Object.FindObjectOfType<Terrain>();
            if (terrain == null || terrain.terrainData == null)
                return AgentToolResult.Fail("No terrain found in scene.");

            string feature = args["feature"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(feature)) return AgentToolResult.Fail("feature is required.");

            var td = terrain.terrainData;
            var tp = terrain.transform.position;
            var ts = td.size;

            if (feature == "NearObject")
            {
                string targetId = args["targetObjectId"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(targetId)) return AgentToolResult.Fail("targetObjectId is required for NearObject.");
                var target = EditorToolDispatcher.FindInScene(SceneManager.GetActiveScene(), targetId);
                if (target == null) return AgentToolResult.Fail($"Object '{targetId}' not found.");

                // Pick a spot slightly offset from the target
                float offset = 5f;
                Vector3 basePos = target.transform.position;
                Vector3 newPos = basePos + new Vector3(offset, 0, offset);
                float ty = terrain.SampleHeight(new Vector3(newPos.x, 0, newPos.z)) + tp.y;
                return AgentToolResult.Ok($"Feature NearObject ('{targetId}'): Location found at X={newPos.x:F2}, Y={ty:F2}, Z={newPos.z:F2}");
            }

            // Scan the terrain for peaks/valleys/flat
            int res = 64; // scan resolution
            Vector3 bestPos = Vector3.zero;
            float bestVal = feature == "LowestValley" ? float.MaxValue : float.MinValue;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / (res - 1);
                    float nz = (float)z / (res - 1);
                    float worldX = tp.x + nx * ts.x;
                    float worldZ = tp.z + nz * ts.z;
                    float h = td.GetInterpolatedHeight(nx, nz) + tp.y;

                    if (feature == "HighestPeak")
                    {
                        if (h > bestVal) { bestVal = h; bestPos = new Vector3(worldX, h, worldZ); }
                    }
                    else if (feature == "LowestValley")
                    {
                        if (h < bestVal) { bestVal = h; bestPos = new Vector3(worldX, h, worldZ); }
                    }
                    else if (feature == "LargestFlatArea")
                    {
                        // To find flat area, we look for lowest steepness
                        float steepness = td.GetSteepness(nx, nz);
                        // We want lowest steepness, so we invert it to maximize
                        float flatScore = -steepness;
                        if (flatScore > bestVal) { bestVal = flatScore; bestPos = new Vector3(worldX, h, worldZ); }
                    }
                }
            }

            if (bestPos == Vector3.zero && feature != "LargestFlatArea")
                return AgentToolResult.Fail($"Could not find feature: {feature}");

            return AgentToolResult.Ok($"Feature '{feature}': Location found at X={bestPos.x:F2}, Y={bestPos.y:F2}, Z={bestPos.z:F2}");
        }
    }
}
