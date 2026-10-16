// =========================
// RoadMeshBuilder.cs
// =========================
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antimatter
{
    
    public static class RoadMeshBuilder
    {
        public enum RoadConformMode
        {
            SurfaceProjectToTerrain,
            TunnelNoProject
        }
    
        // Wider default: halfWidth=5 => ~10m road
        private const float DefaultHalfWidth = 5f;
    
        // Default material asset location
        private const string DefaultMatFolder = "Assets/Generated/Roads";
        private const string DefaultMatPath = DefaultMatFolder + "/DefaultRoad_V2.mat";
        private static Material _defaultRoadMat;
    
        public static GameObject BuildOrUpdateRoad(
            Scene scene,
            Terrain terrain,
            string name,
            List<Vector3> path,
            float halfWidth,
            float yOffset,
            Material mat = null,
            RoadConformMode mode = RoadConformMode.SurfaceProjectToTerrain,
            bool addCollider = true)
        {
            if (path == null || path.Count < 2) return null;
    
            if (halfWidth <= 0f) halfWidth = DefaultHalfWidth;
    
            // For surface roads we require terrain; for tunnel we don't.
            if (mode == RoadConformMode.SurfaceProjectToTerrain)
            {
                if (terrain == null || terrain.terrainData == null) return null;
            }
    
            GameObject go = GameObject.Find(name);
            bool okExisting = (go != null && go.GetComponent<MeshFilter>() != null && go.GetComponent<MeshRenderer>() != null);
    
            if (!okExisting)
            {
                if (go != null) name = name + "_Mesh";
    
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create Road Mesh");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
            }
    
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
    
            if (mat == null) mat = GetOrCreateDefaultRoadMaterial();
            if (mat != null) mr.sharedMaterial = mat;
    
            var mesh = new Mesh();
            mesh.name = name + "_Mesh";
    
            int n = path.Count;
    
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new int[(n - 1) * 6];
    
            float totalLen = 0f;
            var lens = new float[n];
            for (int i = 1; i < n; i++)
            {
                totalLen += Vector3.Distance(path[i - 1], path[i]);
                lens[i] = totalLen;
            }
    
            Vector3 tp = Vector3.zero;
            Vector3 ts = Vector3.one;
    
            if (terrain != null && terrain.terrainData != null)
            {
                tp = terrain.transform.position;
                ts = terrain.terrainData.size;
            }
    
            const float ClampMargin = 0.5f;
    
            float ClampX(float x) => Mathf.Clamp(x, tp.x + ClampMargin, tp.x + ts.x - ClampMargin);
            float ClampZ(float z) => Mathf.Clamp(z, tp.z + ClampMargin, tp.z + ts.z - ClampMargin);
    
            float SampleY(Vector3 world)
            {
                world.x = ClampX(world.x);
                world.z = ClampZ(world.z);
                return terrain.SampleHeight(world) + tp.y;
            }
    
            for (int i = 0; i < n; i++)
            {
                Vector3 p = path[i];
    
                Vector3 fwd;
                if (i == 0) fwd = (path[i + 1] - path[i]);
                else if (i == n - 1) fwd = (path[i] - path[i - 1]);
                else fwd = (path[i + 1] - path[i - 1]);
    
                if (mode == RoadConformMode.SurfaceProjectToTerrain)
                    fwd.y = 0f;
    
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                fwd.Normalize();
    
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
    
                Vector3 leftP = p - right * halfWidth;
                Vector3 rightP = p + right * halfWidth;
    
                if (mode == RoadConformMode.SurfaceProjectToTerrain)
                {
                    leftP.y = SampleY(leftP) + yOffset;
                    rightP.y = SampleY(rightP) + yOffset;
                }
                else
                {
                    leftP.y = p.y + yOffset;
                    rightP.y = p.y + yOffset;
                }
    
                verts[i * 2 + 0] = leftP;
                verts[i * 2 + 1] = rightP;
    
                // Tile texture every 10 meters along the road
                float v = lens[i] / 10f;
                uvs[i * 2 + 0] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }
    
            int ti = 0;
            for (int i = 0; i < n - 1; i++)
            {
                int i0 = i * 2;
                int i1 = i * 2 + 1;
                int i2 = (i + 1) * 2;
                int i3 = (i + 1) * 2 + 1;
    
                tris[ti++] = i0; tris[ti++] = i2; tris[ti++] = i1;
                tris[ti++] = i1; tris[ti++] = i2; tris[ti++] = i3;
            }
    
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
    
            mf.sharedMesh = mesh;
    
            if (addCollider)
            {
                var mc = go.GetComponent<MeshCollider>();
                if (mc == null) mc = Undo.AddComponent<MeshCollider>(go);
                mc.sharedMesh = null;
                mc.sharedMesh = mesh;
            }
            else
            {
                var mc = go.GetComponent<MeshCollider>();
                if (mc != null) Undo.DestroyObjectImmediate(mc);
            }
    
            return go;
        }
    
        public static Material GetOrCreateDefaultRoadMaterial()
        {
            if (_defaultRoadMat != null) return _defaultRoadMat;
    
            var loaded = AssetDatabase.LoadAssetAtPath<Material>(DefaultMatPath);
            if (loaded != null)
            {
                _defaultRoadMat = loaded;
                return _defaultRoadMat;
            }
    
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(DefaultMatFolder))
                AssetDatabase.CreateFolder("Assets/Generated", "Roads");
    
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return null;
    
            var mat = new Material(shader) { name = "DefaultRoad" };
    
            // Generate realistic 512x512 asphalt texture with lines
            int res = 512;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, true);
            tex.name = "DefaultRoadTex";
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
    
            var cols = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    // Noise for asphalt
                    float n = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.15f;
                    float baseVal = 0.12f + n;
                    Color c = new Color(baseVal, baseVal, baseVal, 1f);
    
                    float u = x / (float)res;
                    
                    // Edge lines (solid white)
                    if ((u > 0.05f && u < 0.08f) || (u > 0.92f && u < 0.95f))
                    {
                        c = Color.Lerp(c, new Color(0.9f, 0.9f, 0.9f, 1f), 0.8f);
                    }
                    
                    // Center dashed lines (yellow)
                    if (u > 0.48f && u < 0.52f)
                    {
                        float v = y / (float)res;
                        if (v % 0.5f < 0.25f) // Dashed pattern
                        {
                            c = Color.Lerp(c, new Color(0.9f, 0.8f, 0.1f, 1f), 0.85f);
                        }
                    }
    
                    cols[y * res + x] = c;
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            
            string texPath = DefaultMatFolder + "/DefaultRoadTex.png";
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
    
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", savedTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", savedTex);
            
            // Set some smoothness/metallic
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.1f);
    
            AssetDatabase.CreateAsset(mat, DefaultMatPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
    
            _defaultRoadMat = AssetDatabase.LoadAssetAtPath<Material>(DefaultMatPath);
            return _defaultRoadMat != null ? _defaultRoadMat : mat;
        }

        public static Material GetOrCreateDefaultMarkingMaterial()
        {
            string markPath = DefaultMatFolder + "/DefaultRoadMarking.mat";
            var loaded = AssetDatabase.LoadAssetAtPath<Material>(markPath);
            if (loaded != null) return loaded;

            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(DefaultMatFolder))
                AssetDatabase.CreateFolder("Assets/Generated", "Roads");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = "DefaultRoadMarking" };
            mat.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            
            AssetDatabase.CreateAsset(mat, markPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            return AssetDatabase.LoadAssetAtPath<Material>(markPath);
        }
    }
}
