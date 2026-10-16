using UnityEngine;

namespace Antimatter
{
    
    public enum SettlementStyle
    {
        Auto = 0,
        ModernCityBlocks = 1,
        Suburban = 2,
        RuralVillage = 3
    }
    
    [System.Serializable]
    public class SettlementGenSettings
    {
        [Header("Style")]
        public SettlementStyle style = SettlementStyle.Auto;
    
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
    
        [Header("Lot / Building Size")]
        [Range(6f, 80f)] public float lotMinSize = 10f;
        [Range(10f, 200f)] public float lotMaxSize = 30f;
    
        [Header("Road Clearance")]
        [Range(2f, 200f)] public float roadClearance = 10f;
    
        [Header("Terrain Stamping (per lot)")]
        [Range(0f, 60f)] public float padRadius = 14f;
        [Range(0f, 60f)] public float padFalloff = 8f;
        [Range(0f, 1f)] public float padBlend = 1.0f;
    }
}
