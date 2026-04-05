using UnityEngine;

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
    [Range(1f, 45f)] public float maxSlopeDeg = 14f;
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

    [Header("Road Clearance")]
    [Range(2f, 200f)] public float roadClearance = 10f;

    [Header("Terrain Stamping (per lot)")]
    [Range(0f, 60f)] public float padRadius = 10f;
    [Range(0f, 60f)] public float padFalloff = 6f;
    [Range(0f, 1f)] public float padBlend = 0.9f;

    // ---------------- Modern blocks ----------------
    [Header("Modern Blocks")]
    [Range(2, 30)] public int modernGridCellsPerSide = 10;
    [Range(8f, 160f)] public float modernBlockCellSize = 26f;
    [Range(0.2f, 30f)] public float modernStreetGap = 6f; // space between parcels

    // ---------------- Medieval ----------------
    [Header("Medieval Town")]
    public bool medievalAddMarket = true;
    public bool medievalAddWalls = false;
    [Range(2, 8)] public int medievalRingCount = 4;
    [Range(0.2f, 1.2f)] public float medievalRingSpacingFactor = 0.22f; // portion of radius per ring
    [Range(1f, 30f)] public float medievalWallThickness = 3f;

    // ---------------- Suburban ----------------
    [Header("Suburban")]
    [Range(1, 12)] public int suburbanCulDeSacCount = 5;
    [Range(25f, 600f)] public float suburbanCulDeSacRadius = 30f;
    [Range(0.2f, 30f)] public float suburbanYardPadding = 3.0f;
}
