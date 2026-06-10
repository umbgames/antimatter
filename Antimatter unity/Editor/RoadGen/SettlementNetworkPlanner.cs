using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antimatter
{
    
    public static class SettlementNetworkPlanner
    {
        private class Settlement
        {
            public Vector3 center;
            public float radius;
            public int buildingCount;
            public string name;
            public bool isTown;
            public SettlementStyle style;

            // Per-settlement overrides (0 = use global default)
            public float lotMinSize;
            public float lotMaxSize;
            public float buildingHeightMin;
            public float buildingHeightMax;
            public float roadClearance;
            public float padRadius;
        }

        /// <summary>
        /// Merges per-settlement overrides into a copy of the global settings.
        /// Fields with value > 0 in the Settlement override the global.
        /// </summary>
        private static SettlementGenSettings MergeOverrides(SettlementGenSettings global, Settlement set)
        {
            bool hasOverride =
                set.lotMinSize > 0f || set.lotMaxSize > 0f ||
                set.buildingHeightMin > 0f || set.buildingHeightMax > 0f ||
                set.roadClearance > 0f || set.padRadius > 0f;

            if (!hasOverride) return global;

            // Shallow clone via MemberwiseClone workaround (class, not struct)
            var merged = new SettlementGenSettings
            {
                style = global.style,
                autoModernWeight = global.autoModernWeight,
                autoMedievalWeight = global.autoMedievalWeight,
                autoSuburbanWeight = global.autoSuburbanWeight,
                seed = global.seed,
                maxSettlements = global.maxSettlements,
                hubTownChance = global.hubTownChance,
                hubTownMinSpacing = global.hubTownMinSpacing,
                enableRoadsideVillages = global.enableRoadsideVillages,
                roadsideStep = global.roadsideStep,
                roadsideVillageChance = global.roadsideVillageChance,
                maxSlopeDeg = global.maxSlopeDeg,
                preferLowlands = global.preferLowlands,
                avoidHighPeaks = global.avoidHighPeaks,
                settlementRadius = global.settlementRadius,
                buildingsPerTown = global.buildingsPerTown,
                buildingsPerVillage = global.buildingsPerVillage,
                lotMinSize = global.lotMinSize,
                lotMaxSize = global.lotMaxSize,
                buildingHeightMin = global.buildingHeightMin,
                buildingHeightMax = global.buildingHeightMax,
                roadClearance = global.roadClearance,
                padRadius = global.padRadius,
                padFalloff = global.padFalloff,
                padBlend = global.padBlend,
                modernGridCellsPerSide = global.modernGridCellsPerSide,
                modernBlockCellSize = global.modernBlockCellSize,
                modernStreetGap = global.modernStreetGap,
                medievalAddMarket = global.medievalAddMarket,
                medievalAddWalls = global.medievalAddWalls,
                medievalRingCount = global.medievalRingCount,
                medievalRingSpacingFactor = global.medievalRingSpacingFactor,
                medievalWallThickness = global.medievalWallThickness,
                suburbanCulDeSacCount = global.suburbanCulDeSacCount,
                suburbanCulDeSacRadius = global.suburbanCulDeSacRadius,
                suburbanYardPadding = global.suburbanYardPadding
            };

            if (set.lotMinSize > 0f)        merged.lotMinSize = set.lotMinSize;
            if (set.lotMaxSize > 0f)        merged.lotMaxSize = set.lotMaxSize;
            if (set.buildingHeightMin > 0f) merged.buildingHeightMin = set.buildingHeightMin;
            if (set.buildingHeightMax > 0f) merged.buildingHeightMax = set.buildingHeightMax;
            if (set.roadClearance > 0f)     merged.roadClearance = set.roadClearance;
            if (set.padRadius > 0f)         merged.padRadius = set.padRadius;

            return merged;
        }
    
        // ------------------------------------------------------------------------
        // PAD STAMPING (UPDATED FOR SettlementGenSettings.padRadius)
        // ------------------------------------------------------------------------
        // Centralized pad stamping so settings.padRadius is always respected.
        // If a caller suggests a radius (e.g. based on lot size), we use it,
        // but never allow it to go below s.padRadius (if padRadius > 0).
        private static void StampPad(Terrain terrain, Vector3 world, SettlementGenSettings s, float suggestedRadius = -1f)
        {
            float r = (suggestedRadius > 0f) ? suggestedRadius : s.padRadius;
    
            // Safety clamp + enforce settings padRadius as a minimum.
            r = Mathf.Max(0.5f, r);
            if (s.padRadius > 0f) r = Mathf.Max(r, s.padRadius);
    
            // Increased blend for aggressive carving as per user request
            TerrainStamper.StampPadToTerrain(terrain, world, r, s.padFalloff, Mathf.Max(s.padBlend, 0.95f));
        }
    
        // ------------------------------------------------------------------------
        // PUBLIC ENTRY
        // ------------------------------------------------------------------------
        public static void GenerateSettlements(
            Scene scene,
            Terrain terrain,
            RoadNetworkPlanner.RoadNetworkResult roads,
            SettlementGenSettings s,
            List<SettlementNode> explicitSettlements,
            string userPrompt,
            Material buildingMaterial = null,
            Material wallMaterial = null)
        {
            if (!scene.IsValid()) throw new Exception("Invalid scene.");
            if (terrain == null || terrain.terrainData == null) throw new Exception("No valid terrain.");
            if (roads == null) throw new Exception("Road network result is null (run roads first).");
    
            var root = EnsureGroup(scene, "AI_SettlementNetwork");
            ClearChildrenImmediate(root.transform);
    
            var townsRoot = new GameObject("Towns");
            var villagesRoot = new GameObject("Villages");
            SceneManager.MoveGameObjectToScene(townsRoot, scene);
            SceneManager.MoveGameObjectToScene(villagesRoot, scene);
            townsRoot.transform.SetParent(root.transform, false);
            villagesRoot.transform.SetParent(root.transform, false);
    
            var rng = new System.Random(s.seed);
    
            // Decide style from prompt if Auto
            SettlementStyle chosenStyle = ResolveStyleFromPrompt(userPrompt, s, rng);
    
            // Treat bridges as roads too
            var roadLines = new List<List<Vector3>>();
            if (roads?.surfaceRoadCenterlines != null) roadLines.AddRange(roads.surfaceRoadCenterlines);
            if (roads?.bridgeDeckCenterlines != null) roadLines.AddRange(roads.bridgeDeckCenterlines);
    
            var hubs = roads?.hubsWorld ?? new List<Vector3>();
    
            // 1) Select settlement centers
            var settlements = ChooseSettlementCenters(terrain, hubs, roadLines, s, chosenStyle, explicitSettlements, rng);
    
            // 2) Generate each settlement with the chosen style
            foreach (var set in settlements)
            {
                Transform parent = set.isTown ? townsRoot.transform : villagesRoot.transform;
    
                var setRoot = new GameObject(set.name);
                SceneManager.MoveGameObjectToScene(setRoot, scene);
                setRoot.transform.SetParent(parent, false);
                setRoot.transform.position = set.center;
    
                // Apply per-settlement overrides from the AI plan
                var effectiveSettings = MergeOverrides(s, set);
    
                switch (set.style)
                {
                    case SettlementStyle.ModernCityBlocks:
                        BuildModernBlocks(scene, terrain, roadLines, setRoot.transform, set, effectiveSettings, rng, buildingMaterial);
                        break;
    
                    case SettlementStyle.MedievalTown:
                        BuildMedievalTown(scene, terrain, roadLines, setRoot.transform, set, effectiveSettings, rng, buildingMaterial, wallMaterial);
                        break;
    
                    case SettlementStyle.Suburban:
                        BuildSuburban(scene, terrain, roadLines, setRoot.transform, set, effectiveSettings, rng, buildingMaterial);
                        break;
    
                    default:
                        BuildSuburban(scene, terrain, roadLines, setRoot.transform, set, effectiveSettings, rng, buildingMaterial);
                        break;
                }
            }
    
            Debug.Log($"SettlementNetworkPlanner: Generated {settlements.Count} settlements. Style={chosenStyle}");
        }
    
        // ------------------------------------------------------------------------
        // STYLE DETECTION
        // ------------------------------------------------------------------------
        private static SettlementStyle ResolveStyleFromPrompt(string userPrompt, SettlementGenSettings s, System.Random rng)
        {
            if (s.style != SettlementStyle.Auto) return s.style;
    
            string p = (userPrompt ?? "").ToLowerInvariant();
    
            bool modern =
                p.Contains("modern") || p.Contains("downtown") || p.Contains("skyscraper") || p.Contains("city blocks") ||
                p.Contains("grid") || p.Contains("urban") || p.Contains("commercial") || p.Contains("highrise");
    
            bool medieval =
                p.Contains("medieval") || p.Contains("castle") || p.Contains("walled") || p.Contains("market") ||
                p.Contains("old town") || p.Contains("village square") || p.Contains("stone") || p.Contains("fortified");
    
            bool suburban =
                p.Contains("suburb") || p.Contains("suburban") || p.Contains("cul-de-sac") || p.Contains("culdesac") ||
                p.Contains("yards") || p.Contains("houses") || p.Contains("residential") || p.Contains("neighborhood");
    
            if (medieval && !modern && !suburban) return SettlementStyle.MedievalTown;
            if (modern && !medieval && !suburban) return SettlementStyle.ModernCityBlocks;
            if (suburban && !medieval && !modern) return SettlementStyle.Suburban;
    
            if (medieval && (modern || suburban)) return SettlementStyle.MedievalTown;
            if (modern && suburban) return SettlementStyle.ModernCityBlocks;
    
            if (p.Contains("city") || p.Contains("metropolis")) modern = true;
            if (p.Contains("village") && !p.Contains("modern")) medieval = true;
    
            if (modern && !medieval) return SettlementStyle.ModernCityBlocks;
            if (medieval) return SettlementStyle.MedievalTown;
            if (suburban) return SettlementStyle.Suburban;
    
            float wM = Mathf.Max(0.0001f, s.autoModernWeight);
            float wD = Mathf.Max(0.0001f, s.autoMedievalWeight);
            float wS = Mathf.Max(0.0001f, s.autoSuburbanWeight);
            float sum = wM + wD + wS;
    
            float r = (float)rng.NextDouble() * sum;
            if (r < wM) return SettlementStyle.ModernCityBlocks;
            if (r < wM + wD) return SettlementStyle.MedievalTown;
            return SettlementStyle.Suburban;
        }
    
        // ------------------------------------------------------------------------
        // CENTER SELECTION (hubs + roadside villages) – uses chosenStyle
        // ------------------------------------------------------------------------
        private static List<Settlement> ChooseSettlementCenters(
            Terrain terrain,
            List<Vector3> hubs,
            List<List<Vector3>> roadLines,
            SettlementGenSettings s,
            SettlementStyle chosenStyle,
            List<SettlementNode> explicitSettlements,
            System.Random rng)
        {
            var settlements = new List<Settlement>(s.maxSettlements);
    
            // Bypass C# randomness if the AI has laid out towns explicitly
            if (explicitSettlements != null && explicitSettlements.Count > 0)
            {
                foreach (var ext in explicitSettlements)
                {
                    var style = chosenStyle;
                    if (!string.IsNullOrEmpty(ext.style) && Enum.TryParse(ext.style, true, out SettlementStyle parsed))
                        style = parsed == SettlementStyle.Auto ? chosenStyle : parsed;
    
                    settlements.Add(new Settlement
                    {
                        center = SnapToTerrain(terrain, ext.center),
                        radius = ext.radius > 0 ? ext.radius : s.settlementRadius,
                        buildingCount = ext.buildingCount > 0 ? ext.buildingCount : s.buildingsPerTown,
                        name = $"Settlement_{settlements.Count:00}",
                        isTown = (ext.buildingCount > 0 ? ext.buildingCount : s.buildingsPerTown) >= 40,
                        style = style,
                        // Pass per-settlement overrides through
                        lotMinSize = ext.lotMinSize,
                        lotMaxSize = ext.lotMaxSize,
                        buildingHeightMin = ext.buildingHeightMin,
                        buildingHeightMax = ext.buildingHeightMax,
                        roadClearance = ext.roadClearance,
                        padRadius = ext.padRadius
                    });
                }
                return settlements;
            }
    
            var shuffledHubs = new List<Vector3>(hubs);
            Shuffle(rng, shuffledHubs);
    
            // Towns at hubs
            foreach (var h in shuffledHubs)
            {
                if (settlements.Count >= s.maxSettlements) break;
                if ((float)rng.NextDouble() > s.hubTownChance) continue;
    
                Vector3 c = SnapToTerrain(terrain, h);
                if (!IsBuildable(terrain, c, s)) continue;
                if (TooClose(settlements, c, s.hubTownMinSpacing)) continue;
    
                if (DistanceToRoadsXZ(c, roadLines) < s.roadClearance * 1.2f) continue;
    
                settlements.Add(new Settlement
                {
                    center = c,
                    radius = s.settlementRadius,
                    buildingCount = s.buildingsPerTown,
                    name = $"Settlement_{settlements.Count:00}",
                    isTown = s.buildingsPerTown >= 40,
                    style = chosenStyle
                });
            }
    
            // Roadside villages
            if (s.enableRoadsideVillages && settlements.Count < s.maxSettlements)
            {
                for (int i = 0; i < roadLines.Count && settlements.Count < s.maxSettlements; i++)
                {
                    var line = roadLines[i];
                    if (line == null || line.Count < 2) continue;
    
                    float length = PolylineLength(line);
                    if (length < s.roadsideStep * 0.9f) continue;
    
                    for (float d = s.roadsideStep; d < length && settlements.Count < s.maxSettlements; d += s.roadsideStep)
                    {
                        if ((float)rng.NextDouble() > s.roadsideVillageChance) continue;
    
                        Vector3 p = SampleAlong(line, d);
                        Vector3 fwd = ApproxFwdAt(line, p);
                        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
    
                        float side = (rng.NextDouble() < 0.5) ? -1f : 1f;
                        float offset = Mathf.Lerp(s.roadClearance * 1.2f, s.roadClearance * 2.2f, (float)rng.NextDouble());
                        Vector3 c = p + right * (offset * side);
                        c = SnapToTerrain(terrain, c);
    
                        if (!IsBuildable(terrain, c, s)) continue;
                        if (TooClose(settlements, c, s.hubTownMinSpacing * 0.65f)) continue;
    
                        settlements.Add(new Settlement
                        {
                            center = c,
                            radius = Mathf.Lerp(s.settlementRadius * 0.55f, s.settlementRadius * 0.85f, (float)rng.NextDouble()),
                            buildingCount = s.buildingsPerVillage,
                            name = $"Village_{settlements.Count:00}",
                            isTown = false,
                            style = chosenStyle
                        });
                    }
                }
            }
    
            return settlements;
        }
    
        // ------------------------------------------------------------------------
        // MODERN: grid parcels near center (rectangular blocks)
        // ------------------------------------------------------------------------
        private static void BuildModernBlocks(
            Scene scene,
            Terrain terrain,
            List<List<Vector3>> roadLines,
            Transform parent,
            Settlement set,
            SettlementGenSettings s,
            System.Random rng,
            Material buildingMaterial)
        {
            CreateSimpleLandmark(scene, terrain, parent, set.center, s, buildingMaterial, modern: true);
    
            int cells = Mathf.Max(3, s.modernGridCellsPerSide);
            float cell = Mathf.Max(8f, s.modernBlockCellSize);
            float streetGap = Mathf.Clamp(s.modernStreetGap, 0.2f, cell * 0.6f);
    
            Vector3 roadDir = DirectionToNearestRoad(set.center, roadLines);
            if (roadDir.sqrMagnitude < 0.0001f) roadDir = Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(new Vector3(roadDir.x, 0f, roadDir.z), Vector3.up);
    
            int placedCount = 0;
            var placedPos = new List<Vector3>();
            float half = (cells * cell) * 0.5f;
    
            for (int gx = 0; gx < cells; gx++)
            for (int gz = 0; gz < cells; gz++)
            {
                if (placedCount >= set.buildingCount) break;
    
                if ((float)rng.NextDouble() < 0.12f) continue;
    
                float lx = (gx + 0.5f) * cell - half;
                float lz = (gz + 0.5f) * cell - half;
    
                float parcelW = cell - streetGap;
                float parcelD = cell - streetGap;
    
                Vector3 world = set.center + rot * new Vector3(lx, 0f, lz);
                world = SnapToTerrain(terrain, world);
    
                if (Vector2.Distance(new Vector2(world.x, world.z), new Vector2(set.center.x, set.center.z)) > set.radius) continue;
    
                if (!IsBuildable(terrain, world, s)) continue;
                if (DistanceToRoadsXZ(world, roadLines) < s.roadClearance + parcelW * 0.6f) continue;

                // For modern blocks, we check vs placed list too
                if (TooCloseToPlaced(world, placedPos, Mathf.Max(parcelW, parcelD) * 0.8f)) continue;
    
                float padR = Mathf.Max(4f, Mathf.Min(parcelW, parcelD) * 0.65f);
                StampPad(terrain, world, s, padR);
    
                float center01 = Mathf.Clamp01(Vector2.Distance(new Vector2(world.x, world.z), new Vector2(set.center.x, set.center.z)) / Mathf.Max(1f, set.radius));
                float height01 = 1f - center01;
    
                float h = Mathf.Lerp(s.buildingHeightMin * 1.1f, s.buildingHeightMax * 2.6f, Mathf.Clamp01(height01 + (float)rng.NextDouble() * 0.15f));
                float w = parcelW * Mathf.Lerp(0.55f, 0.90f, (float)rng.NextDouble());
                float d = parcelD * Mathf.Lerp(0.55f, 0.90f, (float)rng.NextDouble());
    
                CreateBuilding(scene, terrain, parent, world, w, d, h, rot, buildingMaterial, $"BlockBld_{placedCount:000}", s.coreCommercialPrefabs, rng);
                placedPos.Add(world);
                placedCount++;
            }
        }
    
        // ------------------------------------------------------------------------
        // MEDIEVAL: rings + market + wall ring
        // ------------------------------------------------------------------------
        private static void BuildMedievalTown(
            Scene scene,
            Terrain terrain,
            List<List<Vector3>> roadLines,
            Transform parent,
            Settlement set,
            SettlementGenSettings s,
            System.Random rng,
            Material buildingMaterial,
            Material wallMaterial)
        {
            if (s.medievalAddMarket)
            {
                StampPad(terrain, set.center, s, 18f);
                CreateSimpleLandmark(scene, terrain, parent, set.center, s, buildingMaterial, modern: false);
            }

            int rings = Mathf.Max(2, s.medievalRingCount);
            float ringStep = Mathf.Max(8f, set.radius * Mathf.Clamp(s.medievalRingSpacingFactor, 0.12f, 0.55f));

            int placed = 0;
            var placedPos = new List<Vector3>();
            for (int r = 1; r <= rings && placed < set.buildingCount; r++)
            {
                float rad = Mathf.Min(set.radius * 0.92f, r * ringStep);
                int onRing = Mathf.Max(8, Mathf.RoundToInt((2f * Mathf.PI * rad) / Mathf.Lerp(30f, 48f, (float)rng.NextDouble())));
                int skipped = 0;
                for (int i = 0; i < onRing && placed < set.buildingCount; i++)
                {
                    float a = ((i / (float)onRing) * Mathf.PI * 2f) + ((float)rng.NextDouble() - 0.5f) * 0.25f;

                    Vector3 pos = set.center + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
                    pos += new Vector3(((float)rng.NextDouble() - 0.5f) * 6f, 0f, ((float)rng.NextDouble() - 0.5f) * 6f);

                    pos = SnapToTerrain(terrain, pos);

                    if (!IsBuildable(terrain, pos, s)) { skipped++; continue; }
                    float lot = Mathf.Lerp(s.lotMinSize * 0.8f, s.lotMaxSize * 1.1f, (float)rng.NextDouble());
                    if (DistanceToRoadsXZ(pos, roadLines) < s.roadClearance + lot * 0.55f) continue;

                    if (TooCloseToPlaced(pos, placedPos, lot * 1.1f)) continue; // New proximity check

                    float w = lot * Mathf.Lerp(0.55f, 0.95f, (float)rng.NextDouble());
                    float d = lot * Mathf.Lerp(0.55f, 0.95f, (float)rng.NextDouble());
                    float h = Mathf.Lerp(s.buildingHeightMin * 0.7f, s.buildingHeightMax * 1.4f, (float)rng.NextDouble());

                    StampPad(terrain, pos, s, Mathf.Max(6f, lot * 0.6f));

                    Vector3 toCenter = (set.center - pos); toCenter.y = 0f;
                    Quaternion rot = toCenter.sqrMagnitude < 0.0001f
                        ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f)
                        : Quaternion.LookRotation(toCenter.normalized, Vector3.up);

                    var prefabs = r <= rings / 2 ? s.coreCommercialPrefabs : s.residentialPrefabs;
                    CreateBuilding(scene, terrain, parent, pos, w, d, h, rot, buildingMaterial, $"MedBuilding_{placed:000}", prefabs, rng);
                    placedPos.Add(pos);
                    placed++;
                }
                if (skipped > 5) Debug.LogWarning($"MedievalTown: Skipped {skipped} buildings due to slope/buildability constraints at {set.name}");
            }

            if (s.medievalAddWalls)
                BuildMedievalWallRing(scene, terrain, parent, set.center, set.radius * 0.98f, s.medievalWallThickness, wallMaterial, rng);
        }
    
        private static void BuildMedievalWallRing(
            Scene scene,
            Terrain terrain,
            Transform parent,
            Vector3 center,
            float radius,
            float thickness,
            Material mat,
            System.Random rng)
        {
            int segments = Mathf.Clamp(Mathf.RoundToInt((2f * Mathf.PI * radius) / 18f), 24, 160);
    
            var wallRoot = new GameObject("Walls");
            SceneManager.MoveGameObjectToScene(wallRoot, scene);
            wallRoot.transform.SetParent(parent, true);
    
            for (int i = 0; i < segments; i++)
            {
                float a0 = (i / (float)segments) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
    
                Vector3 p0 = center + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
    
                p0 = SnapToTerrain(terrain, p0);
                p1 = SnapToTerrain(terrain, p1);
    
                Vector3 mid = (p0 + p1) * 0.5f;
                Vector3 dir = (p1 - p0); dir.y = 0f;
                float len = Mathf.Max(2f, dir.magnitude);
                if (dir.sqrMagnitude < 0.0001f) continue;
    
                float h = Mathf.Lerp(6f, 10f, (float)rng.NextDouble());
    
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"Wall_{i:000}";
                SceneManager.MoveGameObjectToScene(seg, scene);
                seg.transform.SetParent(wallRoot.transform, true);
    
                seg.transform.position = new Vector3(mid.x, mid.y + h * 0.5f, mid.z);
                seg.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                seg.transform.localScale = new Vector3(thickness, h, len);
    
                if (mat != null)
                {
                    var r = seg.GetComponent<Renderer>();
                    if (r != null) r.sharedMaterial = mat;
                }
            }
        }
    
        // ------------------------------------------------------------------------
        // SUBURBAN: cul-de-sacs + smaller houses (yards)
        // ------------------------------------------------------------------------
        private static void BuildSuburban(
            Scene scene,
            Terrain terrain,
            List<List<Vector3>> roadLines,
            Transform parent,
            Settlement set,
            SettlementGenSettings s,
            System.Random rng,
            Material buildingMaterial)
        {
            CreateSimpleLandmark(scene, terrain, parent, set.center, s, buildingMaterial, modern: false);
    
            int culCount = Mathf.Max(1, s.suburbanCulDeSacCount);
            float culRad = Mathf.Max(12f, s.suburbanCulDeSacRadius);
    
            var culCenters = new List<Vector3>(culCount);
            int guard = 0;
    
            while (culCenters.Count < culCount && guard++ < 2000)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Pow((float)rng.NextDouble(), 0.8f) * (set.radius * 0.85f);
    
                Vector3 c = set.center + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                c = SnapToTerrain(terrain, c);
    
                if (!IsBuildable(terrain, c, s)) continue;
                if (DistanceToRoadsXZ(c, roadLines) < s.roadClearance * 1.1f) continue;
    
                bool tooClose = false;
                for (int i = 0; i < culCenters.Count; i++)
                {
                    if (Vector2.Distance(new Vector2(c.x, c.z), new Vector2(culCenters[i].x, culCenters[i].z)) < culRad * 1.4f)
                    { tooClose = true; break; }
                }
                if (tooClose) continue;
    
                culCenters.Add(c);
            }
    
            int total = set.buildingCount;
            int perCul = Mathf.Max(6, Mathf.RoundToInt(total / (float)Mathf.Max(1, culCenters.Count)));
    
            int placed = 0;
            var placedPos = new List<Vector3>();
            foreach (var cc in culCenters)
            {
                StampPad(terrain, cc, s, culRad * 0.75f);
    
                int ring = Mathf.Clamp(perCul, 6, 40);
                for (int i = 0; i < ring && placed < total; i++)
                {
                    float a = (i / (float)ring) * Mathf.PI * 2f;
                    Vector3 pos = cc + new Vector3(Mathf.Cos(a) * culRad, 0f, Mathf.Sin(a) * culRad);
                    pos = SnapToTerrain(terrain, pos);
    
                    if (!IsBuildable(terrain, pos, s)) continue;
                    float lot = Mathf.Lerp(s.lotMinSize * 0.65f, s.lotMaxSize * 0.9f, (float)rng.NextDouble());
                    if (DistanceToRoadsXZ(pos, roadLines) < s.roadClearance + lot * 0.55f) continue;
    
                    float yard = Mathf.Max(0f, s.suburbanYardPadding);
                    if (TooCloseToPlaced(pos, placedPos, lot * 1.05f)) continue;
                    float w = lot * Mathf.Lerp(0.45f, 0.80f, (float)rng.NextDouble());
                    float d = lot * Mathf.Lerp(0.45f, 0.80f, (float)rng.NextDouble());
                    float h = Mathf.Lerp(s.buildingHeightMin * 0.35f, s.buildingHeightMax * 0.65f, (float)rng.NextDouble());
    
                    StampPad(terrain, pos, s, Mathf.Max(6f, lot * 0.7f + yard));
    
                    Vector3 toC = (cc - pos); toC.y = 0f;
                    Quaternion rot = toC.sqrMagnitude < 0.0001f
                        ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f)
                        : Quaternion.LookRotation(toC.normalized, Vector3.up);
    
                    CreateBuilding(scene, terrain, parent, pos, w, d, h, rot, buildingMaterial, $"House_{placed:000}", s.residentialPrefabs, rng);
                    placedPos.Add(pos);
                    placed++;
                }
            }
    
            int attempts = 0;
            while (placed < total && attempts++ < total * 30)
            {
                Vector3 pos = RandomPointInCircle(set.center, set.radius * 0.9f, rng);
                pos = SnapToTerrain(terrain, pos);
    
                if (!IsBuildable(terrain, pos, s)) continue;
                float lot = Mathf.Lerp(s.lotMinSize * 0.6f, s.lotMaxSize * 0.9f, (float)rng.NextDouble());
                if (DistanceToRoadsXZ(pos, roadLines) < s.roadClearance + lot * 0.55f) continue;
    
                if (TooCloseToPlaced(pos, placedPos, lot * 1.15f)) continue;
                float w = lot * Mathf.Lerp(0.45f, 0.75f, (float)rng.NextDouble());
                float d = lot * Mathf.Lerp(0.45f, 0.75f, (float)rng.NextDouble());
                float h = Mathf.Lerp(s.buildingHeightMin * 0.35f, s.buildingHeightMax * 0.65f, (float)rng.NextDouble());
    
                StampPad(terrain, pos, s, Mathf.Max(6f, lot * 0.7f + s.suburbanYardPadding));
    
                Vector3 dirToRoad = DirectionToNearestRoad(pos, roadLines);
                Quaternion rot = dirToRoad.sqrMagnitude < 0.0001f
                    ? Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f)
                    : Quaternion.LookRotation(new Vector3(dirToRoad.x, 0f, dirToRoad.z), Vector3.up);
    
                CreateBuilding(scene, terrain, parent, pos, w, d, h, rot, buildingMaterial, $"House_{placed:000}", s.residentialPrefabs, rng);
                placedPos.Add(pos);
                placed++;
            }
        }
    
        // ------------------------------------------------------------------------
        // BUILDING PRIMITIVE
        // ------------------------------------------------------------------------
        private static void CreateBuilding(
            Scene scene,
            Terrain terrain,
            Transform parent,
            Vector3 pos,
            float width,
            float depth,
            float height,
            Quaternion rot,
            Material mat,
            string name,
            GameObject[] prefabs = null,
            System.Random rng = null)
        {
            GameObject b = null;
            if (prefabs != null && prefabs.Length > 0 && rng != null)
            {
                var proto = prefabs[rng.Next(prefabs.Length)];
                if (proto != null)
                {
#if UNITY_EDITOR
                    b = (GameObject)PrefabUtility.InstantiatePrefab(proto);
#else
                    b = GameObject.Instantiate(proto);
#endif
                    if (b != null)
                    {
                        b.name = name;
                        SceneManager.MoveGameObjectToScene(b, scene);
                        b.transform.SetParent(parent, true);

                        // Uniform randomize it slightly
                        float s = Mathf.Lerp(1.2f, 2.0f, (float)rng.NextDouble());
                        b.transform.localScale = proto.transform.localScale * s;

                        float gy = terrain.SampleHeight(pos) + terrain.transform.position.y;
                        b.transform.position = new Vector3(pos.x, gy, pos.z);
                        b.transform.rotation = rot;
                        return;
                    }
                }
            }

            b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            SceneManager.MoveGameObjectToScene(b, scene);
            b.transform.SetParent(parent, true);
    
            float groundY = terrain.SampleHeight(pos) + terrain.transform.position.y;
            b.transform.position = new Vector3(pos.x, groundY + height * 0.5f, pos.z);
            b.transform.rotation = rot;
            b.transform.localScale = new Vector3(width, height, depth);
    
            if (mat != null)
            {
                var r = b.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
        }
    
        private static void CreateSimpleLandmark(
            Scene scene,
            Terrain terrain,
            Transform parent,
            Vector3 center,
            SettlementGenSettings s,
            Material mat,
            bool modern)
        {
            Vector3 c = SnapToTerrain(terrain, center);
    
            // Updated: use settings falloff/blend and enforce padRadius minimum
            StampPad(terrain, c, s, modern ? 16f : 14f);
    
            var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGo.name = modern ? "Landmark_Core" : "Landmark_Market";
            SceneManager.MoveGameObjectToScene(baseGo, scene);
            baseGo.transform.SetParent(parent, true);
    
            float baseH = modern ? 26f : 10f;
            float groundY = terrain.SampleHeight(c) + terrain.transform.position.y;
            baseGo.transform.position = new Vector3(c.x, groundY + baseH * 0.5f, c.z);
            baseGo.transform.localScale = new Vector3(modern ? 20f : 24f, baseH, modern ? 20f : 18f);
    
            if (mat != null)
            {
                var r = baseGo.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
        }
    
        // ------------------------------------------------------------------------
        // TERRAIN / ROAD HELPERS
        // ------------------------------------------------------------------------
        private static bool IsBuildable(Terrain terrain, Vector3 p, SettlementGenSettings s)
        {
            var td = terrain.terrainData;
            Vector3 tp = terrain.transform.position;
            Vector3 ts = td.size;
    
            if (p.x < tp.x || p.z < tp.z || p.x > tp.x + ts.x || p.z > tp.z + ts.z)
                return false;
    
            float slope = SampleSlopeDeg(terrain, p);
            if (slope > 75f) return false; // Relaxed from s.maxSlopeDeg as per user request
    
            float y = terrain.SampleHeight(p) + tp.y;
            float h01 = Mathf.InverseLerp(tp.y, tp.y + ts.y, y);
            if (h01 > s.avoidHighPeaks) return false;
    
            float accept = Mathf.Lerp(1f, 0.25f, Mathf.Pow(h01, Mathf.Lerp(0.6f, 2.2f, s.preferLowlands)));
            float hash = Hash01(p.x, p.z, s.seed);
            return hash <= accept;
        }
    
        private static float Hash01(float x, float z, int seed)
        {
            unchecked
            {
                int xi = Mathf.FloorToInt(x * 10f);
                int zi = Mathf.FloorToInt(z * 10f);
                int h = seed;
                h = h * 31 + xi;
                h = h * 31 + zi;
                h ^= (h << 13);
                h ^= (h >> 17);
                h ^= (h << 5);
                uint uh = (uint)h;
                return (uh % 100000u) / 100000f;
            }
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
    
        private static bool TooClose(List<Settlement> sets, Vector3 c, float minDist)
        {
            Vector2 c2 = new Vector2(c.x, c.z);
            for (int i = 0; i < sets.Count; i++)
            {
                Vector2 s2 = new Vector2(sets[i].center.x, sets[i].center.z);
                if (Vector2.Distance(c2, s2) < minDist) return true;
            }
            return false;
        }
    
        private static float DistanceToRoadsXZ(Vector3 p, List<List<Vector3>> roads)
        {
            if (roads == null || roads.Count == 0) return float.PositiveInfinity;
    
            float best = float.PositiveInfinity;
            Vector2 q = new Vector2(p.x, p.z);
    
            for (int i = 0; i < roads.Count; i++)
            {
                var line = roads[i];
                if (line == null || line.Count < 2) continue;
    
                for (int j = 0; j < line.Count - 1; j++)
                {
                    Vector2 a = new Vector2(line[j].x, line[j].z);
                    Vector2 b = new Vector2(line[j + 1].x, line[j + 1].z);
                    float d = DistPointSeg(q, a, b);
                    if (d < best) best = d;
                }
            }
            return best;
        }
    
        private static Vector3 DirectionToNearestRoad(Vector3 p, List<List<Vector3>> roads)
        {
            if (roads == null || roads.Count == 0) return Vector3.zero;
    
            float best = float.PositiveInfinity;
            Vector3 bestPoint = p;
    
            Vector2 q = new Vector2(p.x, p.z);
    
            for (int i = 0; i < roads.Count; i++)
            {
                var line = roads[i];
                if (line == null || line.Count < 2) continue;
    
                for (int j = 0; j < line.Count - 1; j++)
                {
                    Vector2 a = new Vector2(line[j].x, line[j].z);
                    Vector2 b = new Vector2(line[j + 1].x, line[j + 1].z);
    
                    Vector2 cp = ClosestPointOnSeg(q, a, b);
                    float d = Vector2.Distance(q, cp);
    
                    if (d < best)
                    {
                        best = d;
                        bestPoint = new Vector3(cp.x, p.y, cp.y);
                    }
                }
            }
    
            Vector3 dir = bestPoint - p;
            dir.y = 0f;
            return dir.sqrMagnitude < 0.0001f ? Vector3.zero : dir.normalized;
        }
    
        private static float PolylineLength(List<Vector3> line)
        {
            float len = 0f;
            for (int i = 1; i < line.Count; i++)
                len += Vector3.Distance(line[i - 1], line[i]);
            return len;
        }
    
        private static Vector3 SampleAlong(List<Vector3> line, float dist)
        {
            float acc = 0f;
            for (int i = 1; i < line.Count; i++)
            {
                float seg = Vector3.Distance(line[i - 1], line[i]);
                if (acc + seg >= dist)
                {
                    float t = (dist - acc) / Mathf.Max(0.0001f, seg);
                    return Vector3.Lerp(line[i - 1], line[i], t);
                }
                acc += seg;
            }
            return line[line.Count - 1];
        }
    
        private static Vector3 ApproxFwdAt(List<Vector3> line, Vector3 p)
        {
            int idx = 0;
            float best = float.PositiveInfinity;
            for (int i = 0; i < line.Count; i++)
            {
                float d = (line[i] - p).sqrMagnitude;
                if (d < best) { best = d; idx = i; }
            }
    
            Vector3 fwd;
            if (idx <= 0) fwd = (line[1] - line[0]);
            else if (idx >= line.Count - 1) fwd = (line[line.Count - 1] - line[line.Count - 2]);
            else fwd = (line[idx + 1] - line[idx - 1]);
    
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            return fwd.normalized;
        }
    
        private static Vector3 RandomPointInCircle(Vector3 center, float radius, System.Random rng)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = radius * Mathf.Sqrt((float)rng.NextDouble());
            return center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
    
        private static bool TooCloseToPlaced(Vector3 pos, List<Vector3> placed, float minDist)
        {
            float d2 = minDist * minDist;
            for (int i = 0; i < placed.Count; i++)
            {
                float dx = pos.x - placed[i].x;
                float dz = pos.z - placed[i].z;
                if (dx * dx + dz * dz < d2) return true;
            }
            return false;
        }
    
        private static float DistPointSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, Vector2.Dot(ab, ab));
            t = Mathf.Clamp01(t);
            return Vector2.Distance(p, a + ab * t);
        }
    
        private static Vector2 ClosestPointOnSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, Vector2.Dot(ab, ab));
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }
    
        private static void Shuffle<T>(System.Random rng, List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
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
