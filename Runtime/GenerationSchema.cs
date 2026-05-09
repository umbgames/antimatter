using System;
using System.Collections.Generic;
using UnityEngine;

namespace Antimatter
{
    [Serializable]
    public class GameGeneration
    {
        public string version;

        public string planExplanation;
        public string planSuggestions;

        public GenerationSettings settings;

        public List<SceneDefinition> scenes;
        public List<ScriptDefinition> scripts;
        public FlowDefinition flow;

        public AssetPlan assets;
    }

    [Serializable]
    public class GenerationSettings
    {
        public bool ensureCameraAndLight = true;
        public WorldGenSettings worldGen;
    }

    // ───────────────────── WORLD GEN (AI-CONTROLLED) ─────────────────────

    [Serializable]
    public class WorldGenSettings
    {
        public bool enabled = false;

        // ── Terrain ──
        public string terrainPreset = "Hills";
        public SchemaTerrainParams terrain;

        // ── Roads ──
        public List<Vector3> roadHubs;
        public SchemaRoadSettings roads;

        // ── Settlements ──
        public List<SettlementNode> settlements;
        public SchemaSettlementDefaults settlementDefaults;
    }

    /// <summary>
    /// Full terrain generation parameters exposed to the AI.
    /// When null/zeroed the system falls back to preset defaults.
    /// </summary>
    [Serializable]
    public class SchemaTerrainParams
    {
        public int heightmapResolution;     // e.g. 513, 257
        public int sizeXZ;                  // terrain width/length in metres
        public int heightY;                 // max height in metres
        public int seed;

        // Noise
        public int octaves;
        public float lacunarity;
        public float persistence;
        public float baseFrequency;

        // Shaping
        public float ridgeStrength;         // 0..1
        public float erosionPasses;         // 0..50
        public float seaLevel;              // 0..1
        public float valleyWidth;           // 0..1
        public float valleyDepth;           // 0..1

        // Texturing / Environment
        public string biome;                // e.g. "Desert", "Snow", "Lush"
    }

    /// <summary>
    /// Full road generation parameters exposed to the AI.
    /// When null the system uses sensible defaults.
    /// </summary>
    [Serializable]
    public class SchemaRoadSettings
    {
        public int seed;

        // Grid / Network
        public int gridResolution;          // 64..1024
        public int hubCount;                // 4..64
        public int extraConnections;        // 0..32
        public float hubMinSpacingWorld;    // 10..500

        // Surface constraints
        public float maxSurfaceSlopeDeg;    // 1..30
        public float hardBlockSlopeDeg;     // 1..45
        public float maxRoadGrade;          // 0.01..0.25

        // Cost weights
        public float heightPenalty;
        public float slopePenalty;
        public float valleyBonus;
        public float turnPenalty;

        // Geometry
        public float roadHalfWidth;         // 1..20
        public float roadYOffset;           // 0..1

        // Terrain stamping
        public float stampFalloff;
        public float stampBlend;
        public float stampCutDepth;

        // Smoothing
        public float simplifyToleranceWorld;
        public int chaikinIterations;

        // Tunnel
        public float tunnelTriggerCoverMeters;
        public float tunnelInnerRadius;
        public float tunnelWallThickness;
        public float tunnelPortalLength;

        // Bridge
        public float bridgeTriggerDropMeters;
        public float minBridgeLength;
        public float bridgeDeckClearance;
        public float bridgeDeckThickness;
        public float bridgePillarSpacing;
        public float bridgePillarWidth;
        public float bridgeRailHeight;
        public float bridgeRailThickness;
        public bool bridgeEnableTruss;
        public float bridgeTrussHeight;
        public float bridgeTrussThickness;
        public float bridgeTrussPostSpacing;
    }

    /// <summary>
    /// Global settlement defaults exposed to the AI.
    /// Individual SettlementNode entries can override these.
    /// </summary>
    [Serializable]
    public class SchemaSettlementDefaults
    {
        public int seed;
        public string style;                // Auto, ModernCityBlocks, MedievalTown, Suburban
        public int maxSettlements;

        // Lot / building sizing
        public float lotMinSize;
        public float lotMaxSize;
        public float buildingHeightMin;
        public float buildingHeightMax;
        public int buildingsPerTown;
        public int buildingsPerVillage;

        // Terrain filters
        public float maxSlopeDeg;
        public float roadClearance;

        // Pad stamping
        public float padRadius;
        public float padFalloff;
        public float padBlend;

        // Settlement radius
        public float settlementRadius;
    }

    [Serializable]
    public class SettlementNode
    {
        public Vector3 center;
        public float radius;
        public int buildingCount;
        public string style;                // Auto, ModernCityBlocks, MedievalTown, Suburban

        // Per-settlement overrides (0 = use global default)
        public float lotMinSize;
        public float lotMaxSize;
        public float buildingHeightMin;
        public float buildingHeightMax;
        public float roadClearance;
        public float padRadius;
    }

    // ───────────────────── ASSETS ─────────────────────

    [Serializable]
    public class AssetPlan
    {
        public List<MaterialDefinition> materials;
        public List<TerrainDataDefinition> terrainData;
    }

    [Serializable]
    public class SceneDefinition
    {
        public string name;
        public List<ObjectDefinition> objects;
        public SceneSettings settings;
    }

    [Serializable]
    public class SceneSettings
    {
        public bool? ensureCameraAndLight;
    }

    [Serializable]
    public class ObjectDefinition
    {
        public string id;
        public string kind;                // "Primitive" | "Empty" | "Builtin" | "Terrain"
        public string primitiveType;
        public string builtinType;
        public string terrainDataId;
        public string parentId;

        public Vector3 position;
        public Vector3 rotationEuler;
        public Vector3 scale;

        public List<ComponentDefinition> components;
        public string materialId;
    }

    [Serializable]
    public class ComponentDefinition
    {
        public string script;
        public string builtinComponent;
        public List<PropertyValue> properties;
    }

    [Serializable]
    public class PropertyValue
    {
        public string name;
        public string type;                 // "float"|"int"|"bool"|"string"|"color"|"vector3"
        public float floatValue;
        public int intValue;
        public bool boolValue;
        public string stringValue;
        public Color colorValue;
        public Vector3 vector3Value;
    }

    [Serializable]
    public class ScriptDefinition
    {
        public string name;
        public string path;
        public string code;
    }

    [Serializable]
    public class FlowDefinition
    {
        public string restartKey;
        public bool nextSceneOnTrigger;
    }

    [Serializable]
    public class MaterialDefinition
    {
        public string id;
        public string shader;
        public Color color;
        public float metallic;
        public float smoothness;
    }

    [Serializable]
    public class TerrainDataDefinition
    {
        public string id;
        public Vector3 size;
        public int heightmapResolution;
        public string preset;

        // Full terrain params (optional — overrides preset)
        public SchemaTerrainParams terrainParams;
    }
}
