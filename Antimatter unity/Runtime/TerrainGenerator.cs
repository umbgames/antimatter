using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Antimatter
{
    public static class TerrainGenerator
    {
        public enum TerrainPreset
        {
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

        /// <summary>
        /// Merges AI-provided SchemaTerrainParams over preset defaults.
        /// Any field with value 0 in the schema falls back to the preset default.
        /// </summary>
        public static TerrainParams MergeSchemaIntoParams(SchemaTerrainParams s, TerrainParams defaults)
        {
            if (s == null) return defaults;

            var p = defaults;

            if (s.heightmapResolution > 0) p.heightmapResolution = s.heightmapResolution;
            if (s.sizeXZ > 0)              p.sizeXZ = s.sizeXZ;
            if (s.heightY > 0)             p.heightY = s.heightY;
            if (s.seed != 0)               p.seed = s.seed;

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

        // ── Core implementation ──
        private static GameObject CreateOrReplaceTerrainInternal(string name, TerrainPreset preset, TerrainParams p, Vector3 position)
        {
#if UNITY_EDITOR
            var old = GameObject.Find(name);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Terrain");
#else
            var old = GameObject.Find(name);
            if (old != null) UnityEngine.Object.Destroy(old);
            var go = new GameObject(name);
#endif

            var terrain = go.AddComponent<Terrain>();
            var collider = go.AddComponent<TerrainCollider>();

            var data = new TerrainData();
            data.heightmapResolution = p.heightmapResolution;
            data.size = new Vector3(p.sizeXZ, p.heightY, p.sizeXZ);

            float[,] heights = BuildHeights(preset, p);
            data.SetHeights(0, 0, heights);

#if UNITY_EDITOR
            string folder = "Assets/GeneratedTerrainData";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "GeneratedTerrainData");
            }
            string assetPath = $"{folder}/{name}_Data.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            AssetDatabase.CreateAsset(data, assetPath);
            AssetDatabase.SaveAssets();
#endif

            terrain.terrainData = data;
            collider.terrainData = data;

            go.transform.position = position;

            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5;

#if UNITY_EDITOR
            var rpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rpAsset != null)
            {
                terrain.materialTemplate = rpAsset.defaultTerrainMaterial;
            }
            else
            {
                terrain.materialTemplate = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Terrain-Standard.mat");
            }

            string biomeStr = p.biome?.ToLowerInvariant() ?? "";
            Color baseColor = new Color(0.36f, 0.55f, 0.28f, 1f); // Grass
            Color steepColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Rock
            Color peakColor = new Color(0.9f, 0.9f, 0.95f, 1f); // Snow
            
            if (biomeStr.Contains("desert") || biomeStr.Contains("sand"))
            {
                baseColor = new Color(0.76f, 0.70f, 0.50f, 1f);
                steepColor = new Color(0.65f, 0.50f, 0.39f, 1f);
                peakColor = new Color(0.85f, 0.70f, 0.55f, 1f);
            }
            else if (biomeStr.Contains("snow") || biomeStr.Contains("arctic") || biomeStr.Contains("ice"))
            {
                baseColor = new Color(0.85f, 0.85f, 0.9f, 1f);
                steepColor = new Color(0.3f, 0.3f, 0.35f, 1f);
                peakColor = new Color(0.98f, 0.98f, 1f, 1f);
            }
            else if (biomeStr.Contains("red") || biomeStr.Contains("mars"))
            {
                baseColor = new Color(0.7f, 0.3f, 0.2f, 1f);
                steepColor = new Color(0.4f, 0.15f, 0.1f, 1f);
                peakColor = new Color(0.8f, 0.4f, 0.3f, 1f);
            }

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
                AssetDatabase.CreateAsset(l, lp);
                return l;
            }

            var baseLayer = MakeLayer("Base", baseColor);
            var steepLayer = MakeLayer("Steep", steepColor);
            var peakLayer = MakeLayer("Peak", peakColor);

            data.terrainLayers = new TerrainLayer[] { baseLayer, steepLayer, peakLayer };

            int alphaRes = data.alphamapResolution;
            float[,,] splats = new float[alphaRes, alphaRes, 3];
            for (int y = 0; y < alphaRes; y++)
            {
                for (int x = 0; x < alphaRes; x++)
                {
                    float normX = x / (float)(alphaRes - 1);
                    float normY = y / (float)(alphaRes - 1);
                    
                    int hx = Mathf.Clamp(Mathf.RoundToInt(normX * (p.heightmapResolution - 1)), 0, p.heightmapResolution - 1);
                    int hy = Mathf.Clamp(Mathf.RoundToInt(normY * (p.heightmapResolution - 1)), 0, p.heightmapResolution - 1);
                    float h = data.GetHeight(hx, hy) / (float)p.heightY;
                    float angle = data.GetSteepness(normX, normY);
                    
                    float steepness = Mathf.InverseLerp(25f, 45f, angle);
                    
                    float peakness = Mathf.InverseLerp(0.6f, 0.8f, h);
                    if (steepness > 0) peakness *= (1f - steepness * 0.8f);

                    float wPeak = peakness;
                    float wSteep = steepness * (1f - wPeak);
                    float wBase = 1f - (wPeak + wSteep);

                    splats[y, x, 0] = Mathf.Max(0, wBase);
                    splats[y, x, 1] = Mathf.Max(0, wSteep);
                    splats[y, x, 2] = Mathf.Max(0, wPeak);
                }
            }
            data.SetAlphamaps(0, 0, splats);

            terrain.Flush();
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrain);
            EditorUtility.SetDirty(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif

            return go;
        }

        public static TerrainPreset GuessPresetFromPrompt(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return TerrainPreset.Hills;

            string p = prompt.ToLowerInvariant();
            bool mountain = p.Contains("mountain") || p.Contains("peaks") || p.Contains("alps");
            bool valley = p.Contains("valley") || p.Contains("canyon") || p.Contains("river");
            bool island = p.Contains("island") || p.Contains("beach") || p.Contains("coast");

            if (island) return TerrainPreset.Island;
            if (mountain && valley) return TerrainPreset.MountainValley;
            if (mountain) return TerrainPreset.Mountains;
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
                case TerrainPreset.Plains:
                    p.heightY = 60;
                    p.baseFrequency = 0.0012f;
                    p.octaves = 4;
                    p.ridgeStrength = 0.0f;
                    p.erosionPasses = 10;
                    break;

                case TerrainPreset.Hills:
                    p.heightY = 120;
                    p.baseFrequency = 0.0015f;
                    p.octaves = 5;
                    p.ridgeStrength = 0.25f;
                    p.erosionPasses = 10;
                    break;

                case TerrainPreset.Mountains:
                    p.heightY = 320;
                    p.baseFrequency = 0.0018f;
                    p.octaves = 6;
                    p.ridgeStrength = 0.85f;
                    p.erosionPasses = 6;
                    break;

                case TerrainPreset.MountainValley:
                    p.heightY = 340;
                    p.baseFrequency = 0.0018f;
                    p.octaves = 6;
                    p.ridgeStrength = 0.85f;
                    p.erosionPasses = 6;
                    p.valleyWidth = 0.18f;
                    p.valleyDepth = 0.65f;
                    break;

                case TerrainPreset.Island:
                    p.heightY = 220;
                    p.baseFrequency = 0.0017f;
                    p.octaves = 5;
                    p.ridgeStrength = 0.55f;
                    p.erosionPasses = 8;
                    p.seaLevel = 0.22f;
                    break;
            }

            return p;
        }

        private static float[,] BuildHeights(TerrainPreset preset, TerrainParams p)
        {
            int res = p.heightmapResolution;
            float[,] h = new float[res, res];

            float ox = Hash01(p.seed, 11) * 10000f;
            float oz = Hash01(p.seed, 23) * 10000f;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (x / (float)(res - 1) - 0.5f);
                    float nz = (z / (float)(res - 1) - 0.5f);

                    float baseH = FractalNoise(nx, nz, ox, oz, p);
                    float ridged = RidgedNoise(nx, nz, ox + 1000f, oz + 2000f, p);
                    float height = Mathf.Lerp(baseH, Mathf.Max(baseH, ridged), p.ridgeStrength);

                    if (preset == TerrainPreset.Island)
                        height = ApplyIslandFalloff(height, nx, nz, p);

                    if (preset == TerrainPreset.MountainValley)
                        height = CarveValley(height, nx, nz, p);

                    h[z, x] = height;
                }
            }

            int passes = Mathf.Clamp(Mathf.RoundToInt(p.erosionPasses), 0, 50);
            for (int i = 0; i < passes; i++)
                SmoothInPlace(h);

            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    h[z, x] = Mathf.Clamp01(h[z, x]);

            return h;
        }

        private static float FractalNoise(float nx, float nz, float ox, float oz, TerrainParams p)
        {
            float amp = 1f;
            float freq = p.baseFrequency;
            float sum = 0f;
            float norm = 0f;

            for (int i = 0; i < p.octaves; i++)
            {
                float v = Mathf.PerlinNoise((nx * 1000f + ox) * freq, (nz * 1000f + oz) * freq);
                sum += v * amp;
                norm += amp;
                amp *= p.persistence;
                freq *= p.lacunarity;
            }

            float h = sum / Mathf.Max(0.0001f, norm);
            h = Mathf.Pow(h, 1.25f);
            return h;
        }

        private static float RidgedNoise(float nx, float nz, float ox, float oz, TerrainParams p)
        {
            float n = FractalNoise(nx, nz, ox, oz, p);
            float r = 1f - Mathf.Abs(2f * n - 1f);
            r = Mathf.Pow(r, 2.2f);
            return r;
        }

        private static float ApplyIslandFalloff(float height, float nx, float nz, TerrainParams p)
        {
            float r = Mathf.Sqrt(nx * nx + nz * nz);
            float falloff = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(r / 0.55f));
            float sea = p.seaLevel;
            float island = height * falloff;
            island = Mathf.Max(island, sea * 0.85f);
            return island;
        }

        private static float CarveValley(float height, float nx, float nz, TerrainParams p)
        {
            float d = Mathf.Abs(nz - nx) / Mathf.Sqrt(2f);
            float width = Mathf.Clamp01(p.valleyWidth);
            float t = Mathf.SmoothStep(width, 0f, d);
            float depth = p.valleyDepth;
            float carved = height - (t * depth);
            return carved;
        }

        private static void SmoothInPlace(float[,] h)
        {
            int res0 = h.GetLength(0);
            int res1 = h.GetLength(1);

            for (int z = 1; z < res0 - 1; z++)
            {
                for (int x = 1; x < res1 - 1; x++)
                {
                    float v = h[z, x] + h[z - 1, x] + h[z + 1, x] + h[z, x - 1] + h[z, x + 1];
                    h[z, x] = v / 5f;
                }
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
