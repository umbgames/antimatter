using UnityEditor;
using UnityEngine;

public class TerrainGeneratorWindow : EditorWindow
{
    private string terrainName = "Generated Terrain";
    private TerrainGenerator.TerrainPreset preset = TerrainGenerator.TerrainPreset.Hills;
    private int seed = 1337;
    private Vector3 position = Vector3.zero;

    private TerrainAssetProfile assetProfile;

    private bool clearOldRocks = true;

    [MenuItem("Tools/Terrain/Generator + Assets")]
    public static void Open()
    {
        GetWindow<TerrainGeneratorWindow>("Terrain Generator");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Terrain Generation", EditorStyles.boldLabel);

        terrainName = EditorGUILayout.TextField("Name", terrainName);
        preset = (TerrainGenerator.TerrainPreset)EditorGUILayout.EnumPopup("Preset", preset);
        seed = EditorGUILayout.IntField("Seed", seed);
        position = EditorGUILayout.Vector3Field("Position", position);

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Terrain Asset Profile", EditorStyles.boldLabel);
        assetProfile = (TerrainAssetProfile)EditorGUILayout.ObjectField("Profile", assetProfile, typeof(TerrainAssetProfile), false);
        clearOldRocks = EditorGUILayout.ToggleLeft("Clear old rocks under __Rocks", clearOldRocks);

        EditorGUILayout.Space(10);

        using (new EditorGUI.DisabledScope(assetProfile == null))
        {
            if (GUILayout.Button("Generate Terrain + Apply Profile", GUILayout.Height(32)))
            {
                Generate();
            }
        }

        EditorGUILayout.Space(6);

        if (GUILayout.Button("Generate Terrain Only", GUILayout.Height(24)))
        {
            Generate(onlyTerrain: true);
        }

        EditorGUILayout.HelpBox(
            "Workflow:\n" +
            "1) Create a Terrain Asset Profile (Right click in Project → Create → Terrain → Terrain Asset Profile)\n" +
            "2) Fill texture rules, detail rules, tree rules, rock rules.\n" +
            "3) Use this window to generate + populate.",
            MessageType.Info);
    }

    private void Generate(bool onlyTerrain = false)
    {
        var go = TerrainGenerator.CreateOrReplaceTerrain(terrainName, preset, seed, position);

        if (!onlyTerrain && assetProfile != null)
        {
            var terrain = go.GetComponent<Terrain>();
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Apply Terrain Asset Profile");
            TerrainAssetApplier.ApplyAll(terrain, assetProfile, seed, clearOldRocks);

            EditorUtility.SetDirty(terrain.terrainData);
            AssetDatabase.SaveAssets();
        }

        Selection.activeGameObject = go;
    }
}
