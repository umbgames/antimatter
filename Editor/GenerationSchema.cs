using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameGeneration
{
    public string version;

    public string planExplanation;
    public string planSuggestions;

    // NEW: global settings
    public GenerationSettings settings;

    public List<SceneDefinition> scenes;
    public List<ScriptDefinition> scripts;
    public FlowDefinition flow;

    // NEW: asset definitions (materials, terrain data, etc.)
    public AssetPlan assets;
}

[Serializable]
public class GenerationSettings
{
    // If true, executor injects missing camera/light per scene,
    // unless scene explicitly disables it.
    public bool ensureCameraAndLight = true;

    // NEW: AI-controlled world generation flags (terrain/roads/settlements).
    public WorldGenSettings worldGen;
}

[Serializable]
public class WorldGenSettings
{
    public bool enabled = false;

    // Schema enum strings: "Plains","Hills","Mountains","Valley","Canyon","Plateau","Island"
    public string terrainPreset = "Hills";

    // AI-Dictated parameters replacing boolean flags
    public List<Vector3> roadHubs;
    public List<SettlementNode> settlements;
}

[Serializable]
public class SettlementNode
{
    public Vector3 center;
    public float radius;
    public int buildingCount;
    public string style; // Auto, ModernCityBlocks, MedievalTown, Suburban
}

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

    // NEW: per-scene override
    public SceneSettings settings;
}

[Serializable]
public class SceneSettings
{
    // If null, inherit from global ensureCameraAndLight.
    public bool? ensureCameraAndLight;
}

[Serializable]
public class ObjectDefinition
{
    public string id;

    // NEW: broader object kinds
    // "Primitive" | "Empty" | "Builtin" | "Terrain"
    public string kind;

    // If kind == Primitive: "Cube" | "Sphere" | "Capsule" | "Plane" | "Cylinder" | "Quad"
    public string primitiveType;

    // If kind == Builtin: "Camera" | "DirectionalLight" | "PointLight" | "SpotLight"
    public string builtinType;

    // If kind == Terrain: reference terrainData id
    public string terrainDataId;

    // Hierarchy support (optional)
    public string parentId;

    public Vector3 position;
    public Vector3 rotationEuler;
    public Vector3 scale;

    // Components: still for MonoBehaviours, plus basic built-in components via a whitelist mechanism
    public List<ComponentDefinition> components;

    // NEW: renderer/material link (optional)
    public string materialId;
}

[Serializable]
public class ComponentDefinition
{
    // Either:
    // - script = MonoBehaviour class name (preferred)
    // or
    // - builtinComponent = whitelisted built-in component name ("Rigidbody", "BoxCollider", "SphereCollider", "CapsuleCollider")
    public string script;
    public string builtinComponent;

    public List<PropertyValue> properties;
}

[Serializable]
public class PropertyValue
{
    public string name;                 // field/property
    public string type;                 // "float"|"int"|"bool"|"string"|"color"|"vector3"
    public float floatValue;
    public int intValue;
    public bool boolValue;
    public string stringValue;

    // NEW
    public Color colorValue;
    public Vector3 vector3Value;
}

[Serializable]
public class ScriptDefinition
{
    public string name;
    public string path;                 // Must be under Assets/
    public string code;                 // C# content
}

[Serializable]
public class FlowDefinition
{
    public string restartKey;           // e.g. "R"
    public bool nextSceneOnTrigger;
}

[Serializable]
public class MaterialDefinition
{
    public string id;           // stable reference id, e.g. "Mat_Player"
    public string shader;       // e.g. "Standard" or "Universal Render Pipeline/Lit" (executor will try)
    public Color color;         // base color
    public float metallic;
    public float smoothness;
}

[Serializable]
public class TerrainDataDefinition
{
    public string id;                   // stable reference id, e.g. "Terrain_Main"
    public Vector3 size;                // terrain size
    public int heightmapResolution;     // e.g. 513
    public string preset;              // optional: matches schema enum
    // Optional later: heights, splats, trees, details, etc.
}
