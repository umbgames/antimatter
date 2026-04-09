// using System;
// using System.Collections.Generic;
// using UnityEditor;
// using UnityEngine;

namespace Antimatter
{
    
    // public static class TerrainEditor
    // {
    //     // ----------------------------- PRESETS -----------------------------
    //     public enum TerrainPreset
    //     {
    //         Plains,
    //         Hills,
    //         Mountains,
    //         MountainValley,
    //         Island,
    //         Plateau,
    //         Badlands,
    //         Archipelago,
    //         Dunes,
    //         Cliffs
    //     }
    
    //     // ----------------------------- FORMATIONS (STACKABLE) -----------------------------
    //     public enum FormationType
    //     {
    //         BaseFractal,
    //         RidgeMountains,
    //         RollingHills,
    //         Plateau,
    //         CliffBands,
    //         Dunes,
    //         BadlandsTerraces,
    //         Craters,
    //         NoiseDetail,
    //         IslandFalloff,
    //         ArchipelagoFalloff
    //     }
    
    //     [Serializable]
    //     public class FormationLayer
    //     {
    //         public bool enabled = true;
    //         public FormationType type = FormationType.BaseFractal;
    
    //         [Range(0f, 1f)] public float strength = 0.6f;
    
    //         // Generic knobs reused per layer
    //         [Range(0.0001f, 0.05f)] public float frequency = 0.002f; // for layers that need it
    //         [Range(0f, 40f)] public float paramA = 0.5f;             // width/radius/steps/etc.
    //         [Range(0f, 1f)] public float paramB = 0.5f;              // depth/softness/etc.
    //     }
    
    //     // ----------------------------- RIVERS -----------------------------
    //     public enum Edge { Left, Right, Top, Bottom }
    
    //     [Serializable]
    //     public class RiverParams
    //     {
    //         public bool enabled = false;
    
    //         public Edge startEdge = Edge.Left;
    //         public Edge endEdge = Edge.Right;
    
    //         [Range(4, 256)] public int points = 64;             // polyline points
    //         [Range(0f, 1f)] public float meander = 0.35f;       // sideways wiggle
    //         [Range(0.001f, 0.05f)] public float meanderFreq = 0.01f;
    
    //         [Range(0.001f, 0.25f)] public float width = 0.04f;  // normalized terrain width
    //         [Range(0f, 1f)] public float depth = 0.35f;         // normalized height subtraction
    //         [Range(0f, 1f)] public float bankSmooth = 0.55f;    // 0 sharp, 1 smooth banks
    
    //         public bool addWaterPlane = true;
    //         [Range(0f, 1f)] public float waterLevel = 0.22f;    // normalized height
    //         public string waterObjectName = "Water";
    //     }
    
    //     // ----------------------------- TREES -----------------------------
    //     [Serializable]
    //     public class TreeRule
    //     {
    //         public bool enabled = true;
    //         public string name = "Tree Rule";
    
    //         public GameObject prefab;
    
    //         [Range(0f, 1f)] public float density = 0.2f;       // probability per sample
    //         [Range(1, 10)] public int samplesPerCell = 2;      // more = denser, slower
    
    //         [Range(0f, 1f)] public float minHeight = 0.2f;     // normalized height
    //         [Range(0f, 1f)] public float maxHeight = 0.9f;
    //         [Range(0f, 60f)] public float maxSlopeDeg = 30f;
    
    //         [Range(0.2f, 2f)] public float minScale = 0.9f;
    //         [Range(0.2f, 2f)] public float maxScale = 1.3f;
    
    //         public int seedOffset = 1000;
    //         public Color color = Color.white;
    //         public Color lightmapColor = Color.white;
    //     }
    
    //     // ----------------------------- MAIN PARAMS -----------------------------
    //     [Serializable]
    //     public class TerrainParams
    //     {
    //         public int heightmapResolution = 513;
    //         public int sizeXZ = 800;
    //         public int heightY = 220;
    //         public int seed = 1337;
    
    //         // Base noise
    //         public int octaves = 5;
    //         public float lacunarity = 2.05f;
    //         public float persistence = 0.52f;
    //         public float baseFrequency = 0.0016f;
    
    //         // Warp
    //         public float warpStrength = 0.18f; // 0..0.6
    //         public float warpFrequency = 0.006f;
    
    //         // Shaping
    //         public float heightPow = 1.25f;    // 0.6..2.5
    //         public float seaLevel = 0.18f;     // 0..1
    
    //         // Smoothing
    //         public int smoothPasses = 8;       // 0..60
    //         public float slopeLimit = 0.0f;    // 0..1 (optional)
    
    //         // Valley (simple)
    //         public float valleyWidth = 0.18f;  // 0..0.5
    //         public float valleyDepth = 0.65f;  // 0..1
    //     }
    
    //     // ----------------------------- PUBLIC API -----------------------------
    //     public static GameObject CreateOrReplaceTerrain(
    //         string name,
    //         TerrainPreset preset,
    //         int seed,
    //         Vector3 position,
    //         TerrainParams p,
    //         List<FormationLayer> formations,
    //         RiverParams river,
    //         List<TreeRule> treeRules)
    //     {
    //         // Remove old
    //         var old = GameObject.Find(name);
    //         if (old != null) Undo.DestroyObjectImmediate(old);
    
    //         // Create GO + components
    //         var go = new GameObject(name);
    //         Undo.RegisterCreatedObjectUndo(go, "Create Terrain");
    
    //         var terrain = go.AddComponent<Terrain>();
    //         var collider = go.AddComponent<TerrainCollider>();
    
    //         // Params
    //         p.seed = (seed == 0) ? 1337 : seed;
    
    //         // Data
    //         var data = new TerrainData
    //         {
    //             heightmapResolution = p.heightmapResolution,
    //             size = new Vector3(p.sizeXZ, p.heightY, p.sizeXZ)
    //         };
    
    //         // Heights
    //         float[,] heights = BuildHeights(preset, p, formations, river);
    //         data.SetHeights(0, 0, heights);
    
    //         // Trees
    //         ApplyTrees(data, heights, p, treeRules);
    
    //         // Assign
    //         terrain.terrainData = data;
    //         collider.terrainData = data;
    
    //         // Place
    //         go.transform.position = position;
    
    //         // Terrain rendering tweaks
    //         terrain.drawInstanced = true;
    //         terrain.heightmapPixelError = 5;
    //         terrain.basemapDistance = 2000f;
    
    //         // Water plane
    //         if (river != null && river.enabled && river.addWaterPlane)
    //             EnsureWaterPlane(go, river, p);
    
    //         // Make it obvious it exists
    //         Selection.activeObject = go;
    //         EditorGUIUtility.PingObject(go);
    //         SceneView.lastActiveSceneView?.FrameSelected();
    //         Debug.Log($"[TerrainEditor] Created terrain '{go.name}' at {go.transform.position}");
    
    //         return go;
    //     }
    
    //     public static TerrainParams DefaultParams(TerrainPreset preset, int seed)
    //     {
    //         var p = new TerrainParams
    //         {
    //             heightmapResolution = 513,
    //             sizeXZ = 800,
    //             heightY = 220,
    //             seed = seed == 0 ? 1337 : seed,
    
    //             octaves = 5,
    //             lacunarity = 2.05f,
    //             persistence = 0.52f,
    //             baseFrequency = 0.0016f,
    
    //             warpStrength = 0.18f,
    //             warpFrequency = 0.006f,
    
    //             heightPow = 1.25f,
    //             seaLevel = 0.18f,
    
    //             smoothPasses = 8,
    //             slopeLimit = 0.0f,
    
    //             valleyWidth = 0.18f,
    //             valleyDepth = 0.65f
    //         };
    
    //         switch (preset)
    //         {
    //             case TerrainPreset.Plains:
    //                 p.heightY = 60;
    //                 p.baseFrequency = 0.0011f;
    //                 p.octaves = 4;
    //                 p.warpStrength = 0.08f;
    //                 p.heightPow = 1.1f;
    //                 p.smoothPasses = 10;
    //                 p.seaLevel = 0.16f;
    //                 break;
    
    //             case TerrainPreset.Hills:
    //                 p.heightY = 120;
    //                 p.baseFrequency = 0.0015f;
    //                 p.octaves = 5;
    //                 p.warpStrength = 0.14f;
    //                 p.smoothPasses = 8;
    //                 break;
    
    //             case TerrainPreset.Mountains:
    //                 p.heightY = 360;
    //                 p.baseFrequency = 0.0018f;
    //                 p.octaves = 6;
    //                 p.warpStrength = 0.22f;
    //                 p.heightPow = 1.35f;
    //                 p.smoothPasses = 5;
    //                 break;
    
    //             case TerrainPreset.MountainValley:
    //                 p.heightY = 380;
    //                 p.baseFrequency = 0.0018f;
    //                 p.octaves = 6;
    //                 p.warpStrength = 0.22f;
    //                 p.heightPow = 1.35f;
    //                 p.smoothPasses = 5;
    //                 p.valleyWidth = 0.18f;
    //                 p.valleyDepth = 0.7f;
    //                 break;
    
    //             case TerrainPreset.Island:
    //                 p.heightY = 220;
    //                 p.baseFrequency = 0.0017f;
    //                 p.octaves = 5;
    //                 p.warpStrength = 0.20f;
    //                 p.seaLevel = 0.22f;
    //                 p.smoothPasses = 6;
    //                 break;
    
    //             case TerrainPreset.Plateau:
    //                 p.heightY = 220;
    //                 p.baseFrequency = 0.0015f;
    //                 p.octaves = 5;
    //                 p.heightPow = 1.15f;
    //                 p.slopeLimit = 0.25f;
    //                 p.smoothPasses = 4;
    //                 break;
    
    //             case TerrainPreset.Badlands:
    //                 p.heightY = 200;
    //                 p.baseFrequency = 0.0019f;
    //                 p.octaves = 6;
    //                 p.warpStrength = 0.28f;
    //                 p.heightPow = 1.1f;
    //                 p.slopeLimit = 0.35f;
    //                 p.smoothPasses = 2;
    //                 break;
    
    //             case TerrainPreset.Archipelago:
    //                 p.heightY = 200;
    //                 p.baseFrequency = 0.0017f;
    //                 p.octaves = 5;
    //                 p.seaLevel = 0.28f;
    //                 p.warpStrength = 0.25f;
    //                 p.smoothPasses = 5;
    //                 break;
    
    //             case TerrainPreset.Dunes:
    //                 p.heightY = 120;
    //                 p.baseFrequency = 0.0022f;
    //                 p.octaves = 4;
    //                 p.warpStrength = 0.18f;
    //                 p.heightPow = 1.05f;
    //                 p.smoothPasses = 3;
    //                 break;
    
    //             case TerrainPreset.Cliffs:
    //                 p.heightY = 280;
    //                 p.baseFrequency = 0.0016f;
    //                 p.octaves = 5;
    //                 p.warpStrength = 0.20f;
    //                 p.slopeLimit = 0.15f;
    //                 p.smoothPasses = 2;
    //                 break;
    //         }
    
    //         return p;
    //     }
    
    //     public static List<FormationLayer> DefaultFormations(TerrainPreset preset)
    //     {
    //         var list = new List<FormationLayer>
    //         {
    //             new FormationLayer { type = FormationType.BaseFractal, enabled = true, strength = 1f, frequency = 0.002f }
    //         };
    
    //         switch (preset)
    //         {
    //             case TerrainPreset.Plains:
    //                 list.Add(new FormationLayer { type = FormationType.RollingHills, strength = 0.15f, frequency = 0.0015f });
    //                 list.Add(new FormationLayer { type = FormationType.NoiseDetail, strength = 0.10f, frequency = 0.012f });
    //                 break;
    
    //             case TerrainPreset.Hills:
    //                 list.Add(new FormationLayer { type = FormationType.RollingHills, strength = 0.45f, frequency = 0.0018f });
    //                 list.Add(new FormationLayer { type = FormationType.NoiseDetail, strength = 0.20f, frequency = 0.012f });
    //                 break;
    
    //             case TerrainPreset.Mountains:
    //                 list.Add(new FormationLayer { type = FormationType.RidgeMountains, strength = 0.85f, frequency = 0.0020f, paramA = 0.85f });
    //                 list.Add(new FormationLayer { type = FormationType.NoiseDetail, strength = 0.22f, frequency = 0.014f });
    //                 break;
    
    //             case TerrainPreset.MountainValley:
    //                 list.Add(new FormationLayer { type = FormationType.RidgeMountains, strength = 0.85f, frequency = 0.0020f, paramA = 0.9f });
    //                 list.Add(new FormationLayer { type = FormationType.NoiseDetail, strength = 0.22f, frequency = 0.014f });
    //                 break;
    
    //             case TerrainPreset.Island:
    //                 list.Add(new FormationLayer { type = FormationType.RidgeMountains, strength = 0.55f, frequency = 0.0020f });
    //                 list.Add(new FormationLayer { type = FormationType.IslandFalloff, strength = 1f, paramA = 0.60f, paramB = 0.55f });
    //                 list.Add(new FormationLayer { type = FormationType.NoiseDetail, strength = 0.18f, frequency = 0.013f });
    //                 break;
    
    //             case TerrainPreset.Archipelago:
    //                 list.Add(new FormationLayer { type = FormationType.ArchipelagoFalloff, strength = 1f, paramA = 0.65f, paramB = 0.65f });
    //                 list.Add(new FormationLayer { type = FormationType.NoiseDetail, strength = 0.18f, frequency = 0.013f });
    //                 break;
    
    //             case TerrainPreset.Plateau:
    //                 list.Add(new FormationLayer { type = FormationType.Plateau, strength = 0.75f, paramA = 10f, paramB = 0.55f });
    //                 list.Add(new FormationLayer { type = FormationType.NoiseDetail, strength = 0.16f, frequency = 0.012f });
    //                 break;
    
    //             case TerrainPreset.Badlands:
    //                 list.Add(new FormationLayer { type = FormationType.BadlandsTerraces, strength = 0.6f, paramA = 18f, paramB = 0.4f });
    //                 list.Add(new FormationLayer { type = FormationType.NoiseDetail, strength = 0.35f, frequency = 0.02f });
    //                 break;
    
    //             case TerrainPreset.Dunes:
    //                 list.Add(new FormationLayer { type = FormationType.Dunes, strength = 0.65f, frequency = 0.01f, paramA = 0.7f });
    //                 list.Add(new FormationLayer { type = FormationType.NoiseDetail, strength = 0.12f, frequency = 0.02f });
    //                 break;
    
    //             case TerrainPreset.Cliffs:
    //                 list.Add(new FormationLayer { type = FormationType.CliffBands, strength = 0.75f, frequency = 0.002f, paramA = 0.55f, paramB = 0.8f });
    //                 list.Add(new FormationLayer { type = FormationType.NoiseDetail, strength = 0.18f, frequency = 0.015f });
    //                 break;
    //         }
    
    //         return list;
    //     }
    
    //     // ----------------------------- HEIGHT GENERATION -----------------------------
    //     private static float[,] BuildHeights(TerrainPreset preset, TerrainParams p, List<FormationLayer> formations, RiverParams river)
    //     {
    //         int res = p.heightmapResolution;
    //         float[,] h = new float[res, res];
    
    //         // Seed offsets
    //         float ox = Hash01(p.seed, 11) * 10000f;
    //         float oz = Hash01(p.seed, 23) * 10000f;
    //         float wx = Hash01(p.seed, 101) * 10000f;
    //         float wz = Hash01(p.seed, 203) * 10000f;
    
    //         // River polyline 0..1 space
    //         Vector2[] riverPts = null;
    //         if (river != null && river.enabled)
    //             riverPts = BuildRiverPolyline(p.seed, river);
    
    //         for (int z = 0; z < res; z++)
    //         {
    //             for (int x = 0; x < res; x++)
    //             {
    //                 float u = x / (float)(res - 1);
    //                 float v = z / (float)(res - 1);
    
    //                 float nx = u - 0.5f;
    //                 float nz = v - 0.5f;
    
    //                 DomainWarp(nx, nz, wx, wz, p, out float dnx, out float dnz);
    
    //                 float baseH = FractalNoise(dnx, dnz, ox, oz, p);
    //                 float height = baseH;
    
    //                 if (formations != null)
    //                 {
    //                     for (int i = 0; i < formations.Count; i++)
    //                     {
    //                         var f = formations[i];
    //                         if (f == null || !f.enabled) continue;
    //                         height = ApplyFormation(height, baseH, nx, nz, dnx, dnz, ox, oz, p, f);
    //                     }
    //                 }
    
    //                 // Simple preset valley
    //                 if (preset == TerrainPreset.MountainValley)
    //                     height = CarveDiagonalValley(height, nx, nz, p.valleyWidth, p.valleyDepth);
    
    //                 // River carve
    //                 if (riverPts != null)
    //                     height = CarveRiver(height, u, v, riverPts, river);
    
    //                 // curve
    //                 height = Mathf.Pow(Mathf.Clamp01(height), Mathf.Clamp(p.heightPow, 0.6f, 2.5f));
    
    //                 // sea floor
    //                 height = Mathf.Max(height, p.seaLevel * 0.55f);
    
    //                 h[z, x] = height;
    //             }
    //         }
    
    //         // Smooth
    //         int passes = Mathf.Clamp(p.smoothPasses, 0, 60);
    //         for (int i = 0; i < passes; i++)
    //             SmoothCross5(h);
    
    //         // Optional slope limiter
    //         if (p.slopeLimit > 0f)
    //             LimitSlope(h, Mathf.Clamp01(p.slopeLimit));
    
    //         // Clamp
    //         for (int z = 0; z < res; z++)
    //             for (int x = 0; x < res; x++)
    //                 h[z, x] = Mathf.Clamp01(h[z, x]);
    
    //         // Debug min/max (helps diagnose "flat")
    //         GetMinMax(h, out float mn, out float mx);
    //         Debug.Log($"[TerrainEditor] Heights min={mn:0.000} max={mx:0.000} (res {res})");
    
    //         return h;
    //     }
    
    //     private static void GetMinMax(float[,] h, out float mn, out float mx)
    //     {
    //         mn = float.MaxValue;
    //         mx = float.MinValue;
    //         int zN = h.GetLength(0);
    //         int xN = h.GetLength(1);
    //         for (int z = 0; z < zN; z++)
    //         for (int x = 0; x < xN; x++)
    //         {
    //             float v = h[z, x];
    //             if (v < mn) mn = v;
    //             if (v > mx) mx = v;
    //         }
    //     }
    
    //     private static float ApplyFormation(
    //         float height,
    //         float baseH,
    //         float nx,
    //         float nz,
    //         float dnx,
    //         float dnz,
    //         float ox,
    //         float oz,
    //         TerrainParams p,
    //         FormationLayer f)
    //     {
    //         float s = Mathf.Clamp01(f.strength);
    
    //         switch (f.type)
    //         {
    //             case FormationType.BaseFractal:
    //                 height = Mathf.Lerp(height, baseH, s);
    //                 break;
    
    //             case FormationType.RollingHills:
    //             {
    //                 float n = FractalNoise(dnx, dnz, ox + 100f, oz + 200f, p);
    //                 n = Mathf.Pow(n, 1.25f);
    //                 height = Mathf.Lerp(height, (height + n) * 0.5f, s);
    //                 break;
    //             }
    
    //             case FormationType.RidgeMountains:
    //             {
    //                 float r = RidgedNoise(dnx, dnz, ox + 1000f, oz + 2000f, p);
    //                 float m = Mathf.Max(height, r);
    //                 height = Mathf.Lerp(height, m, s);
    //                 break;
    //             }
    
    //             case FormationType.Plateau:
    //             {
    //                 int steps = Mathf.Clamp(Mathf.RoundToInt(f.paramA), 2, 40);
    //                 float terr = Terrace(height, steps, Mathf.Clamp01(f.paramB));
    //                 height = Mathf.Lerp(height, terr, s);
    //                 break;
    //             }
    
    //             case FormationType.BadlandsTerraces:
    //             {
    //                 int steps = Mathf.Clamp(Mathf.RoundToInt(f.paramA), 6, 40);
    //                 float terr = Terrace(height, steps, 0.2f);
    //                 float det = Mathf.PerlinNoise((dnx * 1000f + ox) * 0.02f, (dnz * 1000f + oz) * 0.02f);
    //                 det = (det * 2f - 1f) * 0.08f;
    //                 height = Mathf.Lerp(height, Mathf.Clamp01(terr + det), s);
    //                 break;
    //             }
    
    //             case FormationType.CliffBands:
    //             {
    //                 float spacing = Mathf.Max(0.05f, f.paramA);
    //                 float sharp = Mathf.Lerp(1f, 12f, Mathf.Clamp01(f.paramB));
    //                 float band = Mathf.Abs(Mathf.Sin(height * Mathf.PI * spacing * 6f));
    //                 band = Mathf.Pow(band, sharp);
    //                 float cliffs = Mathf.Clamp01(height * (0.75f + band * 0.6f));
    //                 height = Mathf.Lerp(height, cliffs, s);
    //                 break;
    //             }
    
    //             case FormationType.Dunes:
    //             {
    //                 float dir = Mathf.Clamp01(f.paramB);
    //                 float freq = Mathf.Clamp(f.frequency, 0.001f, 0.05f);
    //                 float a = Mathf.PerlinNoise((dnx * 1000f + ox) * freq, (dnz * 1000f + oz) * freq);
    //                 float b = Mathf.PerlinNoise((dnz * 1000f + ox + 777f) * freq, (dnx * 1000f + oz + 333f) * freq);
    //                 float dunes = Mathf.Lerp(b, a, dir);
    //                 dunes = Mathf.Pow(dunes, 1.6f);
    //                 height = Mathf.Lerp(height, Mathf.Clamp01(height * 0.7f + dunes * 0.35f), s);
    //                 break;
    //             }
    
    //             case FormationType.Craters:
    //             {
    //                 int count = Mathf.Clamp(Mathf.RoundToInt(f.paramA * 20f), 1, 40);
    //                 float radius = Mathf.Clamp01(f.paramB) * 0.25f + 0.02f;
    
    //                 float crater = height;
    //                 for (int i = 0; i < count; i++)
    //                 {
    //                     float cx = Hash01(p.seed, 300 + i * 17) - 0.5f;
    //                     float cz = Hash01(p.seed, 600 + i * 31) - 0.5f;
    //                     float dx = nx - cx;
    //                     float dz = nz - cz;
    //                     float r = Mathf.Sqrt(dx * dx + dz * dz);
    //                     float t = Mathf.Clamp01(1f - (r / radius));
    //                     float bowl = -t * t * 0.35f;
    //                     float rim = Mathf.Pow(t, 6f) * 0.10f;
    //                     crater += (bowl + rim) * s;
    //                 }
    //                 height = Mathf.Clamp01(crater);
    //                 break;
    //             }
    
    //             case FormationType.NoiseDetail:
    //             {
    //                 float freq = Mathf.Clamp(f.frequency, 0.002f, 0.05f);
    //                 float det = Mathf.PerlinNoise((dnx * 1000f + ox) * freq, (dnz * 1000f + oz) * freq);
    //                 det = (det * 2f - 1f) * 0.12f;
    //                 height = Mathf.Clamp01(height + det * s);
    //                 break;
    //             }
    
    //             case FormationType.IslandFalloff:
    //             {
    //                 float radius = Mathf.Clamp(f.paramA, 0.2f, 0.95f);
    //                 float softness = Mathf.Clamp01(f.paramB);
    //                 height = Mathf.Lerp(height, IslandFalloff(height, nx, nz, p.seaLevel, radius, softness), s);
    //                 break;
    //             }
    
    //             case FormationType.ArchipelagoFalloff:
    //             {
    //                 float radius = Mathf.Clamp(f.paramA, 0.25f, 0.95f);
    //                 float softness = Mathf.Clamp01(f.paramB);
    
    //                 float fall = 0f;
    //                 for (int k = 0; k < 3; k++)
    //                 {
    //                     float cx = (Hash01(p.seed, 900 + k * 13) - 0.5f) * 0.7f;
    //                     float cz = (Hash01(p.seed, 1200 + k * 19) - 0.5f) * 0.7f;
    //                     float dx = nx - cx;
    //                     float dz = nz - cz;
    //                     float r = Mathf.Sqrt(dx * dx + dz * dz);
    
    //                     float edge0 = radius * (1f - 0.35f * softness);
    //                     float edge1 = radius;
    //                     float f0 = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(edge0, edge1, r));
    //                     fall = Mathf.Max(fall, f0);
    //                 }
    
    //                 float island = Mathf.Max(height * fall, p.seaLevel * 0.85f);
    //                 height = Mathf.Lerp(height, island, s);
    //                 break;
    //             }
    //         }
    
    //         return height;
    //     }
    
    //     // ----------------------------- RIVER FUNCTIONS -----------------------------
    //     private static Vector2[] BuildRiverPolyline(int seed, RiverParams rp)
    //     {
    //         int n = Mathf.Clamp(rp.points, 4, 256);
    //         var pts = new Vector2[n];
    
    //         Vector2 start = EdgePoint(rp.startEdge, 0.15f + Hash01(seed, 700) * 0.7f);
    //         Vector2 end = EdgePoint(rp.endEdge, 0.15f + Hash01(seed, 701) * 0.7f);
    
    //         for (int i = 0; i < n; i++)
    //         {
    //             float t = i / (float)(n - 1);
    //             Vector2 p = Vector2.Lerp(start, end, t);
    
    //             Vector2 dir = (end - start).normalized;
    //             Vector2 perp = new Vector2(-dir.y, dir.x);
    
    //             float m = Mathf.PerlinNoise((t * 1000f + Hash01(seed, 800) * 999f) * rp.meanderFreq, Hash01(seed, 801) * 10f);
    //             m = (m * 2f - 1f) * rp.meander;
    
    //             p += perp * m;
    
    //             p.x = Mathf.Clamp01(p.x);
    //             p.y = Mathf.Clamp01(p.y);
    
    //             pts[i] = p;
    //         }
    
    //         return pts;
    //     }
    
    //     private static Vector2 EdgePoint(Edge e, float t)
    //     {
    //         t = Mathf.Clamp01(t);
    //         return e switch
    //         {
    //             Edge.Left => new Vector2(0f, t),
    //             Edge.Right => new Vector2(1f, t),
    //             Edge.Top => new Vector2(t, 1f),
    //             Edge.Bottom => new Vector2(t, 0f),
    //             _ => new Vector2(0f, t),
    //         };
    //     }
    
    //     private static float CarveRiver(float height, float u, float v, Vector2[] pts, RiverParams rp)
    //     {
    //         float dist = DistanceToPolyline(new Vector2(u, v), pts);
    //         float w = Mathf.Max(0.0001f, rp.width);
    
    //         float t = Mathf.Clamp01(1f - dist / w);
    //         float profile = Mathf.Lerp(t * t, Mathf.SmoothStep(0f, 1f, t), rp.bankSmooth);
    
    //         float carved = height - profile * Mathf.Clamp01(rp.depth);
    //         return carved;
    //     }
    
    //     private static float DistanceToPolyline(Vector2 p, Vector2[] pts)
    //     {
    //         float best = float.MaxValue;
    //         for (int i = 0; i < pts.Length - 1; i++)
    //         {
    //             float d = DistancePointToSegment(p, pts[i], pts[i + 1]);
    //             if (d < best) best = d;
    //         }
    //         return best;
    //     }
    
    //     private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    //     {
    //         Vector2 ab = b - a;
    //         float t = Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, Vector2.Dot(ab, ab));
    //         t = Mathf.Clamp01(t);
    //         Vector2 proj = a + ab * t;
    //         return Vector2.Distance(p, proj);
    //     }
    
    //     // ----------------------------- TREES -----------------------------
    //     private static void ApplyTrees(TerrainData data, float[,] heights, TerrainParams p, List<TreeRule> rules)
    //     {
    //         if (rules == null || rules.Count == 0)
    //         {
    //             data.treeInstances = Array.Empty<TreeInstance>();
    //             return;
    //         }
    
    //         // Build prototypes
    //         var prefabToIndex = new Dictionary<GameObject, int>();
    //         var prototypes = new List<TreePrototype>();
    
    //         foreach (var r in rules)
    //         {
    //             if (r == null || !r.enabled) continue;
    //             if (r.prefab == null) continue;
    
    //             if (!prefabToIndex.ContainsKey(r.prefab))
    //             {
    //                 prefabToIndex[r.prefab] = prototypes.Count;
    //                 prototypes.Add(new TreePrototype { prefab = r.prefab });
    //             }
    //         }
    
    //         data.treePrototypes = prototypes.ToArray();
    
    //         if (prototypes.Count == 0)
    //         {
    //             data.treeInstances = Array.Empty<TreeInstance>();
    //             return;
    //         }
    
    //         int grid = 128;
    //         var instances = new List<TreeInstance>(2000);
    
    //         float SampleHeight01(float u, float v) => SampleHeightBilinear01(heights, u, v);
    
    //         float SampleSlopeDeg(float u, float v)
    //         {
    //             float du = 1f / grid;
    //             float dv = 1f / grid;
    //             float h0 = SampleHeight01(u, v);
    //             float hx = SampleHeight01(Mathf.Clamp01(u + du), v);
    //             float hz = SampleHeight01(u, Mathf.Clamp01(v + dv));
    
    //             float dx = (hx - h0) * p.heightY;
    //             float dz = (hz - h0) * p.heightY;
    //             float run = (p.sizeXZ / (float)grid);
    
    //             float rise = Mathf.Sqrt(dx * dx + dz * dz);
    //             return Mathf.Atan2(rise, Mathf.Max(1e-6f, run)) * Mathf.Rad2Deg;
    //         }
    
    //         for (int z = 0; z < grid; z++)
    //         for (int x = 0; x < grid; x++)
    //         {
    //             float baseU = (x + 0.5f) / grid;
    //             float baseV = (z + 0.5f) / grid;
    
    //             foreach (var r in rules)
    //             {
    //                 if (r == null || !r.enabled) continue;
    //                 if (r.prefab == null) continue;
    
    //                 int protoIndex = prefabToIndex[r.prefab];
    
    //                 // deterministic per-cell rng
    //                 var rng = new System.Random(HashInt(p.seed, r.seedOffset + x * 73856093 + z * 19349663));
    
    //                 for (int s = 0; s < r.samplesPerCell; s++)
    //                 {
    //                     float ju = (float)rng.NextDouble() - 0.5f;
    //                     float jv = (float)rng.NextDouble() - 0.5f;
    
    //                     float u = Mathf.Clamp01(baseU + ju / grid);
    //                     float v = Mathf.Clamp01(baseV + jv / grid);
    
    //                     float h01 = SampleHeight01(u, v);
    //                     if (h01 < r.minHeight || h01 > r.maxHeight) continue;
    
    //                     float slope = SampleSlopeDeg(u, v);
    //                     if (slope > r.maxSlopeDeg) continue;
    
    //                     if ((float)rng.NextDouble() > r.density) continue;
    
    //                     float scale = Mathf.Lerp(r.minScale, r.maxScale, (float)rng.NextDouble());
    
    //                     instances.Add(new TreeInstance
    //                     {
    //                         prototypeIndex = protoIndex,
    //                         position = new Vector3(u, h01, v),
    //                         widthScale = scale,
    //                         heightScale = scale,
    //                         color = r.color,
    //                         lightmapColor = r.lightmapColor,
    //                         rotation = (float)rng.NextDouble() * Mathf.PI * 2f
    //                     });
    //                 }
    //             }
    //         }
    
    //         data.treeInstances = instances.ToArray();
    //         Debug.Log($"[TerrainEditor] Placed {instances.Count} trees.");
    //     }
    
    //     private static float SampleHeightBilinear01(float[,] h, float u, float v)
    //     {
    //         int res = h.GetLength(0);
    //         float x = u * (res - 1);
    //         float z = v * (res - 1);
    
    //         int x0 = Mathf.Clamp((int)Mathf.Floor(x), 0, res - 2);
    //         int z0 = Mathf.Clamp((int)Mathf.Floor(z), 0, res - 2);
    
    //         float tx = x - x0;
    //         float tz = z - z0;
    
    //         float a = h[z0, x0];
    //         float b = h[z0, x0 + 1];
    //         float c = h[z0 + 1, x0];
    //         float d = h[z0 + 1, x0 + 1];
    
    //         float ab = Mathf.Lerp(a, b, tx);
    //         float cd = Mathf.Lerp(c, d, tx);
    //         return Mathf.Lerp(ab, cd, tz);
    //     }
    
    //     // ----------------------------- WATER PLANE -----------------------------
    //     private static void EnsureWaterPlane(GameObject terrainGO, RiverParams river, TerrainParams p)
    //     {
    //         Transform child = terrainGO.transform.Find(river.waterObjectName);
    //         GameObject water;
    
    //         if (child != null) water = child.gameObject;
    //         else
    //         {
    //             water = GameObject.CreatePrimitive(PrimitiveType.Plane);
    //             water.name = river.waterObjectName;
    //             Undo.RegisterCreatedObjectUndo(water, "Create Water");
    //             water.transform.SetParent(terrainGO.transform, true);
    //         }
    
    //         float scale = p.sizeXZ / 10f;
    //         water.transform.localScale = new Vector3(scale, 1f, scale);
    
    //         float y = river.waterLevel * p.heightY;
    //         water.transform.position = terrainGO.transform.position + new Vector3(p.sizeXZ * 0.5f, y, p.sizeXZ * 0.5f);
    
    //         var col = water.GetComponent<Collider>();
    //         if (col != null) UnityEngine.Object.DestroyImmediate(col);
    //     }
    
    //     // ----------------------------- NOISE + SHAPING -----------------------------
    //     private static void DomainWarp(float nx, float nz, float ox, float oz, TerrainParams p, out float dnx, out float dnz)
    //     {
    //         float ws = Mathf.Clamp01(p.warpStrength);
    //         if (ws <= 0f)
    //         {
    //             dnx = nx;
    //             dnz = nz;
    //             return;
    //         }
    
    //         float wf = Mathf.Max(0.000001f, p.warpFrequency);
    
    //         float a = Mathf.PerlinNoise((nx * 1000f + ox) * wf, (nz * 1000f + oz) * wf);
    //         float b = Mathf.PerlinNoise((nx * 1000f + ox + 5000f) * wf, (nz * 1000f + oz + 7000f) * wf);
    
    //         a = a * 2f - 1f;
    //         b = b * 2f - 1f;
    
    //         float scale = ws * 0.18f;
    //         dnx = nx + a * scale;
    //         dnz = nz + b * scale;
    //     }
    
    //     private static float FractalNoise(float nx, float nz, float ox, float oz, TerrainParams p)
    //     {
    //         float amp = 1f;
    //         float freq = p.baseFrequency;
    //         float sum = 0f;
    //         float norm = 0f;
    
    //         for (int i = 0; i < p.octaves; i++)
    //         {
    //             float v = Mathf.PerlinNoise((nx * 1000f + ox) * freq, (nz * 1000f + oz) * freq);
    //             sum += v * amp;
    //             norm += amp;
    //             amp *= p.persistence;
    //             freq *= p.lacunarity;
    //         }
    
    //         return sum / Mathf.Max(0.0001f, norm);
    //     }
    
    //     private static float RidgedNoise(float nx, float nz, float ox, float oz, TerrainParams p)
    //     {
    //         float n = FractalNoise(nx, nz, ox, oz, p);
    //         float r = 1f - Mathf.Abs(2f * n - 1f);
    //         return Mathf.Pow(r, 2.2f);
    //     }
    
    //     private static float Terrace(float h, int steps, float smoothness)
    //     {
    //         h = Mathf.Clamp01(h);
    //         float k = steps - 1;
    //         float stepped = Mathf.Round(h * k) / k;
    //         return Mathf.Lerp(stepped, h, Mathf.Clamp01(smoothness));
    //     }
    
    //     private static float IslandFalloff(float height, float nx, float nz, float seaLevel, float radius, float softness)
    //     {
    //         float r = Mathf.Sqrt(nx * nx + nz * nz);
    //         float edge0 = radius * (1f - 0.35f * softness);
    //         float edge1 = radius;
    
    //         float falloff = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(edge0, edge1, r));
    //         float island = height * falloff;
    //         return Mathf.Max(island, seaLevel * 0.85f);
    //     }
    
    //     private static float CarveDiagonalValley(float height, float nx, float nz, float width, float depth)
    //     {
    //         float d = Mathf.Abs(nz - nx) / Mathf.Sqrt(2f);
    //         float t = Mathf.SmoothStep(Mathf.Clamp01(width), 0f, d);
    //         return height - (t * Mathf.Clamp01(depth));
    //     }
    
    //     private static void SmoothCross5(float[,] h)
    //     {
    //         int zN = h.GetLength(0);
    //         int xN = h.GetLength(1);
    
    //         for (int z = 1; z < zN - 1; z++)
    //         for (int x = 1; x < xN - 1; x++)
    //         {
    //             float v = h[z, x] + h[z - 1, x] + h[z + 1, x] + h[z, x - 1] + h[z, x + 1];
    //             h[z, x] = v / 5f;
    //         }
    //     }
    
    //     private static void LimitSlope(float[,] h, float amount)
    //     {
    //         int zN = h.GetLength(0);
    //         int xN = h.GetLength(1);
    
    //         for (int z = 1; z < zN - 1; z++)
    //         for (int x = 1; x < xN - 1; x++)
    //         {
    //             float avg = (h[z - 1, x] + h[z + 1, x] + h[z, x - 1] + h[z, x + 1]) * 0.25f;
    //             float maxDelta = Mathf.Lerp(0.25f, 0.02f, amount);
    //             h[z, x] = Mathf.Clamp(h[z, x], avg - maxDelta, avg + maxDelta);
    //         }
    //     }
    
    //     private static float Hash01(int seed, int salt)
    //     {
    //         unchecked
    //         {
    //             int h = unchecked((int)2166136261);
    //             h = (h ^ seed) * 16777619;
    //             h = (h ^ salt) * 16777619;
    //             int v = h & 0x00FFFFFF;
    //             return v / 16777216f;
    //         }
    //     }
    
    //     private static int HashInt(int a, int b)
    //     {
    //         unchecked
    //         {
    //             int h = 17;
    //             h = h * 31 + a;
    //             h = h * 31 + b;
    //             h ^= (h << 13);
    //             h ^= (h >> 17);
    //             h ^= (h << 5);
    //             return h;
    //         }
    //     }
    
    //     // ----------------------------- EDITOR WINDOW -----------------------------
    //     public class TerrainEditorWindow : EditorWindow
    //     {
    //         private Vector2 scroll;
    
    //         private string terrainName = "Generated Terrain";
    //         private TerrainPreset preset = TerrainPreset.Hills;
    //         private int seed = 1337;
    //         private Vector3 terrainPosition = Vector3.zero; // FIX: doesn't collide with EditorWindow.position
    
    //         private TerrainParams p;
    //         private List<FormationLayer> formations;
    //         private RiverParams river;
    //         private List<TreeRule> trees;
    
    //         private bool foldCore = true;
    //         private bool foldForm = true;
    //         private bool foldRiver = true;
    //         private bool foldTrees = true;
    
    //         [MenuItem("Tools/Terrain Editor")]
    //         public static void Open() => GetWindow<TerrainEditorWindow>("Terrain Editor");
    
    //         private void OnEnable()
    //         {
    //             p = DefaultParams(preset, seed);
    //             formations = DefaultFormations(preset);
    //             river = new RiverParams();
    //             trees = new List<TreeRule> { new TreeRule { name = "Default Trees" } };
    //         }
    
    //         private void OnGUI()
    //         {
    //             scroll = EditorGUILayout.BeginScrollView(scroll);
    
    //             EditorGUILayout.LabelField("Terrain Editor", EditorStyles.boldLabel);
    //             terrainName = EditorGUILayout.TextField("Name", terrainName);
    //             preset = (TerrainPreset)EditorGUILayout.EnumPopup("Preset", preset);
    //             seed = EditorGUILayout.IntField("Seed", seed);
    //             terrainPosition = EditorGUILayout.Vector3Field("Position", terrainPosition);
    
    //             EditorGUILayout.Space(6);
    
    //             if (GUILayout.Button("Load Preset Defaults"))
    //             {
    //                 p = DefaultParams(preset, seed);
    //                 formations = DefaultFormations(preset);
    //             }
    
    //             EditorGUILayout.Space(8);
    
    //             foldCore = EditorGUILayout.Foldout(foldCore, "Core Settings", true);
    //             if (foldCore) DrawCore(p);
    
    //             EditorGUILayout.Space(6);
    
    //             foldForm = EditorGUILayout.Foldout(foldForm, "Formations (Stackable Layers)", true);
    //             if (foldForm) DrawFormations(formations);
    
    //             EditorGUILayout.Space(6);
    
    //             foldRiver = EditorGUILayout.Foldout(foldRiver, "River (Across Map)", true);
    //             if (foldRiver) DrawRiver(river);
    
    //             EditorGUILayout.Space(6);
    
    //             foldTrees = EditorGUILayout.Foldout(foldTrees, "Trees", true);
    //             if (foldTrees) DrawTrees(trees);
    
    //             EditorGUILayout.Space(12);
    
    //             using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(terrainName)))
    //             {
    //                 if (GUILayout.Button("Generate / Replace Terrain", GUILayout.Height(34)))
    //                 {
    //                     CreateOrReplaceTerrain(
    //                         terrainName,
    //                         preset,
    //                         seed,
    //                         terrainPosition,
    //                         p,
    //                         formations,
    //                         river,
    //                         trees);
    //                 }
    //             }
    
    //             EditorGUILayout.EndScrollView();
    //         }
    
    //         private void DrawCore(TerrainParams p)
    //         {
    //             p.heightmapResolution = EditorGUILayout.IntPopup("Heightmap Res",
    //                 p.heightmapResolution,
    //                 new[] { "257", "513", "1025" },
    //                 new[] { 257, 513, 1025 });
    
    //             p.sizeXZ = EditorGUILayout.IntField("Size XZ (m)", p.sizeXZ);
    //             p.heightY = EditorGUILayout.IntField("Max Height Y (m)", p.heightY);
    
    //             p.seaLevel = EditorGUILayout.Slider("Sea Level", p.seaLevel, 0f, 1f);
    //             p.heightPow = EditorGUILayout.Slider("Height Curve (Pow)", p.heightPow, 0.6f, 2.5f);
    
    //             p.smoothPasses = EditorGUILayout.IntSlider("Smooth Passes", p.smoothPasses, 0, 60);
    //             p.slopeLimit = EditorGUILayout.Slider("Slope Limit", p.slopeLimit, 0f, 1f);
    
    //             EditorGUILayout.Space(6);
    //             EditorGUILayout.LabelField("Noise", EditorStyles.boldLabel);
    //             p.octaves = EditorGUILayout.IntSlider("Octaves", p.octaves, 1, 8);
    //             p.lacunarity = EditorGUILayout.Slider("Lacunarity", p.lacunarity, 1.5f, 4f);
    //             p.persistence = EditorGUILayout.Slider("Persistence", p.persistence, 0.2f, 0.9f);
    //             p.baseFrequency = EditorGUILayout.Slider("Base Frequency", p.baseFrequency, 0.0005f, 0.005f);
    
    //             EditorGUILayout.Space(4);
    //             EditorGUILayout.LabelField("Domain Warp", EditorStyles.boldLabel);
    //             p.warpStrength = EditorGUILayout.Slider("Warp Strength", p.warpStrength, 0f, 0.6f);
    //             p.warpFrequency = EditorGUILayout.Slider("Warp Frequency", p.warpFrequency, 0.001f, 0.03f);
    
    //             if (preset == TerrainPreset.MountainValley)
    //             {
    //                 EditorGUILayout.Space(6);
    //                 EditorGUILayout.LabelField("Valley (Preset)", EditorStyles.boldLabel);
    //                 p.valleyWidth = EditorGUILayout.Slider("Valley Width", p.valleyWidth, 0f, 0.5f);
    //                 p.valleyDepth = EditorGUILayout.Slider("Valley Depth", p.valleyDepth, 0f, 1f);
    //             }
    //         }
    
    //         private void DrawFormations(List<FormationLayer> layers)
    //         {
    //             if (layers == null) return;
    
    //             for (int i = 0; i < layers.Count; i++)
    //             {
    //                 var f = layers[i];
    //                 EditorGUILayout.BeginVertical("box");
    
    //                 EditorGUILayout.BeginHorizontal();
    //                 f.enabled = EditorGUILayout.Toggle(f.enabled, GUILayout.Width(20));
    //                 f.type = (FormationType)EditorGUILayout.EnumPopup(f.type);
    //                 if (GUILayout.Button("▲", GUILayout.Width(24)) && i > 0)
    //                     (layers[i - 1], layers[i]) = (layers[i], layers[i - 1]);
    //                 if (GUILayout.Button("▼", GUILayout.Width(24)) && i < layers.Count - 1)
    //                     (layers[i + 1], layers[i]) = (layers[i], layers[i + 1]);
    //                 if (GUILayout.Button("X", GUILayout.Width(22)))
    //                 {
    //                     layers.RemoveAt(i);
    //                     EditorGUILayout.EndHorizontal();
    //                     EditorGUILayout.EndVertical();
    //                     break;
    //                 }
    //                 EditorGUILayout.EndHorizontal();
    
    //                 f.strength = EditorGUILayout.Slider("Strength", f.strength, 0f, 1f);
    //                 f.frequency = EditorGUILayout.Slider("Frequency", f.frequency, 0.0001f, 0.05f);
    //                 f.paramA = EditorGUILayout.FloatField("Param A", f.paramA);
    //                 f.paramB = EditorGUILayout.Slider("Param B", f.paramB, 0f, 1f);
    
    //                 EditorGUILayout.EndVertical();
    //             }
    
    //             if (GUILayout.Button("+ Add Formation Layer"))
    //                 layers.Add(new FormationLayer());
    
    //             EditorGUILayout.HelpBox(
    //                 "Order matters: BaseFractal first, big shapes next (mountains/cliffs), detail last.\n" +
    //                 "Param A/B meanings depend on the layer type.",
    //                 MessageType.Info);
    //         }
    
    //         private void DrawRiver(RiverParams r)
    //         {
    //             r.enabled = EditorGUILayout.Toggle("Enabled", r.enabled);
    //             if (!r.enabled) return;
    
    //             r.startEdge = (Edge)EditorGUILayout.EnumPopup("Start Edge", r.startEdge);
    //             r.endEdge = (Edge)EditorGUILayout.EnumPopup("End Edge", r.endEdge);
    
    //             r.points = EditorGUILayout.IntSlider("Points", r.points, 4, 256);
    
    //             r.meander = EditorGUILayout.Slider("Meander", r.meander, 0f, 1f);
    //             r.meanderFreq = EditorGUILayout.Slider("Meander Freq", r.meanderFreq, 0.001f, 0.05f);
    
    //             r.width = EditorGUILayout.Slider("Width", r.width, 0.001f, 0.25f);
    //             r.depth = EditorGUILayout.Slider("Depth", r.depth, 0f, 1f);
    //             r.bankSmooth = EditorGUILayout.Slider("Bank Smooth", r.bankSmooth, 0f, 1f);
    
    //             EditorGUILayout.Space(6);
    //             EditorGUILayout.LabelField("Water", EditorStyles.boldLabel);
    //             r.addWaterPlane = EditorGUILayout.Toggle("Add Water Plane", r.addWaterPlane);
    //             r.waterLevel = EditorGUILayout.Slider("Water Level", r.waterLevel, 0f, 1f);
    //             r.waterObjectName = EditorGUILayout.TextField("Water Object Name", r.waterObjectName);
    //         }
    
    //         private void DrawTrees(List<TreeRule> rules)
    //         {
    //             if (rules == null) return;
    
    //             for (int i = 0; i < rules.Count; i++)
    //             {
    //                 var t = rules[i];
    //                 EditorGUILayout.BeginVertical("box");
    
    //                 EditorGUILayout.BeginHorizontal();
    //                 t.enabled = EditorGUILayout.Toggle(t.enabled, GUILayout.Width(20));
    //                 t.name = EditorGUILayout.TextField(t.name);
    //                 if (GUILayout.Button("X", GUILayout.Width(22)))
    //                 {
    //                     rules.RemoveAt(i);
    //                     EditorGUILayout.EndHorizontal();
    //                     EditorGUILayout.EndVertical();
    //                     break;
    //                 }
    //                 EditorGUILayout.EndHorizontal();
    
    //                 t.prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", t.prefab, typeof(GameObject), false);
    //                 t.density = EditorGUILayout.Slider("Density", t.density, 0f, 1f);
    //                 t.samplesPerCell = EditorGUILayout.IntSlider("Samples/Cell", t.samplesPerCell, 1, 10);
    
    //                 t.minHeight = EditorGUILayout.Slider("Min Height", t.minHeight, 0f, 1f);
    //                 t.maxHeight = EditorGUILayout.Slider("Max Height", t.maxHeight, 0f, 1f);
    //                 t.maxSlopeDeg = EditorGUILayout.Slider("Max Slope", t.maxSlopeDeg, 0f, 60f);
    
    //                 t.minScale = EditorGUILayout.Slider("Min Scale", t.minScale, 0.2f, 2f);
    //                 t.maxScale = EditorGUILayout.Slider("Max Scale", t.maxScale, 0.2f, 2f);
    
    //                 t.seedOffset = EditorGUILayout.IntField("Seed Offset", t.seedOffset);
    
    //                 EditorGUILayout.EndVertical();
    //             }
    
    //             if (GUILayout.Button("+ Add Tree Rule"))
    //                 rules.Add(new TreeRule { name = "New Tree Rule" });
    
    //             EditorGUILayout.HelpBox(
    //                 "Trees use Terrain TreeInstances (fast). If you see no trees: set a prefab, increase density,\n" +
    //                 "and loosen slope/height limits.",
    //                 MessageType.Info);
    //         }
    //     }
    // }
}
