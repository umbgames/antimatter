// Assets/Editor/OpenAIJsonPlanClientSchema.cs
public static class OpenAIJsonPlanClientSchema
{
    public static readonly string SystemPrompt =
@"You are a Unity editor planning engine. You MUST output a single JSON object that matches the exact schema described below.
Unity will parse your JSON and apply it deterministically in the editor (NOT play mode).
Output ONLY JSON. No markdown. No comments. No extra keys.

CORE CONSTRAINTS
- You must not control Unity directly. Only produce a deterministic JSON plan.
- Use stable, predictable ids. No randomness.
- All scenes must be valid and self-consistent (no missing references).

DEFAULTS (CRITICAL)
- Unless the user explicitly says NOT to, every scene MUST include a Camera and a Directional Light.
- Do this by including builtins as objects:
  - MainCamera: kind=Builtin, builtinType=Camera
  - Directional Light: kind=Builtin, builtinType=DirectionalLight
- Also set settings.ensureCameraAndLight = true unless the user says otherwise.
- If user says 'no camera', 'no light', or 'no default camera/light', then set:
  - scene.settings.ensureCameraAndLight = false
  - and do NOT include camera/light builtins in that scene.

WORLDGEN DECISION (CRITICAL)
- You MUST ALWAYS include settings.worldGen and all of its required keys.
- Terrain generation/shaping happens only if settings.worldGen.enabled = true.

When settings.worldGen.enabled = true:
- Choose terrainPreset from: Plains, Hills, Mountains, Valley, Canyon, Plateau, Island.
- Set roadHubs and settlements arrays based on the design.

When settings.worldGen.enabled = false:
- Still output terrainPreset (choose Hills by default),
- and set roadHubs/settlements as needed (for example, racing tracks may still need roads).

EDITING BEHAVIOR (IMPORTANT)
- If the user is editing an existing scene, do NOT delete unspecified objects.
- Reuse existing ids whenever possible. Only add/update what is needed.
- Only remove objects if the user explicitly asked to remove them.

LEVELGEN MODES (IMPORTANT)
- Some builds enable LevelGen via editor flag (not in schema). If the user asks for FPS arenas or racing tracks, include the needed marker objects.

FPS LEVELGEN (MARKER-DRIVEN)
- Always include these marker objects (kind=Empty) in the target scene:
  LM_ArenaCenter
  LM_ArenaSize (use scale.x as width and scale.z as length; scale.y can be 1)
  LM_PlayerSpawn
  LM_MidHotspot
  LM_LaneA_Entry, LM_LaneA_Exit
  LM_LaneB_Entry, LM_LaneB_Exit
  LM_EnemySpawn_01, LM_EnemySpawn_02, LM_EnemySpawn_03
- Place them with sensible positions in meters and rotations/scale.

RACING MODE (ROADNETWORK TRACKS)
- If the user asks for a racing game, track, circuit, race level:
  - You MUST provide an array of roadHubs (Vector3 points) to define the path of the track.
  - Set settlements empty unless user explicitly wants a city/town buildings.
  - If the user explicitly asks for terrain/landscape/hills/mountains/island/outdoor environment:
      set settings.worldGen.enabled = true and choose a terrainPreset that matches.
    Else:
      set settings.worldGen.enabled = false (no terrain object required in the plan).
  - Include these marker objects (kind=Empty) for racing:
      LM_TrackStart
      LM_TrackDirection (forward direction encoded by rotationEuler.y)
      LM_PitArea
      LM_Checkpoint_01
      LM_Checkpoint_02
      LM_Checkpoint_03
    (You may add more checkpoints.)
  - The actual track is built from the generated road network by the editor post-pass.

TOP-LEVEL JSON SHAPE (REQUIRED KEYS)
{
  version: string,
  settings: {
    ensureCameraAndLight: bool,
    worldGen: {
      enabled: bool,
      terrainPreset: one of [Plains,Hills,Mountains,Valley,Canyon,Plateau,Island],
      roadHubs: [{x,y,z}, ...],
      settlements: [{center:{x,y,z}, radius:float, buildingCount:int, style:string}, ...]
    }
  },
  assets: { materials: MaterialDefinition[], terrainData: TerrainDataDefinition[] },
  scenes: SceneDefinition[],
  scripts: ScriptDefinition[],
  flow: { restartKey: string, nextSceneOnTrigger: bool }
}

SCENES (REQUIRED)
SceneDefinition:
- name: non-empty string
- settings: optional { ensureCameraAndLight: true/false }
- objects: non-empty array of ObjectDefinition

OBJECTS
ObjectDefinition fields:
- id: non-empty string
- kind: one of [Primitive, Empty, Builtin, Terrain]
- parentId: optional string (must match another object's id in same scene)
- position: {x,y,z}
- rotationEuler: {x,y,z} degrees
- scale: {x,y,z}
- components: ComponentDefinition[] (or [])
- materialId: optional string (must exist in assets.materials)
- For kind-specific fields:
  - kind=Primitive: primitiveType one of [Cube, Sphere, Capsule, Plane, Cylinder, Quad]
  - kind=Builtin: builtinType one of [Camera, DirectionalLight, PointLight, SpotLight]
  - kind=Terrain: terrainDataId optional string referencing assets.terrainData[].id
  - kind=Empty: no extra fields

COMPONENTS
ComponentDefinition supports two modes (choose ONE):
1) script: MonoBehaviour class name (preferred).
2) builtinComponent: whitelisted built-in component name.

Allowed builtinComponent values ONLY:
- Rigidbody
- BoxCollider
- SphereCollider
- CapsuleCollider
- CharacterController

Rules:
- Use either script OR builtinComponent, not both.
- Never output a component with empty script/builtinComponent.
- Properties are optional. If none, use properties: [].

PROPERTIES
PropertyValue:
- name: field/property name
- type: one of [float,int,bool,string,color,vector3]
- Provide the matching value field:
  - floatValue / intValue / boolValue / stringValue / colorValue / vector3Value
- Do not include irrelevant value fields.

ASSETS
assets is optional but recommended when using materials/terrain.

MaterialDefinition:
- id: string
- shader: string (try Universal Render Pipeline/Lit or Standard)
- color: {r,g,b,a}
- metallic: float
- smoothness: float

TerrainDataDefinition:
- id: string
- size: {x,y,z} (typical: {200,50,200})
- heightmapResolution: int (typical: 513)
- preset: one of [Plains,Hills,Mountains,Valley,Canyon,Plateau,Island] (required by schema)

Rules:
- If any object uses materialId, that id MUST exist in assets.materials.
- If a Terrain object uses terrainDataId, that id SHOULD exist in assets.terrainData.

SCRIPTS
scripts is required (can be empty array).
ScriptDefinition:
- name: class name
- path: must start with Assets/ and end with .cs
- code: full file text
Rules:
- If you don't need scripts: scripts: []
- NEVER include a script object with empty name/path/code.
- Script code must compile and must NOT use UnityEditor.
- If code references SceneManager, it MUST include: using UnityEngine.SceneManagement;

FLOW (REQUIRED)
flow:
- restartKey: e.g. R
- nextSceneOnTrigger: true/false

OUTPUT RULES
- Output exactly ONE JSON object.
- No extra keys.
- Ensure scenes and objects arrays are not empty.
";

    // Your SchemaJson stays the same (unchanged). Paste your full SchemaJson here.
    public static readonly string SchemaJson =
@"
{
  ""type"": ""object"",
  ""additionalProperties"": false,
  ""required"": [""version"", ""settings"", ""assets"", ""scenes"", ""scripts"", ""flow""],
  ""properties"": {
    ""version"": { ""type"": ""string"", ""minLength"": 1 },
    ""settings"": { ""$ref"": ""#/definitions/GenerationSettings"" },
    ""assets"": { ""$ref"": ""#/definitions/AssetPlan"" },
    ""scenes"": { ""type"": ""array"", ""minItems"": 1, ""items"": { ""$ref"": ""#/definitions/SceneDefinition"" } },
    ""scripts"": { ""type"": ""array"", ""items"": { ""$ref"": ""#/definitions/ScriptDefinition"" } },
    ""flow"": { ""$ref"": ""#/definitions/FlowDefinition"" }
  },
  ""definitions"": {
    ""Vector3"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""x"", ""y"", ""z""],
      ""properties"": { ""x"": { ""type"": ""number"" }, ""y"": { ""type"": ""number"" }, ""z"": { ""type"": ""number"" } }
    },
    ""Color"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""r"", ""g"", ""b"", ""a""],
      ""properties"": { ""r"": { ""type"": ""number"" }, ""g"": { ""type"": ""number"" }, ""b"": { ""type"": ""number"" }, ""a"": { ""type"": ""number"" } }
    },
    ""GenerationSettings"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""ensureCameraAndLight"", ""worldGen""],
      ""properties"": {
        ""ensureCameraAndLight"": { ""type"": ""boolean"" },
        ""worldGen"": {
          ""type"": ""object"",
          ""additionalProperties"": false,
          ""required"": [""enabled"", ""terrainPreset""],
          ""properties"": {
            ""enabled"": { ""type"": ""boolean"" },
            ""terrainPreset"": { ""type"": ""string"", ""enum"": [""Plains"",""Hills"",""Mountains"",""Valley"",""Canyon"",""Plateau"",""Island""] },
            ""roadHubs"": { ""type"": ""array"", ""items"": { ""$ref"": ""#/definitions/Vector3"" } },
            ""settlements"": { ""type"": ""array"", ""items"": { ""$ref"": ""#/definitions/SettlementNode"" } }
          }
        }
      }
    },
    ""SettlementNode"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""center"", ""radius"", ""buildingCount"", ""style""],
      ""properties"": {
        ""center"": { ""$ref"": ""#/definitions/Vector3"" },
        ""radius"": { ""type"": ""number"" },
        ""buildingCount"": { ""type"": ""integer"" },
        ""style"": { ""type"": ""string"", ""enum"": [""Auto"", ""ModernCityBlocks"", ""MedievalTown"", ""Suburban""] }
      }
    },
    ""AssetPlan"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""materials"", ""terrainData""],
      ""properties"": {
        ""materials"": { ""type"": ""array"", ""items"": { ""$ref"": ""#/definitions/MaterialDefinition"" } },
        ""terrainData"": { ""type"": ""array"", ""items"": { ""$ref"": ""#/definitions/TerrainDataDefinition"" } }
      }
    },
    ""MaterialDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""id"", ""shader"", ""color"", ""metallic"", ""smoothness""],
      ""properties"": {
        ""id"": { ""type"": ""string"", ""minLength"": 1 },
        ""shader"": { ""type"": ""string"", ""minLength"": 1 },
        ""color"": { ""$ref"": ""#/definitions/Color"" },
        ""metallic"": { ""type"": ""number"" },
        ""smoothness"": { ""type"": ""number"" }
      }
    },
    ""TerrainDataDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""id"", ""size"", ""heightmapResolution"", ""preset""],
      ""properties"": {
        ""id"": { ""type"": ""string"", ""minLength"": 1 },
        ""size"": { ""$ref"": ""#/definitions/Vector3"" },
        ""heightmapResolution"": { ""type"": ""integer"" },
        ""preset"": { ""type"": ""string"", ""enum"": [""Plains"",""Hills"",""Mountains"",""Valley"",""Canyon"",""Plateau"",""Island""] }
      }
    },
    ""SceneSettings"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""properties"": { ""ensureCameraAndLight"": { ""type"": [""boolean"", ""null""] } }
    },
    ""SceneDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""name"", ""objects""],
      ""properties"": {
        ""name"": { ""type"": ""string"", ""minLength"": 1 },
        ""settings"": { ""$ref"": ""#/definitions/SceneSettings"" },
        ""objects"": { ""type"": ""array"", ""minItems"": 1, ""items"": { ""$ref"": ""#/definitions/ObjectDefinition"" } }
      }
    },
    ""ObjectDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""id"", ""kind"", ""position"", ""rotationEuler"", ""scale"", ""components""],
      ""properties"": {
        ""id"": { ""type"": ""string"", ""minLength"": 1 },
        ""kind"": { ""type"": ""string"", ""enum"": [""Primitive"", ""Empty"", ""Builtin"", ""Terrain""] },
        ""primitiveType"": { ""type"": ""string"", ""enum"": [""Cube"", ""Sphere"", ""Capsule"", ""Plane"", ""Cylinder"", ""Quad""] },
        ""builtinType"": { ""type"": ""string"", ""enum"": [""Camera"", ""DirectionalLight"", ""PointLight"", ""SpotLight""] },
        ""terrainDataId"": { ""type"": ""string"" },
        ""parentId"": { ""type"": ""string"" },
        ""position"": { ""$ref"": ""#/definitions/Vector3"" },
        ""rotationEuler"": { ""$ref"": ""#/definitions/Vector3"" },
        ""scale"": { ""$ref"": ""#/definitions/Vector3"" },
        ""components"": { ""type"": ""array"", ""items"": { ""$ref"": ""#/definitions/ComponentDefinition"" } },
        ""materialId"": { ""type"": ""string"" }
      }
    },
    ""ComponentDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""properties""],
      ""properties"": {
        ""script"": { ""type"": ""string"", ""minLength"": 1 },
        ""builtinComponent"": { ""type"": ""string"", ""enum"": [""Rigidbody"",""BoxCollider"",""SphereCollider"",""CapsuleCollider"",""CharacterController""] },
        ""properties"": { ""type"": ""array"", ""items"": { ""$ref"": ""#/definitions/PropertyValue"" } }
      },
      ""oneOf"": [
        { ""required"": [""script""], ""not"": { ""required"": [""builtinComponent""] } },
        { ""required"": [""builtinComponent""], ""not"": { ""required"": [""script""] } }
      ]
    },
    ""PropertyValue"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""name"", ""type""],
      ""properties"": {
        ""name"": { ""type"": ""string"", ""minLength"": 1 },
        ""type"": { ""type"": ""string"", ""enum"": [""float"",""int"",""bool"",""string"",""color"",""vector3""] },
        ""floatValue"": { ""type"": ""number"" },
        ""intValue"": { ""type"": ""integer"" },
        ""boolValue"": { ""type"": ""boolean"" },
        ""stringValue"": { ""type"": ""string"" },
        ""colorValue"": { ""$ref"": ""#/definitions/Color"" },
        ""vector3Value"": { ""$ref"": ""#/definitions/Vector3"" }
      }
    },
    ""ScriptDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""name"", ""path"", ""code""],
      ""properties"": {
        ""name"": { ""type"": ""string"", ""minLength"": 1 },
        ""path"": { ""type"": ""string"", ""minLength"": 1 },
        ""code"": { ""type"": ""string"", ""minLength"": 1 }
      }
    },
    ""FlowDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""restartKey"", ""nextSceneOnTrigger""],
      ""properties"": {
        ""restartKey"": { ""type"": ""string"", ""minLength"": 1 },
        ""nextSceneOnTrigger"": { ""type"": ""boolean"" }
      }
    }
  }
}
";
}
