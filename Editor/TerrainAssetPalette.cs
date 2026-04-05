using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Terrain/Terrain Asset Palette", fileName = "TerrainAssetPalette")]
public class TerrainAssetPalette : ScriptableObject
{
    [Header("Textures (Terrain Layers)")]
    [Tooltip("Used for beaches / shallow coasts.")]
    public TerrainLayer sand;

    [Tooltip("Used for low elevations / plains.")]
    public TerrainLayer grass;

    [Tooltip("Used for mid elevations / slopes.")]
    public TerrainLayer dirt;

    [Tooltip("Used for rocky areas / steep slopes.")]
    public TerrainLayer rock;

    [Tooltip("Used for high elevations (mountain tops).")]
    public TerrainLayer snow;

    [Header("Trees (Tree Prototypes)")]
    public GameObject[] treePrefabs;

    [Header("Rocks (Tree Prototypes, but rocks as 'trees' is common)")]
    public GameObject[] rockPrefabs;

    [Header("Grass / Details (Detail Prototypes)")]
    public DetailPrototypeEntry[] details;

    [Serializable]
    public class DetailPrototypeEntry
    {
        public enum DetailMode { Texture, Mesh }

        public string name;
        public DetailMode mode;

        // If Texture:
        public Texture2D detailTexture;

        // If Mesh:
        public GameObject detailMesh;

        [Range(0.1f, 10f)] public float minWidth = 1f;
        [Range(0.1f, 10f)] public float maxWidth = 2f;
        [Range(0.1f, 10f)] public float minHeight = 1f;
        [Range(0.1f, 10f)] public float maxHeight = 2f;

        [Range(0f, 1f)] public float noiseSpread = 0.2f;
        public bool useInstancing = true;
    }

    // Convenience: order of layers we’ll push into TerrainData
    public TerrainLayer[] BuildOrderedLayers()
    {
        // Keep stable order: sand, grass, dirt, rock, snow
        return new[] { sand, grass, dirt, rock, snow };
    }
}
