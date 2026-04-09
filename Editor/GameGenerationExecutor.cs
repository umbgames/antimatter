// Assets/Editor/GameGenerationExecutor.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace Antimatter
{
    
    public static class GameGenerationExecutor
    {
        // ── Named constants (were magic numbers) ──
        private const float TempTerrainSizeXZ = 400f;
        private const float TempTerrainHeightY = 20f;
        private const int TempTerrainResolution = 257;
        private const int TerrainPixelError = 5;
    
        public static void ExecuteCreate(string json)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("Stop Play Mode before generating.");
    
            ValidateOnly(json);
    
            SessionState.SetString(AiPrefsKeys.SessionJsonCreate, json);
            SessionState.SetBool(AiPrefsKeys.SessionPendingCreate, true);
    
            var gen = JsonUtility.FromJson<GameGeneration>(json);
    
            // scripts first (may compile/reload)
            ScriptGenerator.GenerateScripts(gen);
    
            ScheduleCreateInternal();
        }
    
        public static void ExecuteEdit(string json)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("Stop Play Mode before generating.");
    
            ValidateOnly(json);
    
            SessionState.SetString(AiPrefsKeys.SessionJsonEdit, json);
            SessionState.SetBool(AiPrefsKeys.SessionPendingEdit, true);
    
            var gen = JsonUtility.FromJson<GameGeneration>(json);
    
            ScriptGenerator.GenerateScripts(gen);
    
            ScheduleEditInternal();
        }
    
        public static void ValidateOnly(string json)
        {
            var gen = JsonUtility.FromJson<GameGeneration>(json);
            bool levelGen = SessionState.GetBool(AiPrefsKeys.SessionLevelGen, false);
            ValidatePlan(gen, LoadConstraints(), levelGen);
        }
    
        public static void ResumeIfPending()
        {
            if (SessionState.GetBool(AiPrefsKeys.SessionPendingCreate, false))
                ScheduleCreateInternal();
    
            if (SessionState.GetBool(AiPrefsKeys.SessionPendingEdit, false))
                ScheduleEditInternal();
        }
    
        private static void ScheduleCreateInternal()
        {
            EditorApplication.update -= UpdateCreateLoop;
            EditorApplication.update += UpdateCreateLoop;
    
            EditorApplication.delayCall -= TryCreateIfReady;
            EditorApplication.delayCall += TryCreateIfReady;
        }
    
        private static void ScheduleEditInternal()
        {
            EditorApplication.update -= UpdateEditLoop;
            EditorApplication.update += UpdateEditLoop;
    
            EditorApplication.delayCall -= TryEditIfReady;
            EditorApplication.delayCall += TryEditIfReady;
        }
    
        private static void UpdateCreateLoop()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= UpdateCreateLoop;
            TryCreateIfReady();
        }
    
        private static void UpdateEditLoop()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= UpdateEditLoop;
            TryEditIfReady();
        }
    
        private static void TryCreateIfReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleCreateInternal();
                return;
            }
    
            if (!SessionState.GetBool(AiPrefsKeys.SessionPendingCreate, false))
                return;
    
            var json = SessionState.GetString(AiPrefsKeys.SessionJsonCreate, null);
            if (string.IsNullOrEmpty(json))
            {
                SessionState.SetBool(AiPrefsKeys.SessionPendingCreate, false);
                return;
            }
    
            var constraints = LoadConstraints();
            var userPrompt = SessionState.GetString(AiPrefsKeys.SessionUserPrompt, "");
            bool levelGen = SessionState.GetBool(AiPrefsKeys.SessionLevelGen, false);
    
            var gen = JsonUtility.FromJson<GameGeneration>(json);
            ValidatePlan(gen, constraints, levelGen);
    
            var matMap = constraints.forbidMaterials ? new Dictionary<string, Material>() : PlanAssetBuilder.BuildMaterials(gen);
            var terrainMap = constraints.forbidTerrain ? new Dictionary<string, TerrainData>() : PlanAssetBuilder.BuildTerrainData(gen);
    
            MonoScriptResolver.RebuildIndex();
    
            SessionState.SetBool(AiPrefsKeys.SessionPendingCreate, false);
    
            using (new UndoGroupScope("AI GameGen (Create)"))
            {
                BuildAllScenesCreate(gen, constraints, userPrompt, matMap, terrainMap, levelGen);
            }
    
            AssetDatabase.SaveAssets();
            Debug.Log("AI Game Generator: Create build complete.");
        }
    
        private static void TryEditIfReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleEditInternal();
                return;
            }
    
            if (!SessionState.GetBool(AiPrefsKeys.SessionPendingEdit, false))
                return;
    
            var json = SessionState.GetString(AiPrefsKeys.SessionJsonEdit, null);
            if (string.IsNullOrEmpty(json))
            {
                SessionState.SetBool(AiPrefsKeys.SessionPendingEdit, false);
                return;
            }
    
            var constraints = LoadConstraints();
            var userPrompt = SessionState.GetString(AiPrefsKeys.SessionUserPrompt, "");
            bool levelGen = SessionState.GetBool(AiPrefsKeys.SessionLevelGen, false);
    
            var gen = JsonUtility.FromJson<GameGeneration>(json);
            ValidatePlan(gen, constraints, levelGen);
    
            var matMap = constraints.forbidMaterials ? new Dictionary<string, Material>() : PlanAssetBuilder.BuildMaterials(gen);
            var terrainMap = constraints.forbidTerrain ? new Dictionary<string, TerrainData>() : PlanAssetBuilder.BuildTerrainData(gen);
    
            MonoScriptResolver.RebuildIndex();
    
            SessionState.SetBool(AiPrefsKeys.SessionPendingEdit, false);
    
            using (new UndoGroupScope("AI GameGen (Edit)"))
            {
                ApplyEditToActiveScene(gen, constraints, userPrompt, matMap, terrainMap, levelGen);
            }
    
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("AI Game Generator: Edit apply complete (no wipe).");
        }
    
        // ---------------- WORLDGEN (PLAN-DRIVEN) ----------------
    
        private static WorldGenSettings ResolveWorldGen(GameGeneration gen)
        {
            if (gen != null && gen.settings != null && gen.settings.worldGen != null)
                return gen.settings.worldGen;
    
            return new WorldGenSettings
            {
                enabled = false,
                terrainPreset = "Hills",
                roadHubs = new List<Vector3>(),
                settlements = new List<SettlementNode>()
            };
        }
    
        private static TerrainGenerator.TerrainPreset MapTerrainPreset(string schemaPreset)
        {
            string p = (schemaPreset ?? "").Trim();
    
            switch (p)
            {
                case "Plains": return TerrainGenerator.TerrainPreset.Plains;
                case "Hills": return TerrainGenerator.TerrainPreset.Hills;
                case "Mountains": return TerrainGenerator.TerrainPreset.Mountains;
                case "Valley":
                case "Canyon": return TerrainGenerator.TerrainPreset.MountainValley;
                case "Island": return TerrainGenerator.TerrainPreset.Island;
                case "Plateau":
                default:
                    return TerrainGenerator.TerrainPreset.Plains;
            }
        }
    
        // ---------------- CREATE ----------------
    
        private static void BuildAllScenesCreate(
            GameGeneration gen,
            GenerationConstraints constraints,
            string userPrompt,
            Dictionary<string, Material> matMap,
            Dictionary<string, TerrainData> terrainMap,
            bool levelGen)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
    
            var wg = ResolveWorldGen(gen);
    
            foreach (var sceneDef in gen.scenes)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                scene.name = sceneDef.name;
                SceneManager.SetActiveScene(scene);
    
                EnsureScriptBackedObjects(sceneDef, gen, userPrompt);
    
                BuildSceneObjects(scene, sceneDef, constraints, matMap, terrainMap, isEdit: false);
    
                // 1) Terrain only if plan enabled it (user asked terrain)
                MaybeGenerateOrShapeTerrain(scene, constraints, userPrompt, wg);
    
                // 2) Roads/Settlements (roads can run even if terrain is not kept)
                var roadsResult = RunPostTerrainWorldPass(scene, constraints, userPrompt, wg, isEdit: false);
    
                // 3) FPS LevelGen marker-driven layout (if enabled)
                if (levelGen)
                {
                    int seed = StablePromptSeed(userPrompt);
                    try
                    {
                        LevelLayoutGenerator.BuildFromMarkers(scene, seed, clearPreviousLayout: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("AI Game Generator: LevelLayoutGenerator failed: " + ex.Message);
                    }
    
                    // 4) Racing pass ALSO uses roads (safe to run even if FPS was requested; it no-ops if no roads)
                    // If you prefer mutual-exclusion, add a separate SessionRacingGen flag and gate this.
                    try
                    {
                        int seed2 = StablePromptSeed(userPrompt + "|racing");
                        RacingRoadTrackPass.Apply(scene, seed2, roadsResult, clearPrevious: true);
                    }
                    catch
                    {
                        // quiet: racing pass should not break FPS
                    }
                }
    
                bool ensure = ResolveEnsureCameraLight(gen, sceneDef, constraints);
                SceneDefaults.EnsureCameraAndLightIfNeeded(scene, ensure);
    
                EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{sceneDef.name}.unity");
            }
        }
    
        // ---------------- EDIT ----------------
    
        private static void ApplyEditToActiveScene(
            GameGeneration gen,
            GenerationConstraints constraints,
            string userPrompt,
            Dictionary<string, Material> matMap,
            Dictionary<string, TerrainData> terrainMap,
            bool levelGen)
        {
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid())
                throw new Exception("No active scene is open.");
    
            SceneDefinition target = null;
            foreach (var s in gen.scenes)
            {
                if (s.name == active.name) { target = s; break; }
            }
            target ??= gen.scenes[0];
    
            var wg = ResolveWorldGen(gen);
    
            bool wantsWorldRebuild = PromptWantsWorldRebuild(userPrompt) || (wg != null && wg.enabled);
    
            EnsureScriptBackedObjects(target, gen, userPrompt);
    
            if (!wantsWorldRebuild)
            {
                if (PromptWantsSpawn(userPrompt))
                {
                    ApplyNonDestructiveEditsCreateMissing(active, target, constraints, matMap, terrainMap, isEdit: true);
                }
                else
                {
                    ApplyNonDestructiveEditsOnly(active, target, constraints, matMap);
                }
    
                var terrain = FindTerrainInScene(active);
                if (terrain != null)
                    TerrainSnapper.SnapBuildingsOnly(active, terrain);
    
                // Roads/track can still be applied without keeping terrain
                var roadsResult = RunPostTerrainWorldPass(active, constraints, userPrompt, wg, isEdit: true);
    
                if (levelGen)
                {
                    int seed = StablePromptSeed(userPrompt);
                    try
                    {
                        LevelLayoutGenerator.BuildFromMarkers(active, seed, clearPreviousLayout: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("AI Game Generator: LevelLayoutGenerator failed: " + ex.Message);
                    }
    
                    // Racing post-pass (safe)
                    try
                    {
                        int seed2 = StablePromptSeed(userPrompt + "|racing");
                        RacingRoadTrackPass.Apply(active, seed2, roadsResult, clearPrevious: true);
                    }
                    catch
                    {
                        // quiet
                    }
                }
    
                bool ensure = ResolveEnsureCameraLight(gen, target, constraints);
                SceneDefaults.EnsureCameraAndLightIfNeeded(active, ensure);
                return;
            }
    
            // Destructive edit
            BuildSceneObjects(active, target, constraints, matMap, terrainMap, isEdit: true);
    
            MaybeGenerateOrShapeTerrain(active, constraints, userPrompt, wg);
    
            var roadsResult2 = RunPostTerrainWorldPass(active, constraints, userPrompt, wg, isEdit: true);
    
            if (levelGen)
            {
                int seed = StablePromptSeed(userPrompt);
                try
                {
                    LevelLayoutGenerator.BuildFromMarkers(active, seed, clearPreviousLayout: true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("AI Game Generator: LevelLayoutGenerator failed: " + ex.Message);
                }
    
                try
                {
                    int seed2 = StablePromptSeed(userPrompt + "|racing");
                    RacingRoadTrackPass.Apply(active, seed2, roadsResult2, clearPrevious: true);
                }
                catch
                {
                    // quiet
                }
            }
    
            bool ensure2 = ResolveEnsureCameraLight(gen, target, constraints);
            SceneDefaults.EnsureCameraAndLightIfNeeded(active, ensure2);
        }
    
        // ---------------- POST TERRAIN WORLD PASS ----------------
        // Roads can be generated even when worldGen.enabled == false by using a temporary flat Terrain (destroyed afterwards).
        private static RoadNetworkPlanner.RoadNetworkResult RunPostTerrainWorldPass(
            Scene scene,
            GenerationConstraints constraints,
            string userPrompt,
            WorldGenSettings wg,
            bool isEdit)
        {
            if (constraints == null) return null;
            if (!constraints.enableAutoArrange) return null;
    
            if (isEdit && !(PromptWantsWorldRebuild(userPrompt) || (wg != null && wg.enabled)))
            {
                // Allow non-destructive edits to still build roads if plan wants them.
                // If you want to block this, remove the below block.
            }
    
            if (wg == null) return null;
    
            bool doRoads = wg.roadHubs != null && wg.roadHubs.Count > 0;
            bool doSettlements = wg.settlements != null && wg.settlements.Count > 0;
    
            if (!doRoads && !doSettlements)
                return null;
    
            Terrain terrain = FindTerrainInScene(scene);
            bool createdTempTerrain = false;
            GameObject tempTerrainGO = null;
    
            // If no terrain exists AND plan does NOT want terrain enabled, we create a TEMP flat terrain for roads only.
            // This satisfies: “terrain only if user says terrain” while still letting road tracks generate.
            if (terrain == null && doRoads && !wg.enabled && !(constraints != null && constraints.forbidTerrain))
            {
                try
                {
                    tempTerrainGO = CreateTemporaryFlatTerrain(scene, sizeXZ: TempTerrainSizeXZ, heightY: TempTerrainHeightY);
                    terrain = tempTerrainGO.GetComponent<Terrain>();
                    createdTempTerrain = terrain != null;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("AI Game Generator: temp flat terrain creation failed: " + ex.Message);
                }
            }
    
            // If still no terrain, we can't run terrain-based road/settlement systems
            if (terrain == null)
                return null;
    
            RoadNetworkPlanner.RoadNetworkResult roadsResult = null;
    
            try
            {
                if (doRoads)
                {
                    var roadSettings = BuildRoadGenSettingsFromPrompt(userPrompt);
                    Material roadMat = null;
                    RoadNetworkPlanner.GenerateRoadSystem(scene, terrain, roadSettings, wg.roadHubs, roadMat, alsoCreateTunnelRoadbed: false);
                    roadsResult = RoadNetworkPlanner.LastResult;
                }
    
                if (doSettlements)
                {
                    // Settlements should generally require real terrain; if temp, skip.
                    if (!createdTempTerrain)
                    {
                        TryGenerateSettlements(scene, terrain, roadsResult, wg.settlements, userPrompt);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("AI Game Generator: World generation (roads/settlements) failed: " + ex.Message);
            }
            finally
            {
                // Remove temp terrain so “terrain only if user says terrain”.
                if (createdTempTerrain && tempTerrainGO != null)
                {
                    try { UnityEngine.Object.DestroyImmediate(tempTerrainGO); } catch { }
                }
            }
    
            // Only snap if a real terrain exists (not temp)
            var realTerrain = FindTerrainInScene(scene);
            if (realTerrain != null)
            {
                TerrainSnapper.SnapSceneObjectsToTerrain(scene, realTerrain);
            }
    
            return roadsResult;
        }
    
        private static GameObject CreateTemporaryFlatTerrain(Scene scene, float sizeXZ, float heightY)
        {
            var td = new TerrainData();
            td.heightmapResolution = TempTerrainResolution;
            td.size = new Vector3(sizeXZ, heightY, sizeXZ);
    
            // Flat heights
            int res = td.heightmapResolution;
            float[,] heights = new float[res, res];
            td.SetHeights(0, 0, heights);
    
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "__TempFlatTerrain__";
            SceneManager.MoveGameObjectToScene(go, scene);
    
            // Keep it far below camera? No; it needs to cover road area. We center at origin.
            go.transform.position = Vector3.zero;
            return go;
        }
    
        // ---------------- SETTLEMENTS ----------------
    
        private static void TryGenerateSettlements(
            Scene scene,
            Terrain terrain,
            RoadNetworkPlanner.RoadNetworkResult roadsResult,
            List<SettlementNode> aiSettlements,
            string userPrompt)
        {
            if (roadsResult == null) return;
    
            var setSettings = BuildSettlementGenSettingsFromPrompt(userPrompt);
    
            Material buildingMat = null;
            Material wallMat = null;
    
            SettlementNetworkPlanner.GenerateSettlements(
                scene,
                terrain,
                roadsResult,
                setSettings,
                aiSettlements,
                userPrompt,
                buildingMat,
                wallMat);
    
            Debug.Log("AI Game Generator: Settlements generated.");
        }
    
        private static SettlementGenSettings BuildSettlementGenSettingsFromPrompt(string userPrompt)
        {
            int seed = StablePromptSeed(userPrompt);
            string p = (userPrompt ?? "").ToLowerInvariant();
    
            var s = new SettlementGenSettings
            {
                seed = seed,
                style = SettlementStyle.Auto,
                autoModernWeight = 0.45f,
                autoMedievalWeight = 0.30f,
                autoSuburbanWeight = 0.25f,
                maxSettlements = 10,
                hubTownChance = 0.55f,
                hubTownMinSpacing = 180f,
                enableRoadsideVillages = true,
                roadsideStep = 300f,
                roadsideVillageChance = 0.35f,
                maxSlopeDeg = 14f,
                preferLowlands = 0.70f,
                avoidHighPeaks = 0.85f,
                settlementRadius = 140f,
                buildingsPerTown = 70,
                buildingsPerVillage = 28,
                lotMinSize = 10f,
                lotMaxSize = 22f,
                buildingHeightMin = 6f,
                buildingHeightMax = 22f,
                roadClearance = 10f,
                padRadius = 10f,
                padFalloff = 6f,
                padBlend = 0.90f,
                modernGridCellsPerSide = 10,
                modernBlockCellSize = 26f,
                modernStreetGap = 6f,
                medievalAddMarket = true,
                medievalAddWalls = true,
                medievalRingCount = 4,
                medievalRingSpacingFactor = 0.22f,
                medievalWallThickness = 3f,
                suburbanCulDeSacCount = 5,
                suburbanCulDeSacRadius = 30f,
                suburbanYardPadding = 3.0f
            };
    
            if (p.Contains("medieval") || p.Contains("castle") || p.Contains("walled") || p.Contains("market"))
                s.style = SettlementStyle.MedievalTown;
            else if (p.Contains("suburb") || p.Contains("cul-de-sac") || p.Contains("neighborhood"))
                s.style = SettlementStyle.Suburban;
            else if (p.Contains("modern") || p.Contains("downtown") || p.Contains("grid") || p.Contains("highrise"))
                s.style = SettlementStyle.ModernCityBlocks;
    
            if (p.Contains("sparse") || p.Contains("few buildings"))
            {
                s.buildingsPerTown = Mathf.Max(12, s.buildingsPerTown / 2);
                s.buildingsPerVillage = Mathf.Max(6, s.buildingsPerVillage / 2);
                s.maxSettlements = Mathf.Max(2, s.maxSettlements / 2);
            }
    
            if (p.Contains("dense") || p.Contains("many buildings"))
            {
                s.buildingsPerTown = Mathf.Min(250, Mathf.RoundToInt(s.buildingsPerTown * 1.5f));
                s.buildingsPerVillage = Mathf.Min(120, Mathf.RoundToInt(s.buildingsPerVillage * 1.35f));
            }
    
            if (p.Contains("one town") || p.Contains("single town"))
            {
                s.maxSettlements = 1;
                s.enableRoadsideVillages = false;
            }
    
            return s;
        }
    
        private static RoadGenSettings BuildRoadGenSettingsFromPrompt(string userPrompt)
        {
            int seed = StablePromptSeed(userPrompt);
            string p = (userPrompt ?? "").ToLowerInvariant();
    
            bool wantsTunnels =
                p.Contains("tunnel") || p.Contains("tunnels") || p.Contains("through the mountain") || p.Contains("cut through");
    
            bool wantsBridges =
                p.Contains("bridge") || p.Contains("bridges") || p.Contains("ravine") || p.Contains("gorge") || p.Contains("cliff");
    
            var s = new RoadGenSettings
            {
                seed = seed,
                gridResolution = 256,
    
                hubCount = 18,
                extraConnections = 6,
                hubMinSpacingWorld = 90f,
    
                maxSurfaceSlopeDeg = 12f,
                hardBlockSlopeDeg = 18f,
                maxRoadGrade = 0.10f,
    
                heightPenalty = 4f,
                slopePenalty = 16f,
                valleyBonus = 2.5f,
                turnPenalty = 1.2f,
    
                roadHalfWidth = 3.5f,
                roadYOffset = 0.02f,
    
                stampFalloff = 4f,
                stampBlend = 2f,
                stampCutDepth = 0.8f,
    
                simplifyToleranceWorld = 2.0f,
                chaikinIterations = 2,
    
                tunnelTriggerCoverMeters = wantsTunnels ? 8f : 9999f,
                tunnelInnerRadius = 6f,
                tunnelWallThickness = 1.5f,
                tunnelPortalLength = 6f,
    
                bridgeTriggerDropMeters = wantsBridges ? 6f : 9999f,
                minBridgeLength = 28f,
                bridgeDeckClearance = 0.25f,
                bridgeDeckThickness = 1.0f,
                bridgePillarSpacing = 14f,
                bridgePillarWidth = 2.5f,
                bridgeRailHeight = 0.9f,
                bridgeRailThickness = 0.18f,
                bridgeMarkingWidth = 0.18f,
                bridgeMarkingYOffset = 0.02f,
                bridgeDashLength = 2.0f,
                bridgeDashGap = 1.2f,
                bridgeEnableTruss = false,
                bridgeTrussHeight = 2.0f,
                bridgeTrussThickness = 0.25f,
                bridgeTrussPostSpacing = 3.0f
            };
    
            if (p.Contains("no tunnel") || p.Contains("no tunnels") || p.Contains("without tunnels"))
                s.tunnelTriggerCoverMeters = 9999f;
    
            if (p.Contains("no bridge") || p.Contains("no bridges") || p.Contains("without bridges"))
                s.bridgeTriggerDropMeters = 9999f;
    
            return s;
        }
    
        // ---------------- NON-DESTRUCTIVE EDITS ----------------
    
        private static void ApplyNonDestructiveEditsOnly(
            Scene scene,
            SceneDefinition sceneDef,
            GenerationConstraints constraints,
            Dictionary<string, Material> matMap)
        {
            if (sceneDef?.objects == null) return;
    
            foreach (var objDef in sceneDef.objects)
            {
                var go = FindByIdInScene(scene, objDef.id);
                if (go == null) continue;
    
                if (objDef.components != null && objDef.components.Count > 0)
                    ApplyComponentsWithUndo(go, objDef);
    
                ApplyMaterialWithUndo(go, objDef, constraints, matMap);
            }
        }
    
        private static void ApplyNonDestructiveEditsCreateMissing(
            Scene scene,
            SceneDefinition sceneDef,
            GenerationConstraints constraints,
            Dictionary<string, Material> matMap,
            Dictionary<string, TerrainData> terrainMap,
            bool isEdit)
        {
            if (sceneDef?.objects == null) return;
    
            foreach (var objDef in sceneDef.objects)
            {
                var go = FindByIdInScene(scene, objDef.id);
    
                if (go == null)
                {
                    go = CreateFromDefinition(objDef, constraints, terrainMap);
                    go.name = objDef.id;
    
                    Undo.RegisterCreatedObjectUndo(go, "AI Create Object");
                    SceneManager.MoveGameObjectToScene(go, scene);
    
                    Undo.RecordObject(go.transform, "AI Modify Transform");
                    ApplyTransform(go, objDef, isEdit: false);
                }
    
                if (objDef.components != null && objDef.components.Count > 0)
                    ApplyComponentsWithUndo(go, objDef);
    
                ApplyMaterialWithUndo(go, objDef, constraints, matMap);
            }
    
            var created = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var objDef in sceneDef.objects)
            {
                var go = FindByIdInScene(scene, objDef.id);
                if (go != null) created[objDef.id] = go;
            }
    
            foreach (var objDef in sceneDef.objects)
            {
                if (string.IsNullOrWhiteSpace(objDef.parentId)) continue;
                if (!created.TryGetValue(objDef.id, out var child)) continue;
    
                var parent = FindByIdInScene(scene, objDef.parentId);
                if (parent != null && child.transform.parent != parent.transform)
                    Undo.SetTransformParent(child.transform, parent.transform, "AI Set Parent");
            }
        }
    
        private static bool PromptWantsWorldRebuild(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt)) return false;
            string p = userPrompt.ToLowerInvariant();
    
            bool looksVisualOnly =
                (p.Contains("color") || p.Contains("material") || p.Contains("texture") ||
                 p.Contains("paint") || p.Contains("change")) &&
                !(p.Contains("terrain") || p.Contains("mountain") || p.Contains("valley") ||
                  p.Contains("road") || p.Contains("street") || p.Contains("bridge") || p.Contains("tunnel") ||
                  p.Contains("settlement") || p.Contains("town") || p.Contains("village") || p.Contains("city") ||
                  p.Contains("flatten") || p.Contains("stamp") || p.Contains("rebuild") || p.Contains("regenerate"));
    
            if (looksVisualOnly) return false;
    
            return
                p.Contains("terrain") || p.Contains("mountain") || p.Contains("valley") || p.Contains("hill") || p.Contains("canyon") ||
                p.Contains("city") || p.Contains("settlement") || p.Contains("town") || p.Contains("village") ||
                p.Contains("road") || p.Contains("street") || p.Contains("bridge") || p.Contains("tunnel") ||
                p.Contains("flatten") || p.Contains("smooth") || p.Contains("stamp") ||
                p.Contains("arrange") || p.Contains("regenerate") || p.Contains("rebuild");
        }
    
        private static bool PromptWantsSpawn(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt)) return false;
            string p = userPrompt.ToLowerInvariant();
    
            return p.Contains("add ") || p.Contains("create ") || p.Contains("spawn ") || p.Contains("place ") ||
                   p.Contains("make a ") || p.Contains("generate a ");
        }
    
        // ---------------- OBJECT BUILD ----------------
    
        private static void BuildSceneObjects(
            Scene scene,
            SceneDefinition sceneDef,
            GenerationConstraints constraints,
            Dictionary<string, Material> matMap,
            Dictionary<string, TerrainData> terrainMap,
            bool isEdit)
        {
            var created = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    
            foreach (var objDef in sceneDef.objects)
            {
                var go = FindByIdInScene(scene, objDef.id);
                if (go == null)
                {
                    go = CreateFromDefinition(objDef, constraints, terrainMap);
                    go.name = objDef.id;
    
                    Undo.RegisterCreatedObjectUndo(go, "AI Create Object");
                    SceneManager.MoveGameObjectToScene(go, scene);
                }
    
                Undo.RecordObject(go.transform, "AI Modify Transform");
                ApplyTransform(go, objDef, isEdit);
    
                ApplyComponentsWithUndo(go, objDef);
                ApplyMaterialWithUndo(go, objDef, constraints, matMap);
    
                created[objDef.id] = go;
            }
    
            foreach (var objDef in sceneDef.objects)
            {
                if (string.IsNullOrWhiteSpace(objDef.parentId)) continue;
                if (!created.TryGetValue(objDef.id, out var child)) continue;
    
                var parent = FindByIdInScene(scene, objDef.parentId);
                if (parent != null && child.transform.parent != parent.transform)
                    Undo.SetTransformParent(child.transform, parent.transform, "AI Set Parent");
            }
        }
    
        private static void ApplyTransform(GameObject go, ObjectDefinition def, bool isEdit)
        {
            go.transform.position = def.position;
    
            if (def.rotationEuler != Vector3.zero)
                go.transform.rotation = Quaternion.Euler(def.rotationEuler);
    
            if (!isEdit || def.scale != Vector3.zero)
            {
                var s = def.scale == Vector3.zero ? Vector3.one : def.scale;
                go.transform.localScale = s;
            }
        }
    
        private static void ApplyMaterialWithUndo(GameObject go, ObjectDefinition def, GenerationConstraints constraints, Dictionary<string, Material> matMap)
        {
            if (constraints.forbidMaterials) return;
            if (string.IsNullOrWhiteSpace(def.materialId)) return;
            if (matMap == null || !matMap.TryGetValue(def.materialId, out var mat)) return;
    
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
    
            Undo.RecordObject(r, "AI Assign Material");
            r.sharedMaterial = mat;
        }
    
        private static void ApplyComponentsWithUndo(GameObject go, ObjectDefinition def)
        {
            if (def.components == null) return;
    
            foreach (var compDef in def.components)
            {
                var type = ResolveComponentType(def, compDef, out bool isBuiltin);
                if (type == null) continue;
    
                var existing = go.GetComponents(type);
                for (int i = 0; i < existing.Length; i++)
                    Undo.DestroyObjectImmediate(existing[i]);
            }
    
            foreach (var compDef in def.components)
            {
                var type = ResolveComponentType(def, compDef, out bool isBuiltin);
                if (type == null)
                    throw new PlanValidationException($"Component type missing/unknown on '{def.id}'.");
    
                Component component;
                if (isBuiltin)
                {
                    component = BuiltinComponentFactory.Add(go, compDef.builtinComponent);
                    if (component == null)
                        throw new PlanValidationException($"Unsupported builtinComponent '{compDef.builtinComponent}' on '{def.id}'.");
                }
                else
                {
                    component = Undo.AddComponent(go, type);
                }
    
                if (compDef.properties == null) continue;
                foreach (var p in compDef.properties)
                    SetMemberValue(component, type, p);
            }
        }
    
        // ---------------- AUTO-SPAWN SCRIPT OBJECTS ----------------
    
        private static void EnsureScriptBackedObjects(SceneDefinition sceneDef, GameGeneration gen, string userPrompt)
        {
            if (sceneDef == null || gen == null) return;
            if (gen.scripts == null || gen.scripts.Count == 0) return;
            if (!PromptWantsSpawn(userPrompt)) return;
    
            if (sceneDef.objects == null)
                sceneDef.objects = new List<ObjectDefinition>();
    
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in sceneDef.objects)
            {
                if (o?.components == null) continue;
                foreach (var c in o.components)
                {
                    if (!string.IsNullOrWhiteSpace(c?.script))
                        referenced.Add(c.script.Trim());
                }
            }
    
            foreach (var s in gen.scripts)
            {
                if (s == null) continue;
                var scriptName = (s.name ?? "").Trim();
                if (string.IsNullOrWhiteSpace(scriptName)) continue;
    
                if (referenced.Contains(scriptName)) continue;
    
                if (sceneDef.objects.Exists(o => string.Equals(o.id, scriptName, StringComparison.OrdinalIgnoreCase)))
                    continue;
    
                if (!ShouldAutoSpawnForScript(scriptName, userPrompt))
                    continue;
    
                var obj = new ObjectDefinition
                {
                    id = scriptName,
                    kind = "Primitive",
                    primitiveType = GuessPrimitiveType(scriptName, userPrompt),
                    position = GuessSpawnPosition(sceneDef),
                    rotationEuler = Vector3.zero,
                    scale = Vector3.one,
                    components = new List<ComponentDefinition>
                    {
                        new ComponentDefinition { script = scriptName }
                    }
                };
    
                sceneDef.objects.Add(obj);
                referenced.Add(scriptName);
            }
        }
    
        private static bool ShouldAutoSpawnForScript(string scriptName, string userPrompt)
        {
            var n = (scriptName ?? "").ToLowerInvariant();
            var p = (userPrompt ?? "").ToLowerInvariant();
    
            if (p.Contains("cube")) return true;
    
            if (n.Contains("mover") || n.Contains("move") || n.Contains("enemy") || n.Contains("npc") ||
                n.Contains("pickup") || n.Contains("collect") || n.Contains("projectile") || n.Contains("bullet") ||
                n.Contains("door") || n.Contains("platform") || n.Contains("trap"))
                return true;
    
            if (n.Contains("manager") || n.Contains("system") || n.Contains("service") || n.Contains("bootstrap"))
                return false;
    
            if (n.EndsWith("controller") && PromptWantsSpawn(userPrompt))
                return true;
    
            return false;
        }
    
        private static string GuessPrimitiveType(string scriptName, string userPrompt)
        {
            var n = (scriptName ?? "").ToLowerInvariant();
            var p = (userPrompt ?? "").ToLowerInvariant();
    
            if (p.Contains("cube") || n.Contains("cube")) return "Cube";
            if (p.Contains("sphere") || n.Contains("sphere")) return "Sphere";
            if (p.Contains("capsule") || n.Contains("player")) return "Capsule";
            if (p.Contains("plane") || n.Contains("ground")) return "Plane";
    
            return "Cube";
        }
    
        private static Vector3 GuessSpawnPosition(SceneDefinition sceneDef)
        {
            if (sceneDef?.objects != null && sceneDef.objects.Count > 0)
            {
                var last = sceneDef.objects[sceneDef.objects.Count - 1];
                var basePos = last != null ? last.position : Vector3.zero;
                return basePos + new Vector3(2f, 0f, 2f);
            }
            return Vector3.zero;
        }
    
        // ---------------- TERRAIN SNAP ----------------
    
        public static class TerrainSnapper
        {
            private static bool IsGeneratedMeshRoadOrBridgeOrTunnel(GameObject go)
            {
                string n = (go.name ?? "").ToLowerInvariant();
    
                if (n == "ai_roadnetwork" || n == "ai_bridgenetwork" || n == "ai_tunnelnetwork" || n == "ai_roadhubs")
                    return true;
    
                if (n.StartsWith("road_") || n.StartsWith("bridge_") || n.StartsWith("tunnel_") || n.StartsWith("portal_"))
                    return true;
    
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    string mn = mf.sharedMesh.name.ToLowerInvariant();
                    if (mn.Contains("__ai_") && (mn.Contains("road") || mn.Contains("bridge") || mn.Contains("tunnel")))
                        return true;
                }
    
                return false;
            }
    
            public static void SnapSceneObjectsToTerrain(Scene scene, Terrain terrain, Func<GameObject, bool> filter = null)
            {
                if (terrain == null) return;
    
                var roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    foreach (var tr in roots[i].GetComponentsInChildren<Transform>(true))
                    {
                        var go = tr.gameObject;
                        if (filter != null && !filter(go)) continue;
    
                        if (IsGeneratedMeshRoadOrBridgeOrTunnel(go)) continue;
    
                        string n = (go.name ?? "").ToLowerInvariant();
    
                        bool snap =
                            n.Contains("building") || n.Contains("house") || n.Contains("tower") ||
                            n.Contains("block") || n.Contains("settlement") || n.Contains("village") ||
                            n.Contains("town") || n.Contains("landmark");
    
                        if (!snap) continue;
    
                        Vector3 p = go.transform.position;
                        float y = terrain.SampleHeight(p) + terrain.transform.position.y;
    
                        Undo.RecordObject(go.transform, "Snap To Terrain");
    
                        if (n.Contains("building") || n.Contains("house") || n.Contains("tower") || n.Contains("block") || n.Contains("landmark"))
                            go.transform.position = new Vector3(p.x, y + (go.transform.localScale.y * 0.5f), p.z);
                        else
                            go.transform.position = new Vector3(p.x, y, p.z);
                    }
                }
            }
    
            public static void SnapBuildingsOnly(Scene scene, Terrain terrain)
            {
                SnapSceneObjectsToTerrain(scene, terrain, go =>
                {
                    string n = (go.name ?? "").ToLowerInvariant();
                    return n.Contains("building") || n.Contains("house") || n.Contains("tower") || n.Contains("block") || n.Contains("landmark");
                });
            }
        }
    
        // ---------------- TERRAIN (PLAN-DRIVEN) ----------------
    
        private static void MaybeGenerateOrShapeTerrain(Scene scene, GenerationConstraints constraints, string userPrompt, WorldGenSettings wg)
        {
            if (constraints != null && constraints.forbidTerrain) return;
            if (wg == null || !wg.enabled) return;
    
            Terrain existing = FindTerrainInScene(scene);
    
            TerrainGenerator.TerrainPreset preset;
            if (!string.IsNullOrWhiteSpace(wg.terrainPreset))
                preset = MapTerrainPreset(wg.terrainPreset);
            else
                preset = TerrainGenerator.GuessPresetFromPrompt(userPrompt);
    
            int seed = StablePromptSeed(userPrompt);
    
            if (existing == null)
            {
                var go = TerrainGenerator.CreateOrReplaceTerrain("GeneratedTerrain", preset, seed, Vector3.zero);
                SceneManager.MoveGameObjectToScene(go, scene);
                Debug.Log($"Terrain generated (new): {preset} (seed {seed})");
                return;
            }
    
            ApplyHeightsToTerrain(existing, preset, seed);
            Debug.Log($"Terrain shaped (existing): {preset} (seed {seed})");
        }
    
        private static Terrain FindTerrainInScene(Scene scene)
        {
            if (!scene.IsValid()) return null;
    
            if (Terrain.activeTerrain != null && Terrain.activeTerrain.gameObject.scene == scene)
                return Terrain.activeTerrain;
    
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var t = roots[i].GetComponentInChildren<Terrain>(true);
                if (t != null) return t;
            }
            return null;
        }
    
        private static int StablePromptSeed(string s)
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
    
        private static void ApplyHeightsToTerrain(Terrain terrain, TerrainGenerator.TerrainPreset preset, int seed)
        {
            if (terrain == null) return;
    
            string tempName = "__TempTerrainGen__";
            Vector3 tempPos = new Vector3(999999f, 999999f, 999999f);
    
            GameObject tempGo = null;
    
            try
            {
                tempGo = TerrainGenerator.CreateOrReplaceTerrain(tempName, preset, seed, tempPos);
                var tempTerrain = tempGo != null ? tempGo.GetComponent<Terrain>() : null;
                if (tempTerrain == null || tempTerrain.terrainData == null)
                    return;
    
                CopyTerrainData(tempTerrain.terrainData, terrain);
            }
            finally
            {
                if (tempGo != null)
                    UnityEngine.Object.DestroyImmediate(tempGo);
            }
        }
    
        private static void CopyTerrainData(TerrainData src, Terrain dstTerrain)
        {
            if (src == null || dstTerrain == null) return;
    
            var dst = dstTerrain.terrainData;
            if (dst == null)
            {
                dst = new TerrainData();
                dstTerrain.terrainData = dst;
            }
    
            dst.heightmapResolution = src.heightmapResolution;
            dst.size = src.size;
    
            var heights = src.GetHeights(0, 0, src.heightmapResolution, src.heightmapResolution);
            dst.SetHeights(0, 0, heights);
    
            var col = dstTerrain.GetComponent<TerrainCollider>();
            if (col != null) col.terrainData = dst;
    
            dstTerrain.drawInstanced = true;
            dstTerrain.heightmapPixelError = TerrainPixelError;
        }
    
        // ---------------- HELPERS ----------------
    
        private static GenerationConstraints LoadConstraints()
        {
            var json = SessionState.GetString(AiPrefsKeys.SessionConstraints, "");
            if (string.IsNullOrWhiteSpace(json))
                return new GenerationConstraints();
    
            try
            {
                var c = JsonUtility.FromJson<GenerationConstraints>(json);
                return c ?? new GenerationConstraints();
            }
            catch
            {
                return new GenerationConstraints();
            }
        }
    
        private static bool ResolveEnsureCameraLight(GameGeneration gen, SceneDefinition sceneDef, GenerationConstraints constraints)
        {
            if (constraints.forceEnsureCameraLight) return true;
    
            bool ensure = gen.settings?.ensureCameraAndLight ?? true;
            if (sceneDef.settings != null && sceneDef.settings.ensureCameraAndLight.HasValue)
                ensure = sceneDef.settings.ensureCameraAndLight.Value;
    
            return ensure;
        }
    
        private static GameObject FindByIdInScene(Scene scene, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
    
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var found = FindInHierarchyByName(roots[i].transform, id);
                if (found != null) return found.gameObject;
            }
            return null;
        }
    
        private static Transform FindInHierarchyByName(Transform root, string name)
        {
            if (root.name == name) return root;
    
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var found = FindInHierarchyByName(child, name);
                if (found != null) return found;
            }
    
            return null;
        }
    
        private static GameObject CreateFromDefinition(ObjectDefinition def, GenerationConstraints constraints, Dictionary<string, TerrainData> terrainMap)
        {
            string kind = string.IsNullOrWhiteSpace(def.kind) ? "Primitive" : def.kind;
    
            if (constraints.forbidTerrain && kind == "Terrain")
                throw new PlanValidationException("Terrain is forbidden by constraints.");
    
            GameObject go;
            switch (kind)
            {
                case "Empty":
                    go = new GameObject(def.id);
                    break;
                case "Builtin":
                    go = CreateBuiltin(def);
                    break;
                case "Terrain":
                    go = CreateTerrain(def, terrainMap);
                    break;
                case "Primitive":
                default:
                    go = CreatePrimitive(def);
                    break;
            }
    
            go.name = def.id;
            return go;
        }
    
        private static GameObject CreatePrimitive(ObjectDefinition def)
        {
            string p = string.IsNullOrWhiteSpace(def.primitiveType) ? "Cube" : def.primitiveType;
    
            PrimitiveType pt = p switch
            {
                "Sphere" => PrimitiveType.Sphere,
                "Capsule" => PrimitiveType.Capsule,
                "Plane" => PrimitiveType.Plane,
                "Cylinder" => PrimitiveType.Cylinder,
                "Quad" => PrimitiveType.Quad,
                _ => PrimitiveType.Cube
            };
    
            return GameObject.CreatePrimitive(pt);
        }
    
        private static GameObject CreateBuiltin(ObjectDefinition def)
        {
            string t = def.builtinType ?? "";
    
            if (t == "Camera")
            {
                var go = new GameObject(def.id);
                go.tag = "MainCamera";
                go.AddComponent<Camera>();
    
                if (UnityEngine.Object.FindObjectOfType<AudioListener>() == null)
                    go.AddComponent<AudioListener>();
    
                return go;
            }
    
            if (t == "DirectionalLight")
            {
                var go = new GameObject(def.id);
                var l = go.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1f;
                return go;
            }
    
            if (t == "PointLight")
            {
                var go = new GameObject(def.id);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.intensity = 1f;
                l.range = 10f;
                return go;
            }
    
            if (t == "SpotLight")
            {
                var go = new GameObject(def.id);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.intensity = 1f;
                l.range = 20f;
                l.spotAngle = 30f;
                return go;
            }
    
            return new GameObject(def.id);
        }
    
        private static GameObject CreateTerrain(ObjectDefinition def, Dictionary<string, TerrainData> terrainMap)
        {
            TerrainData td = null;
            if (!string.IsNullOrWhiteSpace(def.terrainDataId))
                terrainMap?.TryGetValue(def.terrainDataId, out td);
    
            if (td == null) td = new TerrainData();
            return Terrain.CreateTerrainGameObject(td);
        }
    
        private static Type ResolveComponentType(ObjectDefinition obj, ComponentDefinition comp, out bool isBuiltin)
        {
            isBuiltin = false;
    
            if (!string.IsNullOrWhiteSpace(comp.builtinComponent))
            {
                isBuiltin = true;
                return BuiltinComponentFactory.ResolveType(comp.builtinComponent);
            }
    
            string scriptName = ResolveMissingScriptName(obj, comp);
            if (string.IsNullOrWhiteSpace(scriptName))
                return null;
    
            var ms = MonoScriptResolver.Find(scriptName);
            if (ms == null) return null;
    
            return ms.GetClass();
        }
    
        private static string ResolveMissingScriptName(ObjectDefinition obj, ComponentDefinition comp)
        {
            if (comp != null && !string.IsNullOrWhiteSpace(comp.script))
                return comp.script;
    
            if (!string.IsNullOrWhiteSpace(obj?.id) &&
                obj.id.Equals("Player", StringComparison.OrdinalIgnoreCase))
                return "PlayerController";
    
            if (!string.IsNullOrWhiteSpace(obj?.id))
                return obj.id + "Controller";
    
            return null;
        }
    
        private static void SetMemberValue(Component component, Type type, PropertyValue p)
        {
            if (component == null || p == null) return;
            if (string.IsNullOrWhiteSpace(p.name) || string.IsNullOrWhiteSpace(p.type))
                return;
    
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    
            var field = type.GetField(p.name, flags);
            if (field != null)
            {
                object v = ConvertTypedValue(field.FieldType, p);
                if (v != null) field.SetValue(component, v);
                return;
            }
    
            var prop = type.GetProperty(p.name, flags);
            if (prop != null && prop.CanWrite)
            {
                object v = ConvertTypedValue(prop.PropertyType, p);
                if (v != null) prop.SetValue(component, v);
            }
        }
    
        private static object ConvertTypedValue(Type targetType, PropertyValue p)
        {
            switch (p.type)
            {
                case "float":
                    if (targetType == typeof(float)) return p.floatValue;
                    if (targetType == typeof(int)) return (int)p.floatValue;
                    if (targetType == typeof(string)) return p.floatValue.ToString();
                    break;
    
                case "int":
                    if (targetType == typeof(int)) return p.intValue;
                    if (targetType == typeof(float)) return (float)p.intValue;
                    if (targetType == typeof(string)) return p.intValue.ToString();
                    break;
    
                case "bool":
                    if (targetType == typeof(bool)) return p.boolValue;
                    if (targetType == typeof(string)) return p.boolValue.ToString();
                    break;
    
                case "string":
                    if (targetType == typeof(string)) return p.stringValue ?? "";
                    break;
    
                case "color":
                    if (targetType == typeof(Color)) return p.colorValue;
                    break;
    
                case "vector3":
                    if (targetType == typeof(Vector3)) return p.vector3Value;
                    break;
            }
    
            return null;
        }
    
        private static void ValidatePlan(GameGeneration gen, GenerationConstraints constraints, bool levelGen)
        {
            if (gen == null) throw new PlanValidationException("Invalid JSON: could not parse.");
            if (gen.scenes == null || gen.scenes.Count == 0) throw new PlanValidationException("Invalid plan: no scenes.");
            if (gen.flow == null) throw new PlanValidationException("Invalid plan: missing flow.");
    
            if (gen.scenes.Count > constraints.maxScenes)
                throw new PlanValidationException($"Too many scenes: {gen.scenes.Count} > {constraints.maxScenes}");
    
            if (constraints.forbidScripts && gen.scripts != null && gen.scripts.Count > 0)
                throw new PlanValidationException("Scripts are forbidden by constraints, but plan includes scripts.");
    
            if (gen.settings == null || gen.settings.worldGen == null)
                throw new PlanValidationException("Invalid plan: enforce settings.worldGen (plan-driven world generation).");
    
            if (gen.scripts != null)
            {
                foreach (var s in gen.scripts)
                {
                    if (string.IsNullOrWhiteSpace(s.path) || !s.path.StartsWith("Assets/"))
                        throw new PlanValidationException($"Invalid script path (must be under Assets/): {s.path}");
                    if (string.IsNullOrWhiteSpace(s.name))
                        throw new PlanValidationException("Invalid script: name missing.");
                    if (string.IsNullOrWhiteSpace(s.code))
                        throw new PlanValidationException($"Invalid script '{s.name}': code missing.");
                }
            }
    
            foreach (var scene in gen.scenes)
            {
                if (string.IsNullOrWhiteSpace(scene.name))
                    throw new PlanValidationException("Scene name missing.");
    
                if (scene.objects == null || scene.objects.Count == 0)
                    throw new PlanValidationException($"Scene '{scene.name}' has no objects.");
    
                if (scene.objects.Count > constraints.maxObjectsPerScene)
                    throw new PlanValidationException(
                        $"Too many objects in scene '{scene.name}': {scene.objects.Count} > {constraints.maxObjectsPerScene}"
                    );
    
                foreach (var o in scene.objects)
                {
                    if (string.IsNullOrWhiteSpace(o.id))
                        throw new PlanValidationException($"Object id missing in scene '{scene.name}'.");
    
                    string kind = string.IsNullOrWhiteSpace(o.kind) ? "Primitive" : o.kind;
    
                    if (kind != "Primitive" && kind != "Empty" && kind != "Builtin" && kind != "Terrain")
                        throw new PlanValidationException($"Unsupported kind '{kind}' on '{o.id}'.");
    
                    if (constraints.forbidTerrain && kind == "Terrain")
                        throw new PlanValidationException("Terrain is forbidden by constraints.");
    
                    if (kind == "Primitive" && string.IsNullOrWhiteSpace(o.primitiveType))
                        o.primitiveType = "Cube";
    
                    if (kind == "Builtin" && string.IsNullOrWhiteSpace(o.builtinType))
                        throw new PlanValidationException($"Builtin missing builtinType on '{o.id}'.");
    
                    if (constraints.forbidMaterials && !string.IsNullOrWhiteSpace(o.materialId))
                        throw new PlanValidationException("Materials are forbidden by constraints, but plan assigns materialId.");
    
                    if (!constraints.forbidMaterials && !string.IsNullOrWhiteSpace(o.materialId))
                    {
                        bool ok = gen.assets?.materials != null && gen.assets.materials.Exists(m => m.id == o.materialId);
                        if (!ok)
                            throw new PlanValidationException($"materialId '{o.materialId}' referenced by '{o.id}' but not defined in assets.materials.");
                    }
    
                    if (o.components != null)
                    {
                        foreach (var c in o.components)
                        {
                            bool hasScript = !string.IsNullOrWhiteSpace(c.script);
                            bool hasBuiltin = !string.IsNullOrWhiteSpace(c.builtinComponent);
    
                            if (hasScript == hasBuiltin)
                                throw new PlanValidationException($"Component on '{o.id}' must specify exactly one of script or builtinComponent.");
    
                            if (constraints.forbidScripts && hasScript)
                                throw new PlanValidationException($"Scripts are forbidden by constraints, but '{o.id}' uses script component '{c.script}'.");
                        }
                    }
                }
    
                // Marker validation stays as you had it (FPS markers).
                if (levelGen)
                {
                    bool HasLM(string id) =>
                        scene.objects.Any(o => o != null && string.Equals(o.id, id, StringComparison.OrdinalIgnoreCase));
    
                    string[] required =
                    {
                        "LM_ArenaCenter",
                        "LM_ArenaSize",
                        "LM_PlayerSpawn",
                        "LM_MidHotspot",
                        "LM_LaneA_Entry",
                        "LM_LaneA_Exit",
                        "LM_LaneB_Entry",
                        "LM_LaneB_Exit",
                        "LM_EnemySpawn_01",
                        "LM_EnemySpawn_02",
                        "LM_EnemySpawn_03"
                    };
    
                    for (int i = 0; i < required.Length; i++)
                    {
                        if (!HasLM(required[i]))
                            throw new PlanValidationException("LevelGen: missing marker object '" + required[i] + "'.");
                    }
    
                    int enemySpawns = scene.objects.Count(o =>
                        o != null && (o.id ?? "").StartsWith("LM_EnemySpawn_", StringComparison.OrdinalIgnoreCase));
    
                    if (enemySpawns < 3)
                        throw new PlanValidationException("LevelGen: need at least 3 enemy spawns (LM_EnemySpawn_01..03).");
    
                    var arenaSizeObj = scene.objects.FirstOrDefault(o => o != null && string.Equals(o.id, "LM_ArenaSize", StringComparison.OrdinalIgnoreCase));
                    if (arenaSizeObj != null)
                    {
                        if (arenaSizeObj.scale.x <= 1f || arenaSizeObj.scale.z <= 1f)
                            throw new PlanValidationException("LevelGen: LM_ArenaSize.scale must encode arena dimensions (x=width, z=length).");
                    }
                }
            }
        }
    
        private sealed class UndoGroupScope : IDisposable
        {
            private readonly int group;
            public UndoGroupScope(string name)
            {
                Undo.IncrementCurrentGroup();
                group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(name);
            }
            public void Dispose()
            {
                Undo.CollapseUndoOperations(group);
            }
        }
    }
}
