using System;
using System.Collections.Generic;
using UnityEngine;

namespace Antimatter
{
    [CreateAssetMenu(menuName = "Antimatter/Terrain Asset Profile", fileName = "TerrainAssetProfile")]
    public class TerrainAssetProfile : ScriptableObject
    {
        [Header("General Settings")]
        public int alphamapResolution = 512;
        public int detailResolution = 1024;
        public int detailResolutionPerPatch = 32;

        [Header("Rules")]
        public List<TextureRule> textureRules = new List<TextureRule>();
        public List<DetailRule> detailRules = new List<DetailRule>();
        public List<TreeRule> treeRules = new List<TreeRule>();
        public List<RockRule> rockRules = new List<RockRule>();

        [Serializable]
        public class TextureRule
        {
            public TerrainLayer layer;
            [Range(0, 1)] public float minHeight = 0f;
            [Range(0, 1)] public float maxHeight = 1f;
            [Range(0, 90)] public float minSlope = 0f;
            [Range(0, 90)] public float maxSlope = 90f;
            public float noiseScale = 0.05f;
            [Range(0, 1)] public float noiseAmount = 0.5f;
            public float weight = 1f;
        }

        [Serializable]
        public class DetailRule
        {
            public DetailPrototype prototype;
            [Range(0, 1)] public float minHeight = 0f;
            [Range(0, 1)] public float maxHeight = 1f;
            [Range(0, 90)] public float minSlope = 0f;
            [Range(0, 90)] public float maxSlope = 90f;
            public float noiseScale = 0.1f;
            [Range(0, 1)] public float noiseAmount = 0.5f;
            public int density = 1;
        }

        [Serializable]
        public class TreeRule
        {
            public GameObject prefab;
            public int count = 1000;
            [Range(0, 1)] public float minHeight = 0f;
            [Range(0, 1)] public float maxHeight = 1f;
            [Range(0, 90)] public float minSlope = 0f;
            [Range(0, 90)] public float maxSlope = 90f;
            public float noiseScale = 0.05f;
            [Range(0, 1)] public float noiseAmount = 0.5f;
            public float minScale = 0.8f;
            public float maxScale = 1.2f;
        }

        [Serializable]
        public class RockRule
        {
            public GameObject prefab;
            public int count = 200;
            [Range(0, 1)] public float minHeight = 0f;
            [Range(0, 1)] public float maxHeight = 1f;
            [Range(0, 90)] public float minSlope = 0f;
            [Range(0, 90)] public float maxSlope = 90f;
            public float noiseScale = 0.1f;
            [Range(0, 1)] public float noiseAmount = 0.5f;
            public float minScale = 0.5f;
            public float maxScale = 2f;
            public bool alignToNormal = true;
        }
    }
}
