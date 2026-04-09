using UnityEngine;

namespace Antimatter
{
    
    [System.Serializable]
    public class RoadGenSettings
    {
        [Header("Grid Sampling")]
        [Range(64, 1024)] public int gridResolution = 256;
    
        [Header("Hubs / Network")]
        [Range(4, 64)] public int hubCount = 18;
        [Range(0, 32)] public int extraConnections = 6;
        [Range(10f, 500f)] public float hubMinSpacingWorld = 80f;
    
        [Header("Surface Constraints")]
        [Range(1f, 30f)] public float maxSurfaceSlopeDeg = 12f;   // soft limit (penalty starts)
        [Range(1f, 45f)] public float hardBlockSlopeDeg = 18f;    // hard block for surface routing
        [Range(0.01f, 0.25f)] public float maxRoadGrade = 0.10f;  // 10% grade default
    
        [Header("Cost Weights")]
        [Range(0f, 20f)] public float heightPenalty = 4f;         // prefer lowlands/valleys
        [Range(0f, 50f)] public float slopePenalty = 16f;         // avoid steep
        [Range(0f, 10f)] public float valleyBonus = 2.5f;         // prefer local minima
        [Range(0f, 10f)] public float turnPenalty = 1.2f;         // reduce zig-zags
    
        [Header("Road Geometry")]
        [Range(1f, 20f)] public float roadHalfWidth = 3.5f;
        [Range(0f, 1f)] public float roadYOffset = 0.02f;
    
        [Header("Terrain Stamping")]
        [Range(0f, 10f)] public float stampFalloff = 4f;
        [Range(0f, 10f)] public float stampBlend = 2f;
        [Range(0f, 5f)] public float stampCutDepth = 0.8f;
    
        [Header("Bridge")]
        [Range(1f, 50f)] public float bridgeTriggerDropMeters = 6f;
        [Range(2f, 200f)] public float minBridgeLength = 25f;
    
        [Range(0.1f, 5f)] public float bridgeDeckThickness = 0.6f;
        [Range(5f, 120f)] public float bridgePillarSpacing = 18f;
        [Range(0.5f, 12f)] public float bridgePillarWidth = 2.5f;
        [Range(0f, 1f)] public float bridgeDeckClearance = 0.2f;
    
        // Visual upgrades
        [Range(0.2f, 3f)] public float bridgeRailHeight = 1.2f;
        [Range(0.05f, 1f)] public float bridgeRailThickness = 0.18f;
    
        [Range(0.05f, 1.2f)] public float bridgeMarkingWidth = 0.15f;
        [Range(0.01f, 0.1f)] public float bridgeMarkingYOffset = 0.02f;
        [Range(0.5f, 12f)] public float bridgeDashLength = 3.0f;
        [Range(0.2f, 8f)] public float bridgeDashGap = 2.0f;
    
        public bool bridgeEnableTruss = true;
        [Range(0.5f, 12f)] public float bridgeTrussHeight = 4.5f;
        [Range(0.05f, 1.2f)] public float bridgeTrussThickness = 0.25f;
        [Range(2f, 25f)] public float bridgeTrussPostSpacing = 6f;
    
    
    
        [Header("Smoothing")]
        [Range(0f, 2f)] public float simplifyToleranceWorld = 2.0f;
        [Range(0, 6)] public int chaikinIterations = 2;
    
        [Header("Tunnel Routing")]
        [Range(1f, 50f)] public float tunnelTriggerCoverMeters = 8f; // if terrain is > grade line by this much => tunnel
        [Range(2f, 50f)] public float tunnelInnerRadius = 6f;        // half-size, roughly
        [Range(0.5f, 10f)] public float tunnelWallThickness = 1.5f;
        [Range(0f, 10f)] public float tunnelPortalLength = 6f;
    
        [Header("Tunnel Entrance Holes (Portal Only)")]
        [Tooltip("How far into the mountain to cut terrain holes at each tunnel entrance.")]
        [Range(0f, 40f)] public float tunnelEntranceHoleLength = 8f;
    
        [Tooltip("Extra hole radius padding beyond innerRadius+wallThickness (prevents terrain clipping at the edges).")]
        [Range(0f, 10f)] public float tunnelEntranceHolePadding = 1.0f;
    
        [Tooltip("Sampling step for hole cutting along the portal segment. Smaller = smoother hole edge.")]
        [Range(0.25f, 5f)] public float tunnelEntranceHoleStep = 1.5f;
    
        [Header("Determinism")]
        public int seed = 1337;
    }
}
