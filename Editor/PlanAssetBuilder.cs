using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class PlanAssetBuilder
{
    public const string MaterialsFolder = "Assets/AI_Generated/Materials";
    public const string TerrainFolder   = "Assets/AI_Generated/Terrain";

    public static Dictionary<string, Material> BuildMaterials(GameGeneration gen)
    {
        EnsureFolder("Assets/AI_Generated");
        EnsureFolder(MaterialsFolder);

        var map = new Dictionary<string, Material>();
        if (gen.assets?.materials == null) return map;

        foreach (var m in gen.assets.materials)
        {
            if (string.IsNullOrWhiteSpace(m.id)) continue;

            string path = $"{MaterialsFolder}/{m.id}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                var shader = FindShaderBestEffort(m.shader);
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            ApplyMaterialProps(mat, m);
            EditorUtility.SetDirty(mat);

            map[m.id] = mat;
        }

        AssetDatabase.SaveAssets();
        return map;
    }

    public static Dictionary<string, TerrainData> BuildTerrainData(GameGeneration gen)
    {
        EnsureFolder("Assets/AI_Generated");
        EnsureFolder(TerrainFolder);

        var map = new Dictionary<string, TerrainData>();
        if (gen.assets?.terrainData == null) return map;

        foreach (var t in gen.assets.terrainData)
        {
            if (string.IsNullOrWhiteSpace(t.id)) continue;

            string path = $"{TerrainFolder}/{t.id}.asset";
            var td = AssetDatabase.LoadAssetAtPath<TerrainData>(path);

            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, path);
            }

            td.size = t.size == Vector3.zero ? new Vector3(200, 50, 200) : t.size;
            if (t.heightmapResolution >= 33) td.heightmapResolution = t.heightmapResolution;

            EditorUtility.SetDirty(td);
            map[t.id] = td;
        }

        AssetDatabase.SaveAssets();
        return map;
    }

    private static Shader FindShaderBestEffort(string shaderName)
    {
        // Deterministic: try requested name, then common fallbacks
        if (!string.IsNullOrWhiteSpace(shaderName))
        {
            var s = Shader.Find(shaderName);
            if (s != null) return s;
        }

        // Fallbacks
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp != null) return urp;

        var standard = Shader.Find("Standard");
        if (standard != null) return standard;

        // Absolute fallback
        return Shader.Find("Sprites/Default");
    }

    private static void ApplyMaterialProps(Material mat, MaterialDefinition m)
    {
        // BaseColor property varies by shader; try common names deterministically.
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", m.color);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", m.color);

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", m.metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", m.smoothness);

        // URP Lit uses _Smoothness in some variants; not perfect, but stable.
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        // Create recursively
        var parts = path.Split('/');
        string current = parts[0]; // Assets
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
