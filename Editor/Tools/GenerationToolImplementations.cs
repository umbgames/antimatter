using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    internal static class GenerationToolImplementations
    {
        internal static AgentToolResult GenerateTerrain(JObject args)
        {
            string presetStr = args["preset"]?.Value<string>() ?? "Hills";
            int seed = args["seed"]?.Value<int>() ?? UnityEngine.Random.Range(0, int.MaxValue);

            TerrainGenerator.TerrainPreset preset = presetStr switch
            {
                "Desert" => TerrainGenerator.TerrainPreset.Desert,
                "Plains" => TerrainGenerator.TerrainPreset.Plains,
                "Hills" => TerrainGenerator.TerrainPreset.Hills,
                "Mountains" => TerrainGenerator.TerrainPreset.Mountains,
                "MountainValley" => TerrainGenerator.TerrainPreset.MountainValley,
                "Island" => TerrainGenerator.TerrainPreset.Island,
                _ => TerrainGenerator.TerrainPreset.Hills
            };

            // Build SchemaTerrainParams from the args for AI-controlled generation
            var schema = new SchemaTerrainParams
            {
                seed = seed,
                sizeXZ = args["sizeXZ"]?.Value<int>() ?? 0,
                heightY = args["heightY"]?.Value<int>() ?? 0,
                gridSizeX = args["gridSizeX"]?.Value<int>() ?? 0,
                gridSizeZ = args["gridSizeZ"]?.Value<int>() ?? 0,
                heightmapResolution = args["heightmapResolution"]?.Value<int>() ?? 0,
                octaves = args["octaves"]?.Value<int>() ?? 0,
                lacunarity = args["lacunarity"]?.Value<float>() ?? 0f,
                persistence = args["persistence"]?.Value<float>() ?? 0f,
                baseFrequency = args["baseFrequency"]?.Value<float>() ?? 0f,
                ridgeStrength = args["ridgeStrength"]?.Value<float>() ?? 0f,
                erosionPasses = args["erosionPasses"]?.Value<float>() ?? 0f,
                seaLevel = args["seaLevel"]?.Value<float>() ?? 0f,
                valleyWidth = args["valleyWidth"]?.Value<float>() ?? 0f,
                valleyDepth = args["valleyDepth"]?.Value<float>() ?? 0f,
                biome = args["biome"]?.Value<string>()
            };

            string prompt = args["prompt"]?.Value<string>() ?? "";
            if (prompt.ToLowerInvariant().Contains("massive") || prompt.ToLowerInvariant().Contains("huge") || prompt.ToLowerInvariant().Contains("large") || prompt.ToLowerInvariant().Contains("tiling"))
            {
                if (schema.gridSizeX == 0) schema.gridSizeX = 2;
                if (schema.gridSizeZ == 0) schema.gridSizeZ = 2;
            }

            var scene = SceneManager.GetActiveScene();
            var go = TerrainGenerator.CreateOrReplaceTerrain("GeneratedTerrain", schema, presetStr, Vector3.zero);
            SceneManager.MoveGameObjectToScene(go, scene);

            // Auto-select & frame in Scene View so the user can see it
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            var t = go.GetComponent<Terrain>();
            string sizeInfo = t != null ? $" Size=({t.terrainData.size.x:F0},{t.terrainData.size.y:F0},{t.terrainData.size.z:F0})" : "";
            return AgentToolResult.Ok($"Generated terrain with preset={presetStr} seed={seed}.{sizeInfo} Terrain is now selected and framed in Scene View.");
        }

        internal static AgentToolResult GenerateRoadNetwork(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();

            // Auto-create terrain if none exists
            if (terrain == null)
            {
                var terrGo = TerrainGenerator.CreateOrReplaceTerrain("GeneratedTerrain", TerrainGenerator.TerrainPreset.Plains, UnityEngine.Random.Range(0, int.MaxValue), Vector3.zero);
                SceneManager.MoveGameObjectToScene(terrGo, scene);
                terrain = terrGo.GetComponent<Terrain>();
                if (terrain == null) return AgentToolResult.Fail("Failed to auto-create terrain for roads.");
            }

            var hubsToken = args["hubs"] as JArray;
            List<Vector3> explicitHubs = null;
            
            if (hubsToken != null && hubsToken.Count > 0)
            {
                explicitHubs = new List<Vector3>();
                foreach(var h in hubsToken)
                {
                    explicitHubs.Add(new Vector3(h["x"].Value<float>(), h["y"].Value<float>(), h["z"].Value<float>()));
                }
            }

            string prompt = args["prompt"]?.Value<string>() ?? "";
            
            var pSettings = new RoadGenSettings();
            try
            {
                RoadNetworkPlanner.GenerateRoadSystem(scene, terrain, pSettings, explicitHubs, RoadMeshBuilder.GetOrCreateDefaultRoadMaterial(), false, RoadMeshBuilder.GetOrCreateDefaultMarkingMaterial());
                return AgentToolResult.Ok($"Generated road network with {explicitHubs.Count} hubs.");
            }
            catch(Exception e)
            {
                 return AgentToolResult.Fail($"Road generation failed: {e.Message}");
            }
        }

        internal static AgentToolResult GenerateSettlements(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (terrain == null) return AgentToolResult.Fail("No Terrain in scene for settlements.");

            var setToken = args["settlements"] as JArray;
            List<SettlementNode> explicitSets = null;

            if (setToken != null && setToken.Count > 0)
            {
                explicitSets = new List<SettlementNode>();
                foreach(var s in setToken)
                {
                    explicitSets.Add(new SettlementNode {
                        center = new Vector3(s["x"].Value<float>(), s["y"].Value<float>(), s["z"].Value<float>()),
                        radius = s["radius"]?.Value<float>() ?? 0f,
                        buildingCount = s["buildingCount"]?.Value<int>() ?? 0,
                        style = s["style"]?.Value<string>() ?? "Auto"
                    });
                }
            }

            string prompt = args["prompt"]?.Value<string>() ?? "";
            var setSettings = new SettlementGenSettings();

            try
            {
                var noRoads = new RoadNetworkPlanner.RoadNetworkResult();
                SettlementNetworkPlanner.GenerateSettlements(scene, terrain, noRoads, setSettings, explicitSets, prompt, null, null);
                return AgentToolResult.Ok($"Generated {explicitSets.Count} settlements.");
            }
            catch(Exception e)
            {
                  return AgentToolResult.Fail($"Settlement generation failed: {e.Message}");
            }
        }

        internal static AgentToolResult GenerateWorld(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return AgentToolResult.Fail("No active scene.");

            string prompt = args["prompt"]?.Value<string>() ?? "";
            var sb = new System.Text.StringBuilder();

            // ── 1. Terrain ──
            string presetStr = args["terrainPreset"]?.Value<string>() ?? "Hills";
            int terrainSeed = args["terrainSeed"]?.Value<int>() ?? UnityEngine.Random.Range(0, int.MaxValue);

            var schema = new SchemaTerrainParams
            {
                seed = terrainSeed,
                sizeXZ = args["sizeXZ"]?.Value<int>() ?? 0,
                heightY = args["heightY"]?.Value<int>() ?? 0,
                gridSizeX = args["gridSizeX"]?.Value<int>() ?? 0,
                gridSizeZ = args["gridSizeZ"]?.Value<int>() ?? 0,
                octaves = args["octaves"]?.Value<int>() ?? 0,
                lacunarity = args["lacunarity"]?.Value<float>() ?? 0f,
                persistence = args["persistence"]?.Value<float>() ?? 0f,
                baseFrequency = args["baseFrequency"]?.Value<float>() ?? 0f,
                ridgeStrength = args["ridgeStrength"]?.Value<float>() ?? 0f,
                erosionPasses = args["erosionPasses"]?.Value<float>() ?? 0f,
                biome = args["biome"]?.Value<string>()
            };

            // Detect if the user wants a massive world based on prompt or explicit args
            if (prompt.ToLowerInvariant().Contains("massive") || prompt.ToLowerInvariant().Contains("huge") || prompt.ToLowerInvariant().Contains("large") || prompt.ToLowerInvariant().Contains("tiling"))
            {
                if (schema.gridSizeX == 0) schema.gridSizeX = 2;
                if (schema.gridSizeZ == 0) schema.gridSizeZ = 2;
                sb.AppendLine($"Detected 'massive'/'huge'/'large' prompt or explicit tiling: Generating a {Mathf.Max(2, schema.gridSizeX)}x{Mathf.Max(2, schema.gridSizeZ)} multi-terrain grid.");
            }

            var terrainGo = TerrainGenerator.CreateOrReplaceTerrain("GeneratedTerrain", schema, presetStr, Vector3.zero);
            SceneManager.MoveGameObjectToScene(terrainGo, scene);
            var terrain = terrainGo.GetComponent<Terrain>();
            if (terrain == null) terrain = terrainGo.GetComponentInChildren<Terrain>();
            if (terrain == null) return AgentToolResult.Fail("Failed to generate terrain component.");
            
            sb.AppendLine($"Terrain generated: preset={presetStr}, seed={terrainSeed}, size=({terrain.terrainData.size.x:F0},{terrain.terrainData.size.y:F0},{terrain.terrainData.size.z:F0})");

            // ── 2. Roads ──
            RoadNetworkPlanner.RoadNetworkResult roadsResult = null;
            var hubsToken = args["roadHubs"] as JArray;
            if (hubsToken != null && hubsToken.Count >= 2)
            {
                var hubs = new List<Vector3>();
                foreach (var h in hubsToken)
                    hubs.Add(new Vector3(h["x"].Value<float>(), h["y"].Value<float>(), h["z"].Value<float>()));

                var roadSettings = new RoadGenSettings { seed = terrainSeed };
                try
                {
                    RoadNetworkPlanner.GenerateRoadSystem(scene, terrain, roadSettings, hubs, RoadMeshBuilder.GetOrCreateDefaultRoadMaterial(), false, RoadMeshBuilder.GetOrCreateDefaultMarkingMaterial());
                    roadsResult = RoadNetworkPlanner.LastResult;
                    sb.AppendLine($"Roads generated: {hubs.Count} hubs");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Roads failed: {ex.Message}");
                }
            }
            else
            {
                // Fallback: generate automatic road network if not provided by AI
                var roadSettings = new RoadGenSettings { seed = terrainSeed };
                try
                {
                    RoadNetworkPlanner.GenerateRoadSystem(scene, terrain, roadSettings, null, RoadMeshBuilder.GetOrCreateDefaultRoadMaterial(), false, RoadMeshBuilder.GetOrCreateDefaultMarkingMaterial());
                    roadsResult = RoadNetworkPlanner.LastResult;
                    sb.AppendLine($"Roads generated (automatic): {roadSettings.hubCount} hubs");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Roads failed (automatic): {ex.Message}");
                }
            }

            // ── 3. Settlements ──
            var setToken = args["settlements"] as JArray;
            if (setToken != null && setToken.Count > 0 && roadsResult != null)
            {
                var explicitSets = new List<SettlementNode>();
                
                float dynamicRadius = terrain.terrainData.size.x * 0.12f; // e.g. 12% of terrain width
                float dynamicBuildings = (terrain.terrainData.size.x * terrain.terrainData.size.z) / 4000f;

                foreach (var s in setToken)
                {
                    explicitSets.Add(new SettlementNode
                    {
                        center = new Vector3(s["x"].Value<float>(), s["y"].Value<float>(), s["z"].Value<float>()),
                        radius = s["radius"]?.Value<float>() > 0 ? s["radius"].Value<float>() : dynamicRadius,
                        buildingCount = s["buildingCount"]?.Value<int>() > 0 ? s["buildingCount"].Value<int>() : (int)dynamicBuildings,
                        style = s["style"]?.Value<string>() ?? "Auto"
                    });
                }

                var setSettings = new SettlementGenSettings 
                { 
                    seed = terrainSeed,
                    maxSlopeDeg = 55f // Brute force slope tolerance
                };
                try
                {
                    SettlementNetworkPlanner.GenerateSettlements(scene, terrain, roadsResult, setSettings, explicitSets, prompt, null, null);
                    sb.AppendLine($"Settlements generated: {explicitSets.Count}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Settlements failed: {ex.Message}");
                }
            }

            // Snap objects to terrain
            TerrainSnapper.SnapSceneObjectsToTerrain(scene, terrain);

            // ── 4. NavMesh Baking ──
            try
            {
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
                sb.AppendLine("NavMesh baked successfully for agents.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"NavMesh bake failed: {ex.Message}");
            }

            // Ensure camera and light
            SceneDefaults.EnsureCameraAndLightIfNeeded(scene, true);

            // Auto-select & frame terrain
            Selection.activeGameObject = terrainGo;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            return AgentToolResult.Ok(sb.ToString());
        }

        internal static AgentToolResult RunFullPlan(JObject args)
        {
            return AgentToolResult.Fail("The 'runFullPlan' tool is deprecated for the interactive agent. Please use other tools like generateWorld, createObject, generateTerrain, etc., to build the scene step-by-step.");
        }

        internal static AgentToolResult PlaceNature(JObject args)
        {
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (terrain == null) return AgentToolResult.Fail("No Terrain found in the active scene.");

            int seed = args["seed"]?.Value<int>() ?? UnityEngine.Random.Range(0, int.MaxValue);
            TerrainGenerator.PlaceNatureOnAllTerrains(seed);
            
            return AgentToolResult.Ok("Nature and foliage scattered successfully across the terrain.");
        }

        internal static float Hash01ForCity(int index, int salt)
        {
            unchecked
            {
                int h = unchecked((int)2166136261);
                h = (h ^ index) * 16777619;
                h = (h ^ salt) * 16777619;
                int v = h & 0x00FFFFFF;
                return v / 16777216f;
            }
        }
    }
}
