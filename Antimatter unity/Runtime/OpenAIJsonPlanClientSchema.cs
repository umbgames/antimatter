// Assets/Editor/OpenAIJsonPlanClientSchema.cs

namespace Antimatter
{
    public static class OpenAIJsonPlanClientSchema
    {
        public static readonly string SystemPrompt = "";
    
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
}
