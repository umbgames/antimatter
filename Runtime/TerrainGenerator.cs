using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Antimatter
{
    public static class TerrainGenerator
    {
        public enum TerrainPreset
        {
            Desert,
            Plains,
            Hills,
            Mountains,
            MountainValley,
            Island
        }

        public struct TerrainParams
        {
            public int heightmapResolution;
            public int sizeXZ;
            public int heightY;
            public int seed;

            public int gridSizeX;
            public int gridSizeZ;

            public int octaves;
            public float lacunarity;
            public float persistence;
            public float baseFrequency;

            public float ridgeStrength;
            public float erosionPasses;
            public float seaLevel;
            public float valleyWidth;
            public float valleyDepth;
            public string biome;
        }

        // ── Original preset-based entry (backward compatible) ──
        public static GameObject CreateOrReplaceTerrain(string name, TerrainPreset preset, int seed, Vector3 position)
        {
            var p = DefaultParams(preset, seed);
            return CreateOrReplaceTerrainInternal(name, preset, p, position);
        }

        // ── NEW: AI-controlled entry accepting full params from the schema ──
        public static GameObject CreateOrReplaceTerrain(string name, SchemaTerrainParams schema, string presetHint, Vector3 position)
        {
            TerrainPreset preset = GuessPresetFromPrompt(presetHint ?? "Hills");
            var defaults = DefaultParams(preset, schema?.seed ?? 0);
            var p = MergeSchemaIntoParams(schema, defaults);
            return CreateOrReplaceTerrainInternal(name, preset, p, position);
        }

        public static TerrainParams MergeSchemaIntoParams(SchemaTerrainParams s, TerrainParams defaults)
        {
            if (s == null) return defaults;
            var p = defaults;
            if (s.heightmapResolution > 0) p.heightmapResolution = s.heightmapResolution;
            if (s.sizeXZ > 0)              p.sizeXZ = s.sizeXZ;
            if (s.heightY > 0)             p.heightY = s.heightY;
            if (s.seed != 0)               p.seed = s.seed;
            if (s.gridSizeX > 0)           p.gridSizeX = s.gridSizeX;
            if (s.gridSizeZ > 0)           p.gridSizeZ = s.gridSizeZ;
            if (s.octaves > 0)             p.octaves = s.octaves;
            if (s.lacunarity > 0f)         p.lacunarity = s.lacunarity;
            if (s.persistence > 0f)        p.persistence = s.persistence;
            if (s.baseFrequency > 0f)      p.baseFrequency = s.baseFrequency;
            if (s.ridgeStrength > 0f)      p.ridgeStrength = s.ridgeStrength;
            if (s.erosionPasses > 0f)      p.erosionPasses = s.erosionPasses;
            if (s.seaLevel > 0f)           p.seaLevel = s.seaLevel;
            if (s.valleyWidth > 0f)        p.valleyWidth = s.valleyWidth;
            if (s.valleyDepth > 0f)        p.valleyDepth = s.valleyDepth;
            if (!string.IsNullOrWhiteSpace(s.biome)) p.biome = s.biome;
            return p;
        }

        private static GameObject CreateOrReplaceTerrainInternal(string name, TerrainPreset preset, TerrainParams p, Vector3 position)
        {
            var parentGo = GameObject.Find(name);
            if (parentGo != null)
            {
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(parentGo);
#else
                UnityEngine.Object.Destroy(parentGo);
#endif
            }

            parentGo = new GameObject(name);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(parentGo, "Create Terrain Grid");
#endif
            parentGo.transform.position = position;

            int gridX = Mathf.Max(1, p.gridSizeX);
            int gridZ = Mathf.Max(1, p.gridSizeZ);

            Terrain[,] terrainGrid = new Terrain[gridX, gridZ];

#if UNITY_EDITOR
            string folder = "Assets/GeneratedTerrainData";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "GeneratedTerrainData");

            TerrainLayer MakeLayer(string layerName, Color c)
            {
                var l = new TerrainLayer();
                var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                var px = new Color[256];
                for (int i = 0; i < 256; i++) px[i] = c;
                tex.SetPixels(px);
                tex.Apply();
                string lp = $"{folder}/{name}_{layerName}Layer.asset";
                string tp = $"{folder}/{name}_{layerName}Tex.asset";
                var eL = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lp);
                if (eL != null) AssetDatabase.DeleteAsset(lp);
                var eT = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                if (eT != null) AssetDatabase.DeleteAsset(tp);
                AssetDatabase.CreateAsset(tex, tp);
                l.diffuseTexture = tex;
                l.tileSize = new Vector2(20f, 20f);
                l.smoothness = 0.05f; // Reduce glossiness
                AssetDatabase.CreateAsset(l, lp);
                return l;
            }

            TerrainLayer[] layers = new TerrainLayer[] {
                MakeLayer("Grass", new Color(0.36f, 0.55f, 0.28f, 1f)),
                MakeLayer("Desert", new Color(0.76f, 0.70f, 0.50f, 1f)),
                MakeLayer("Tundra", new Color(0.65f, 0.7f, 0.65f, 1f)),
                MakeLayer("Rock", new Color(0.4f, 0.4f, 0.4f, 1f)),
                MakeLayer("Snow", new Color(0.9f, 0.9f, 0.95f, 1f))
            };
#endif

            for (int tz = 0; tz < gridZ; tz++)
            {
                for (int tx = 0; tx < gridX; tx++)
                {
                    string tileName = gridX > 1 || gridZ > 1 ? $"{name}_{tx}_{tz}" : $"{name}_Tile";
                    var go = new GameObject(tileName);
                    go.transform.SetParent(parentGo.transform);

                    var terrain = go.AddComponent<Terrain>();
                    var collider = go.AddComponent<TerrainCollider>();

                    var data = new TerrainData();
                    data.heightmapResolution = p.heightmapResolution;
                    data.size = new Vector3(p.sizeXZ, p.heightY, p.sizeXZ);

                    float[,] heights = BuildHeights(preset, p, tx, tz, gridX, gridZ);
                    data.SetHeights(0, 0, heights);

#if UNITY_EDITOR
                    string assetPath = $"{folder}/{tileName}_Data.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);
                    if (existing != null) AssetDatabase.DeleteAsset(assetPath);
                    AssetDatabase.CreateAsset(data, assetPath);
#endif

                    terrain.terrainData = data;
                    collider.terrainData = data;
                    go.transform.localPosition = new Vector3(tx * p.sizeXZ, 0, tz * p.sizeXZ);
                    terrain.drawInstanced = true;
                    terrain.heightmapPixelError = 5;

                    terrainGrid[tx, tz] = terrain;

#if UNITY_EDITOR
                    var rpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                    if (rpAsset != null) terrain.materialTemplate = rpAsset.defaultTerrainMaterial;
                    else terrain.materialTemplate = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Terrain-Standard.mat");

                    data.terrainLayers = layers;
                    int alphaRes = data.alphamapResolution;
                    float[,,] splats = new float[alphaRes, alphaRes, 5];

                    float tempOx = Hash01(p.seed, 100) * 1000f;
                    float tempOz = Hash01(p.seed, 101) * 1000f;
                    float moistOx = Hash01(p.seed, 200) * 1000f;
                    float moistOz = Hash01(p.seed, 201) * 1000f;

                    for (int y = 0; y < alphaRes; y++)
                    {
                        for (int x = 0; x < alphaRes; x++)
                        {
                            float globalX = (tx * (alphaRes - 1) + x);
                            float globalZ = (tz * (alphaRes - 1) + y);
                            float totalResX = gridX * (alphaRes - 1);
                            float totalResZ = gridZ * (alphaRes - 1);

                            float normX = totalResX > 0 ? globalX / totalResX : 0;
                            float normY = totalResZ > 0 ? globalZ / totalResZ : 0;

                            float localNormX = x / (float)(alphaRes - 1);
                            float localNormY = y / (float)(alphaRes - 1);

                            int hx = Mathf.Clamp(Mathf.RoundToInt(localNormX * (p.heightmapResolution - 1)), 0, p.heightmapResolution - 1);
                            int hy = Mathf.Clamp(Mathf.RoundToInt(localNormY * (p.heightmapResolution - 1)), 0, p.heightmapResolution - 1);
                            float h = data.GetHeight(hx, hy) / (float)p.heightY;
                            float angle = data.GetSteepness(localNormX, localNormY);

                            float temp = Mathf.PerlinNoise(normX * 3f * gridX + tempOx, normY * 3f * gridZ + tempOz);
                            float moist = Mathf.PerlinNoise(normX * 4f * gridX + moistOx, normY * 4f * gridZ + moistOz);

                            temp -= h * 0.4f;

                            float steepness = Mathf.InverseLerp(25f, 45f, angle);
                            float peakness = Mathf.InverseLerp(0.6f, 0.8f, h);
                            if (temp < 0.2f) peakness = Mathf.Max(peakness, 0.4f);
                            if (steepness > 0) peakness *= (1f - steepness * 0.8f);

                            float wPeak = peakness;
                            float wSteep = steepness * (1f - wPeak);
                            float wBase = 1f - (wPeak + wSteep);

                            float dGrass = new Vector2(temp - 0.5f, moist - 0.7f).magnitude;
                            float dDesert = new Vector2(temp - 0.8f, moist - 0.2f).magnitude;
                            float dTundra = new Vector2(temp - 0.2f, moist - 0.5f).magnitude;

                            float wG = 1f / (dGrass + 0.001f);
                            float wD = 1f / (dDesert + 0.001f);
                            float wT = 1f / (dTundra + 0.001f);
                            float sumW = wG + wD + wT;

                            splats[y, x, 0] = (wG / sumW) * wBase;
                            splats[y, x, 1] = (wD / sumW) * wBase;
                            splats[y, x, 2] = (wT / sumW) * wBase;
                            splats[y, x, 3] = wSteep;
                            splats[y, x, 4] = wPeak;
                        }
                    }
                    data.SetAlphamaps(0, 0, splats);
                    terrain.Flush();
                    EditorUtility.SetDirty(data);
                    EditorUtility.SetDirty(terrain);
#endif
                }
            }

            var foliageParent = new GameObject("AI_Foliage");
            foliageParent.transform.SetParent(parentGo.transform);

            for (int tz = 0; tz < gridZ; tz++)
            {
                for (int tx = 0; tx < gridX; tx++)
                {
                    Terrain t = terrainGrid[tx, tz];
                    Terrain left = tx > 0 ? terrainGrid[tx - 1, tz] : null;
                    Terrain top = tz < gridZ - 1 ? terrainGrid[tx, tz + 1] : null;
                    Terrain right = tx < gridX - 1 ? terrainGrid[tx + 1, tz] : null;
                    Terrain bottom = tz > 0 ? terrainGrid[tx, tz - 1] : null;
                    t.SetNeighbors(left, top, right, bottom);
                }
            }

#if UNITY_EDITOR
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
            return parentGo;
        }

        public static void PlaceNatureOnAllTerrains(int seed = 1337)
        {
            var terrains = UnityEngine.Object.FindObjectsOfType<Terrain>();
            if (terrains == null || terrains.Length == 0) return;

            var foliageParent = GameObject.Find("AI_Foliage");
            if (foliageParent != null) UnityEngine.Object.DestroyImmediate(foliageParent);
            foliageParent = new GameObject("AI_Foliage");

            int index = 0;
            foreach (var t in terrains)
            {
                TerrainParams p = new TerrainParams();
                p.seed = seed;
                p.sizeXZ = (int)t.terrainData.size.x;
                GenerateFoliage(t, p, index, index, foliageParent.transform);
                index++;
            }
        }

        private static void GenerateFoliage(Terrain terrain, TerrainParams p, int tx, int tz, Transform parent)
        {
            var data = terrain.terrainData;
            int size = data.alphamapResolution;
            float[,,] alphamaps = data.GetAlphamaps(0, 0, size, size);

            System.Random rng = new System.Random(p.seed ^ 8472 ^ (tx * 123) ^ (tz * 321));
            int treeCount = Mathf.RoundToInt(p.sizeXZ * p.sizeXZ * 0.005f);

            for (int i = 0; i < treeCount; i++)
            {
                float nx = (float)rng.NextDouble();
                float nz = (float)rng.NextDouble();

                int ax = Mathf.Clamp(Mathf.RoundToInt(nx * (size - 1)), 0, size - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt(nz * (size - 1)), 0, size - 1);

                float wGrass = alphamaps[az, ax, 0];
                float wDesert = alphamaps[az, ax, 1];
                float wTundra = alphamaps[az, ax, 2];
                float wSteep = alphamaps[az, ax, 3];
                float wPeak = alphamaps[az, ax, 4];

                if (wSteep > 0.3f || wPeak > 0.3f) continue;

                float worldX = terrain.transform.position.x + nx * data.size.x;
                float worldZ = terrain.transform.position.z + nz * data.size.z;
                float worldY = terrain.SampleHeight(new Vector3(worldX, 0, worldZ)) + terrain.transform.position.y;

                GameObject tree = null;
                if (wGrass > 0.5f && rng.NextDouble() > 0.1f)
                {
                    tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tree.transform.localScale = new Vector3(1f, 3f + (float)rng.NextDouble() * 4f, 1f);
                    var rend = tree.GetComponent<Renderer>();
                    if (rend) rend.sharedMaterial.color = new Color(0.2f, 0.4f, 0.15f);
                }
                else if (wDesert > 0.5f && rng.NextDouble() > 0.95f)
                {
                    tree = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    tree.transform.localScale = new Vector3(0.5f, 1.5f + (float)rng.NextDouble() * 1.5f, 0.5f);
                    var rend = tree.GetComponent<Renderer>();
                    if (rend) rend.sharedMaterial.color = new Color(0.4f, 0.6f, 0.3f);
                }
                else if (wTundra > 0.5f && rng.NextDouble() > 0.7f)
                {
                    tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tree.transform.localScale = new Vector3(0.6f, 2f + (float)rng.NextDouble() * 2f, 0.6f);
                    var rend = tree.GetComponent<Renderer>();
                    if (rend) rend.sharedMaterial.color = new Color(0.25f, 0.3f, 0.25f);
                }

                if (tree != null)
                {
                    tree.transform.SetParent(parent);
                    tree.transform.position = new Vector3(worldX, worldY + tree.transform.localScale.y, worldZ);
                    var col = tree.GetComponent<Collider>();
                    if (col != null) UnityEngine.Object.DestroyImmediate(col);
                }
            }
        }

        public static TerrainPreset GuessPresetFromPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return TerrainPreset.Hills;
            string p = prompt.ToLowerInvariant();
            if (p.Contains("island") || p.Contains("beach") || p.Contains("coast")) return TerrainPreset.Island;
            if ((p.Contains("mountain") || p.Contains("peaks")) && (p.Contains("valley") || p.Contains("canyon"))) return TerrainPreset.MountainValley;
            if (p.Contains("mountain") || p.Contains("peaks")) return TerrainPreset.Mountains;
            if (p.Contains("plains") || p.Contains("flat")) return TerrainPreset.Plains;
            return TerrainPreset.Hills;
        }

        public static TerrainParams DefaultParams(TerrainPreset preset, int seed)
        {
            var p = new TerrainParams
            {
                heightmapResolution = 513,
                sizeXZ = 800,
                heightY = 220,
                seed = seed == 0 ? 1337 : seed,
                gridSizeX = 1,
                gridSizeZ = 1,
                octaves = 5,
                lacunarity = 2.05f,
                persistence = 0.52f,
                baseFrequency = 0.0016f,
                ridgeStrength = 0.75f,
                erosionPasses = 8,
                seaLevel = 0.18f,
                valleyWidth = 0.16f,
                valleyDepth = 0.55f
            };
            switch (preset)
            {
                case TerrainPreset.Desert: p.biome = "Desert"; p.heightY = 100; p.baseFrequency = 0.0008f; p.octaves = 4; p.ridgeStrength = 0.1f; p.erosionPasses = 15; break;
                case TerrainPreset.Plains: p.heightY = 60; p.baseFrequency = 0.0012f; p.octaves = 4; p.ridgeStrength = 0.0f; p.erosionPasses = 10; break;
                case TerrainPreset.Hills: p.heightY = 120; p.baseFrequency = 0.0015f; p.octaves = 5; p.ridgeStrength = 0.25f; p.erosionPasses = 10; break;
                case TerrainPreset.Mountains: p.heightY = 320; p.baseFrequency = 0.0018f; p.octaves = 6; p.ridgeStrength = 0.85f; p.erosionPasses = 6; break;
                case TerrainPreset.MountainValley: p.heightY = 340; p.baseFrequency = 0.0018f; p.octaves = 6; p.ridgeStrength = 0.85f; p.erosionPasses = 6; p.valleyWidth = 0.18f; p.valleyDepth = 0.65f; break;
                case TerrainPreset.Island: p.heightY = 220; p.baseFrequency = 0.0017f; p.octaves = 5; p.ridgeStrength = 0.55f; p.erosionPasses = 8; p.seaLevel = 0.22f; break;
            }
            return p;
        }

        private static float[,] BuildHeights(TerrainPreset preset, TerrainParams p, int tx, int tz, int gridX, int gridZ)
        {
            int res = p.heightmapResolution;
            float[,] h = new float[res, res];

            float ox = Hash01(p.seed, 11) * 10000f;
            float oz = Hash01(p.seed, 23) * 10000f;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float globalX = (tx * (res - 1) + x);
                    float globalZ = (tz * (res - 1) + z);
                    float totalResX = gridX * (res - 1);
                    float totalResZ = gridZ * (res - 1);

                    float nx = (globalX / totalResX) - 0.5f;
                    float nz = (globalZ / totalResZ) - 0.5f;

                    float baseH = DomainWarp(nx, nz, ox, oz, p);
                    float ridged = RidgedNoise(nx, nz, ox + 1000f, oz + 2000f, p);
                    float height = Mathf.Lerp(baseH, Mathf.Max(baseH, ridged), p.ridgeStrength);

                    if (preset == TerrainPreset.Island) height = ApplyIslandFalloff(height, nx, nz, p);
                    if (preset == TerrainPreset.MountainValley) height = CarveValley(height, nx, nz, p);
                    h[z, x] = height;
                }
            }

            h = ApplyHydraulicErosion(h, p);

            int smoothPasses = Mathf.Clamp(Mathf.RoundToInt(p.erosionPasses * 0.2f), 1, 5);
            for (int i = 0; i < smoothPasses; i++) SmoothInPlace(h);

            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    h[z, x] = Mathf.Clamp01(h[z, x]);

            return h;
        }

        private static float DomainWarp(float nx, float nz, float ox, float oz, TerrainParams p)
        {
            float qx = FractalNoise(nx + 10.3f, nz + 5.2f, ox, oz, p, 3);
            float qz = FractalNoise(nx - 2.5f, nz + 12.1f, ox, oz, p, 3);
            float rx = FractalNoise(nx + 4.0f * qx + 1.7f, nz + 4.0f * qz + 9.2f, ox, oz, p, 3);
            float rz = FractalNoise(nx + 4.0f * qx + 8.3f, nz + 4.0f * qz + 2.8f, ox, oz, p, 3);
            return FractalNoise(nx + 4.0f * rx, nz + 4.0f * rz, ox, oz, p);
        }

        private static float FractalNoise(float nx, float nz, float ox, float oz, TerrainParams p, int octaveOverride = -1)
        {
            float amp = 1f; float freq = p.baseFrequency; float sum = 0f; float norm = 0f;
            int octaves = octaveOverride > 0 ? octaveOverride : p.octaves;
            for (int i = 0; i < octaves; i++)
            {
                float v = Mathf.PerlinNoise((nx * 1000f + ox) * freq, (nz * 1000f + oz) * freq);
                sum += v * amp; norm += amp; amp *= p.persistence; freq *= p.lacunarity;
            }
            float h = sum / Mathf.Max(0.0001f, norm);
            return Mathf.Pow(h, 1.25f);
        }

        private static float RidgedNoise(float nx, float nz, float ox, float oz, TerrainParams p)
        {
            float n = FractalNoise(nx, nz, ox, oz, p);
            float r = 1f - Mathf.Abs(2f * n - 1f);
            return Mathf.Pow(r, 2.2f);
        }

        private static float ApplyIslandFalloff(float height, float nx, float nz, TerrainParams p)
        {
            float r = Mathf.Sqrt(nx * nx + nz * nz);
            float falloff = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(r / 0.55f));
            float island = height * falloff;
            return Mathf.Max(island, p.seaLevel * 0.85f);
        }

        private static float CarveValley(float height, float nx, float nz, TerrainParams p)
        {
            float d = Mathf.Abs(nz - nx) / Mathf.Sqrt(2f);
            float t = Mathf.SmoothStep(Mathf.Clamp01(p.valleyWidth), 0f, d);
            return height - (t * p.valleyDepth);
        }

        private static void SmoothInPlace(float[,] h)
        {
            int res0 = h.GetLength(0); int res1 = h.GetLength(1);
            for (int z = 1; z < res0 - 1; z++)
                for (int x = 1; x < res1 - 1; x++)
                    h[z, x] = (h[z, x] + h[z - 1, x] + h[z + 1, x] + h[z, x - 1] + h[z, x + 1]) / 5f;
        }

        private static float[,] ApplyHydraulicErosion(float[,] h, TerrainParams p)
        {
            int res = h.GetLength(0);
            int iterations = (int)(res * res * Mathf.Max(p.erosionPasses, 1f) * 0.25f); 
            if (iterations <= 0) return h;

            float[] map = new float[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++) map[y * res + x] = h[y, x];

            System.Random rng = new System.Random(p.seed ^ 12345);
            float inertia = 0.05f; float capacity = 4.0f; float deposition = 0.1f;
            float erosion = 0.1f; float evaporation = 0.05f; float minSlope = 0.01f;

            for (int iter = 0; iter < iterations; iter++)
            {
                float posX = rng.Next(1, res - 2); float posY = rng.Next(1, res - 2);
                float dirX = 0, dirY = 0; float speed = 1.0f; float water = 1.0f; float sediment = 0.0f;

                for (int step = 0; step < 30; step++)
                {
                    int nodeX = (int)posX; int nodeY = (int)posY;
                    int dropIndex = nodeY * res + nodeX;
                    float u = posX - nodeX; float v = posY - nodeY;

                    float h00 = map[dropIndex]; float h10 = map[dropIndex + 1];
                    float h01 = map[dropIndex + res]; float h11 = map[dropIndex + res + 1];

                    float gradX = (h10 - h00) * (1 - v) + (h11 - h01) * v;
                    float gradY = (h01 - h00) * (1 - u) + (h11 - h10) * u;
                    dirX = (dirX * inertia - gradX * (1 - inertia));
                    dirY = (dirY * inertia - gradY * (1 - inertia));
                    float len = Mathf.Sqrt(dirX * dirX + dirY * dirY);
                    if (len != 0) { dirX /= len; dirY /= len; }

                    posX += dirX; posY += dirY;
                    if (posX < 1 || posX >= res - 2 || posY < 1 || posY >= res - 2) break;

                    int newX = (int)posX; int newY = (int)posY;
                    float newU = posX - newX; float newV = posY - newY;
                    int newIndex = newY * res + newX;

                    float newHeight = map[newIndex] * (1 - newU) * (1 - newV) + map[newIndex + 1] * newU * (1 - newV) + map[newIndex + res] * (1 - newU) * newV + map[newIndex + res + 1] * newU * newV;
                    float curHeight = h00 * (1 - u) * (1 - v) + h10 * u * (1 - v) + h01 * (1 - u) * v + h11 * u * v;

                    float deltaHeight = newHeight - curHeight;
                    float c = Mathf.Max(-deltaHeight, minSlope) * speed * water * capacity;

                    if (sediment > c || deltaHeight > 0)
                    {
                        float amountToDeposit = (deltaHeight > 0) ? Mathf.Min(deltaHeight, sediment) : (sediment - c) * deposition;
                        sediment -= amountToDeposit;
                        map[dropIndex] += amountToDeposit * (1 - u) * (1 - v); map[dropIndex + 1] += amountToDeposit * u * (1 - v);
                        map[dropIndex + res] += amountToDeposit * (1 - u) * v; map[dropIndex + res + 1] += amountToDeposit * u * v;
                    }
                    else
                    {
                        float amountToErode = Mathf.Min((c - sediment) * erosion, -deltaHeight);
                        sediment += amountToErode;
                        map[dropIndex] -= amountToErode * (1 - u) * (1 - v); map[dropIndex + 1] -= amountToErode * u * (1 - v);
                        map[dropIndex + res] -= amountToErode * (1 - u) * v; map[dropIndex + res + 1] -= amountToErode * u * v;
                    }
                    speed = Mathf.Sqrt(Mathf.Max(0, speed * speed + deltaHeight * 4)); water *= (1 - evaporation);
                }
            }
            for(int y = 0; y < res; y++) for(int x = 0; x < res; x++) h[y, x] = map[y * res + x];
            return h;
        }

        private static float Hash01(int seed, int salt)
        {
            unchecked { int h = unchecked((int)2166136261); h = (h ^ seed) * 16777619; h = (h ^ salt) * 16777619; return (h & 0x00FFFFFF) / 16777216f; }
        }
    }
}
