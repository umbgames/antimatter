using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Antimatter
{
    public static class TerrainAssetApplier
    {
        public static void ApplyAll(Terrain terrain, TerrainAssetProfile profile, int seed, bool clearOldRocks = true)
        {
            if (!terrain || !terrain.terrainData || !profile)
            {
                Debug.LogWarning("ApplyAll: missing terrain/terrainData/profile.");
                return;
            }

            ApplyTextures(terrain, profile, seed);
            ApplyDetails(terrain, profile, seed);
            ApplyTrees(terrain, profile, seed);
            ScatterRocks(terrain, profile, seed, clearOldRocks);
        }

        public static void ApplyTextures(Terrain terrain, TerrainAssetProfile profile, int seed)
        {
            var td = terrain.terrainData;

            // Collect layers from rules.
            var layers = new List<TerrainLayer>();
            foreach (var r in profile.textureRules)
                if (r != null && r.layer != null && !layers.Contains(r.layer))
                    layers.Add(r.layer);

            if (layers.Count == 0) return;

            td.terrainLayers = layers.ToArray();

            int aRes = Mathf.Max(16, profile.alphamapResolution);
            td.alphamapResolution = aRes;

            int layerCount = td.terrainLayers.Length;
            float[,,] splat = new float[aRes, aRes, layerCount];

            Vector3 size = td.size;
            Vector3 tPos = terrain.transform.position;

            for (int z = 0; z < aRes; z++)
            {
                for (int x = 0; x < aRes; x++)
                {
                    float u = x / (float)(aRes - 1);
                    float v = z / (float)(aRes - 1);

                    float height01 = td.GetInterpolatedHeight(u, v) / Mathf.Max(0.0001f, size.y);
                    float slope = td.GetSteepness(u, v); // 0..90

                    float worldX = tPos.x + u * size.x;
                    float worldZ = tPos.z + v * size.z;

                    float sum = 0f;

                    for (int i = 0; i < layerCount; i++)
                    {
                        var layer = td.terrainLayers[i];
                        float w = 0f;

                        var rule = FindTextureRule(profile, layer);
                        if (rule != null)
                        {
                            bool okH = height01 >= rule.minHeight && height01 <= rule.maxHeight;
                            bool okS = slope >= rule.minSlope && slope <= rule.maxSlope;

                            if (okH && okS)
                            {
                                float n = Mathf.PerlinNoise(
                                    (worldX + seed * 17.1f) * rule.noiseScale,
                                    (worldZ + seed * 29.7f) * rule.noiseScale
                                );
                                n = Mathf.Lerp(1f, n, rule.noiseAmount);
                                w = Mathf.Max(0f, rule.weight * n);
                            }
                        }

                        splat[z, x, i] = w;
                        sum += w;
                    }

                    if (sum < 0.0001f)
                    {
                        splat[z, x, 0] = 1f;
                        for (int i = 1; i < layerCount; i++) splat[z, x, i] = 0f;
                    }
                    else
                    {
                        for (int i = 0; i < layerCount; i++)
                            splat[z, x, i] /= sum;
                    }
                }
            }

            td.SetAlphamaps(0, 0, splat);
        }

        public static void ApplyDetails(Terrain terrain, TerrainAssetProfile profile, int seed)
        {
            var td = terrain.terrainData;
            if (profile.detailRules == null || profile.detailRules.Count == 0) return;

            td.SetDetailResolution(
                Mathf.Max(32, profile.detailResolution),
                Mathf.Clamp(profile.detailResolutionPerPatch, 8, 128)
            );

            var prototypes = new List<DetailPrototype>();
            foreach (var r in profile.detailRules)
            {
                if (r == null) continue;
                prototypes.Add(r.prototype);
            }
            td.detailPrototypes = prototypes.ToArray();

            int dRes = td.detailResolution;
            Vector3 size = td.size;
            Vector3 tPos = terrain.transform.position;

            for (int layerIndex = 0; layerIndex < profile.detailRules.Count; layerIndex++)
            {
                var rule = profile.detailRules[layerIndex];
                int[,] map = new int[dRes, dRes];

                for (int z = 0; z < dRes; z++)
                {
                    for (int x = 0; x < dRes; x++)
                    {
                        float u = x / (float)(dRes - 1);
                        float v = z / (float)(dRes - 1);

                        float height01 = td.GetInterpolatedHeight(u, v) / Mathf.Max(0.0001f, size.y);
                        float slope = td.GetSteepness(u, v);

                        if (height01 < rule.minHeight || height01 > rule.maxHeight) { map[z, x] = 0; continue; }
                        if (slope < rule.minSlope || slope > rule.maxSlope) { map[z, x] = 0; continue; }

                        float worldX = tPos.x + u * size.x;
                        float worldZ = tPos.z + v * size.z;

                        float n = Mathf.PerlinNoise(
                            (worldX + seed * 9.13f + layerIndex * 33.3f) * rule.noiseScale,
                            (worldZ + seed * 5.77f + layerIndex * 12.9f) * rule.noiseScale
                        );

                        float p = Mathf.Lerp(1f, n, rule.noiseAmount);
                        float roll = Mathf.PerlinNoise(worldX * 0.001f + layerIndex, worldZ * 0.001f + seed);

                        map[z, x] = (roll <= p) ? rule.density : 0;
                    }
                }

                td.SetDetailLayer(0, 0, layerIndex, map);
            }
        }

        public static void ApplyTrees(Terrain terrain, TerrainAssetProfile profile, int seed)
        {
            var td = terrain.terrainData;
            if (profile.treeRules == null || profile.treeRules.Count == 0) return;

            var protoList = new List<TreePrototype>();
            for (int i = 0; i < profile.treeRules.Count; i++)
            {
                var r = profile.treeRules[i];
                if (r == null || r.prefab == null) continue;

                protoList.Add(new TreePrototype { prefab = r.prefab });
            }
            td.treePrototypes = protoList.ToArray();

            if (td.treePrototypes.Length == 0) return;

            var instances = new List<TreeInstance>();
            Vector3 size = td.size;

            int protoIndex = 0;
            for (int ruleIndex = 0; ruleIndex < profile.treeRules.Count; ruleIndex++)
            {
                var r = profile.treeRules[ruleIndex];
                if (r == null || r.prefab == null) continue;

                int thisProto = protoIndex;
                protoIndex++;

                var rng = new System.Random(Hash(seed, ruleIndex * 101 + 7));

                for (int k = 0; k < r.count; k++)
                {
                    float u = (float)rng.NextDouble();
                    float v = (float)rng.NextDouble();

                    float height01 = td.GetInterpolatedHeight(u, v) / Mathf.Max(0.0001f, size.y);
                    float slope = td.GetSteepness(u, v);

                    if (height01 < r.minHeight || height01 > r.maxHeight) continue;
                    if (slope < r.minSlope || slope > r.maxSlope) continue;

                    float n = Mathf.PerlinNoise((u * size.x + seed * 31.7f) * r.noiseScale,
                                                (v * size.z + seed * 19.2f) * r.noiseScale);
                    float p = Mathf.Lerp(1f, n, r.noiseAmount);
                    if ((float)rng.NextDouble() > p) continue;

                    float scale = Mathf.Lerp(r.minScale, r.maxScale, (float)rng.NextDouble());

                    var ti = new TreeInstance
                    {
                        position = new Vector3(u, height01, v),
                        prototypeIndex = thisProto,
                        widthScale = scale,
                        heightScale = scale,
                        rotation = Mathf.Lerp(0f, Mathf.PI * 2f, (float)rng.NextDouble()),
                        color = Color.white,
                        lightmapColor = Color.white
                    };

                    instances.Add(ti);
                }
            }

            td.treeInstances = instances.ToArray();
        }

        public static void ScatterRocks(Terrain terrain, TerrainAssetProfile profile, int seed, bool clearOldRocks)
        {
            if (profile.rockRules == null || profile.rockRules.Count == 0) return;

            var td = terrain.terrainData;
            Vector3 size = td.size;

            Transform parent = terrain.transform.Find("__Rocks");
            if (parent == null)
            {
                var go = new GameObject("__Rocks");
#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(go, "Create Rocks Parent");
#endif
                go.transform.SetParent(terrain.transform);
                go.transform.localPosition = Vector3.zero;
                parent = go.transform;
            }

            if (clearOldRocks)
            {
#if UNITY_EDITOR
                for (int i = parent.childCount - 1; i >= 0; i--)
                    Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
#else
                for (int i = parent.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
#endif
            }

            for (int ruleIndex = 0; ruleIndex < profile.rockRules.Count; ruleIndex++)
            {
                var r = profile.rockRules[ruleIndex];
                if (r == null || r.prefab == null) continue;

                var rng = new System.Random(Hash(seed, 9001 + ruleIndex * 131));

                for (int k = 0; k < r.count; k++)
                {
                    float u = (float)rng.NextDouble();
                    float v = (float)rng.NextDouble();

                    float heightWorld = td.GetInterpolatedHeight(u, v);
                    float height01 = heightWorld / Mathf.Max(0.0001f, size.y);
                    float slope = td.GetSteepness(u, v);

                    if (height01 < r.minHeight || height01 > r.maxHeight) continue;
                    if (slope < r.minSlope || slope > r.maxSlope) continue;

                    float n = Mathf.PerlinNoise((u * size.x + seed * 11.1f) * r.noiseScale,
                                                (v * size.z + seed * 23.3f) * r.noiseScale);
                    float p = Mathf.Lerp(1f, n, r.noiseAmount);
                    if ((float)rng.NextDouble() > p) continue;

                    Vector3 worldPos = terrain.transform.position + new Vector3(u * size.x, heightWorld, v * size.z);

                    var rock = (GameObject)UnityEngine.Object.Instantiate(r.prefab, worldPos, Quaternion.identity, parent);
#if UNITY_EDITOR
                    Undo.RegisterCreatedObjectUndo(rock, "Scatter Rock");
#endif

                    float s = Mathf.Lerp(r.minScale, r.maxScale, (float)rng.NextDouble());
                    rock.transform.localScale = Vector3.one * s;

                    rock.transform.Rotate(Vector3.up, Mathf.Lerp(0f, 360f, (float)rng.NextDouble()), Space.World);

                    if (r.alignToNormal)
                    {
                        Vector3 normal = td.GetInterpolatedNormal(u, v);
                        rock.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * rock.transform.rotation;
                    }
                }
            }
        }

        private static TerrainAssetProfile.TextureRule FindTextureRule(TerrainAssetProfile profile, TerrainLayer layer)
        {
            for (int i = 0; i < profile.textureRules.Count; i++)
            {
                var r = profile.textureRules[i];
                if (r != null && r.layer == layer) return r;
            }
            return null;
        }

        private static int Hash(int a, int b)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + a;
                h = h * 31 + b;
                return h;
            }
        }
    }
}
