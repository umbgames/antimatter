using UnityEngine;

namespace Antimatter
{
    
    public enum SettlementStyle
    {
        Auto = 0,
        ModernCityBlocks = 1,
        MedievalTown = 2,
        Suburban = 3
    }
    
    [System.Serializable]
    public class SettlementGenSettings
    {
        [Header("Style")]
        public SettlementStyle style = SettlementStyle.Auto;
    
        [Tooltip("If Auto: probability weights after prompt detection (fallback).")]
        [Range(0f, 1f)] public float autoModernWeight = 0.45f;
        [Range(0f, 1f)] public float autoMedievalWeight = 0.30f;
        [Range(0f, 1f)] public float autoSuburbanWeight = 0.25f;
    
        [Header("Core")]
        public int seed = 1337;
        [Range(1, 64)] public int maxSettlements = 10;
    
        [Header("Hub-based Towns")]
        [Range(0f, 1f)] public float hubTownChance = 0.55f;
        [Range(10f, 5000f)] public float hubTownMinSpacing = 180f;
    
        [Header("Roadside Villages")]
        public bool enableRoadsideVillages = true;
        [Range(50f, 2000f)] public float roadsideStep = 300f;
        [Range(0f, 1f)] public float roadsideVillageChance = 0.35f;
    
        [Header("Terrain Filters")]
        [Range(1f, 90f)] public float maxSlopeDeg = 45f;
        [Range(0f, 1f)] public float preferLowlands = 0.7f;
        [Range(0f, 1f)] public float avoidHighPeaks = 0.85f;
    
        [Header("Settlement Size / Density")]
        [Range(40f, 2000f)] public float settlementRadius = 140f;
        [Range(8, 600)] public int buildingsPerTown = 70;
        [Range(5, 300)] public int buildingsPerVillage = 28;
    
        [Header("Lot / Building Size (generic)")]
        [Range(6f, 80f)] public float lotMinSize = 10f;
        [Range(10f, 200f)] public float lotMaxSize = 22f;
        [Range(4f, 200f)] public float buildingHeightMin = 6f;
        [Range(8f, 400f)] public float buildingHeightMax = 22f;

        [Header("Prefabs (Optional)")]
        [Tooltip("Used for dense/core areas like Modern City centers or Medieval Markets.")]
        public GameObject[] coreCommercialPrefabs;
        [Tooltip("Used for spreading suburban areas, village outskirts, or general housing.")]
        public GameObject[] residentialPrefabs;
        [Tooltip("Used when prompt suggests industrial complexes, factories, or ruined buildings.")]
        public GameObject[] industrialPrefabs;
    
        [Header("Road Clearance")]
        [Range(2f, 200f)] public float roadClearance = 10f;
    
        [Header("Terrain Stamping (per lot)")]
        [Range(0f, 60f)] public float padRadius = 14f;
        [Range(0f, 60f)] public float padFalloff = 8f;
        [Range(0f, 1f)] public float padBlend = 1.0f;
    
        // ---------------- Modern blocks ----------------
        [Header("Modern Blocks")]
        [Range(2, 30)] public int modernGridCellsPerSide = 10;
        [Range(8f, 160f)] public float modernBlockCellSize = 26f;
        [Range(0.2f, 30f)] public float modernStreetGap = 6f; // space between parcels
    
        // ---------------- Medieval ----------------
        [Header("Medieval Town")]
        [Header("Medieval (Village)")]
        [Range(2, 12)] public int medievalRingCount = 4;
        [Range(0.01f, 1.5f)] public float medievalRingSpacingFactor = 0.45f;
        public bool medievalAddMarket = true;
        public bool medievalAddWalls = true;
        [Range(1f, 20f)] public float medievalWallThickness = 6f;
    
        // ---------------- Suburban ----------------
        [Header("Suburban settings")]
        [Range(1, 10)] public int suburbanCulDeSacCount = 3;
        [Range(5f, 60f)] public float suburbanCulDeSacRadius = 25f;
        [Range(0f, 40f)] public float suburbanYardPadding = 12f;
    }
}
