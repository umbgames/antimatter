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
            public int heightmapResolution; // e.g. 513
            public int sizeXZ;              // terrain width/length in meters
            public int heightY;             // max height in meters
            public int seed;

            // Noise
            public int octaves;
            public float lacunarity;
            public float persistence;
            public float baseFrequency;

            // Shaping
            public float ridgeStrength;     // 0..1
            public float erosionPasses;     // 0..50 (cheap smoothing passes)
            public float seaLevel;          // 0..1 (normalized)
            public float valleyWidth;       // 0..1
            public float valleyDepth;       // 0..1
        }

        public static GameObject CreateOrReplaceTerrain(string name, TerrainPreset preset, int seed, Vector3 position)
        {
#if UNITY_EDITOR
            // Remove old terrain with same name (optional)
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

            var p = DefaultParams(preset, seed);

            var data = new TerrainData();
            data.heightmapResolution = p.heightmapResolution;
            data.size = new Vector3(p.sizeXZ, p.heightY, p.sizeXZ);

            float[,] heights = BuildHeights(preset, p);
            data.SetHeights(0, 0, heights);

#if UNITY_EDITOR
            // Save TerrainData as a project asset so Unity can render it
            string folder = "Assets/GeneratedTerrainData";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets", "GeneratedTerrainData");
            }
            string assetPath = $"{folder}/{name}_Data.asset";
            // Remove old asset at same path if it exists
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

            // Basic terrain settings
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5;

#if UNITY_EDITOR
            // Assign default terrain material for the current render pipeline
            var rpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            if (rpAsset != null)
            {
                // URP/HDRP: use the pipeline's default terrain material
                terrain.materialTemplate = rpAsset.defaultTerrainMaterial;
            }
            else
            {
                // Built-in RP: use the built-in terrain standard material
                terrain.materialTemplate = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Terrain-Standard.mat");
            }

            // Create a default terrain layer so the terrain has something visible to render
            var defaultLayer = new TerrainLayer();
            var defaultTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var greenColor = new Color(0.36f, 0.55f, 0.28f, 1f); // natural grass green
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = greenColor;
            defaultTex.SetPixels(pixels);
            defaultTex.Apply();

            string layerPath = $"{folder}/{name}_DefaultLayer.asset";
            string texPath = $"{folder}/{name}_DefaultTex.asset";

            var existingLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (existingLayer != null) AssetDatabase.DeleteAsset(layerPath);
            var existingTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (existingTex != null) AssetDatabase.DeleteAsset(texPath);

            AssetDatabase.CreateAsset(defaultTex, texPath);
            defaultLayer.diffuseTexture = defaultTex;
            defaultLayer.tileSize = new Vector2(10f, 10f);
            AssetDatabase.CreateAsset(defaultLayer, layerPath);
            data.terrainLayers = new TerrainLayer[] { defaultLayer };

            // Force refresh
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

        private static TerrainParams DefaultParams(TerrainPreset preset, int seed)
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

            // Deterministic offsets from seed
            float ox = Hash01(p.seed, 11) * 10000f;
            float oz = Hash01(p.seed, 23) * 10000f;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (x / (float)(res - 1) - 0.5f);
                    float nz = (z / (float)(res - 1) - 0.5f);

                    float baseH = FractalNoise(nx, nz, ox, oz, p);

                    // Ridge noise for mountains
                    float ridged = RidgedNoise(nx, nz, ox + 1000f, oz + 2000f, p);
                    float height = Mathf.Lerp(baseH, Mathf.Max(baseH, ridged), p.ridgeStrength);

                    // Preset shaping
                    if (preset == TerrainPreset.Island)
                        height = ApplyIslandFalloff(height, nx, nz, p);

                    if (preset == TerrainPreset.MountainValley)
                        height = CarveValley(height, nx, nz, p);

                    h[z, x] = height;
                }
            }

            // Cheap “erosion”: repeated smoothing (fast, decent)
            int passes = Mathf.Clamp(Mathf.RoundToInt(p.erosionPasses), 0, 50);
            for (int i = 0; i < passes; i++)
                SmoothInPlace(h);

            // Clamp to [0..1]
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

            // Recenter and compress a bit so plains stay calmer
            h = Mathf.Pow(h, 1.25f);
            return h;
        }

        private static float RidgedNoise(float nx, float nz, float ox, float oz, TerrainParams p)
        {
            // Ridged: 1 - abs(2n-1)
            float n = FractalNoise(nx, nz, ox, oz, p);
            float r = 1f - Mathf.Abs(2f * n - 1f);
            r = Mathf.Pow(r, 2.2f);
            return r;
        }

        private static float ApplyIslandFalloff(float height, float nx, float nz, TerrainParams p)
        {
            float r = Mathf.Sqrt(nx * nx + nz * nz); // 0..~0.7
            float falloff = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(r / 0.55f));
            float sea = p.seaLevel;

            float island = height * falloff;
            island = Mathf.Max(island, sea * 0.85f); // keep shallow sea floor
            return island;
        }

        private static float CarveValley(float height, float nx, float nz, TerrainParams p)
        {
            // Carve a valley along a diagonal “river” axis for determinism.
            // Distance to line y=x in normalized coords:
            float d = Mathf.Abs(nz - nx) / Mathf.Sqrt(2f);

            float width = Mathf.Clamp01(p.valleyWidth);
            float t = Mathf.SmoothStep(width, 0f, d);  // 1 near centerline
            float depth = p.valleyDepth;

            // Lower terrain near valley centerline
            float carved = height - (t * depth);
            return carved;
        }

        private static void SmoothInPlace(float[,] h)
        {
            int res0 = h.GetLength(0);
            int res1 = h.GetLength(1);

            // One pass of 5-tap average (center + 4 neighbors)
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
