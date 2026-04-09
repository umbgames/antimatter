// Assets/Editor/EditorToolDispatcher.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    
    /// <summary>
    /// Registers and dispatches all editor tools available to the agent.
    /// Each tool is a thin wrapper over existing executor/scene logic.
    /// </summary>
    public static class EditorToolDispatcher
    {
        private static Dictionary<string, AgentTool> _tools;
    
        public static string GetFriendlyToolName(string toolName)
        {
            if (string.IsNullOrEmpty(toolName)) return "Unknown Action";
            
            return toolName.ToLower() switch
            {
                "inspectscene" => "Analyzing scene structure...",
                "listassets" => "Listing project assets...",
                "createobject" => "Creating GameObject...",
                "deleteobject" => "Deleting GameObject...",
                "modifytransform" => "Updating transform...",
                "addcomponent" => "Adding component...",
                "writescript" => "Writing C# script...",
                "deletescript" => "Deleting script...",
                "creatematerial" => "Creating material...",
                "readconsoleerrors" => "Checking console for errors...",
                "savescene" => "Saving scene...",
                "createscene" => "Creating new scene...",
                "runfullplan" => "Executing implementation plan...",
                "attachscript" => "Attaching script to object...",
                "generateterrain" => "Generating terrain...",
                "setobjectactive" => "Toggling object visibility...",
                "generateroadnetwork" => "Generating road system...",
                "generatesettlements" => "Generating city settlements...",
                "generatecity" => "Generating full city layout...",
                "generateworld" => "Generating complete world (terrain + roads + settlements)...",
                "arrangecityscene" => "Arranging city onto terrain...",
                "focussceneview" => "Focusing Scene View camera...",
                "searchAssets" => "Searching local project assets...",
                "generateCanvasUI" => "Generating Canvas UI...",
                "enterPlayMode" => "Entering Play Mode...",
                "exitPlayMode" => "Exiting Play Mode...",
                _ => toolName
            };
        }
    
        public static List<AgentTool> GetAllTools()
        {
            if (_tools == null)
                RegisterTools();
            return new List<AgentTool>(_tools.Values);
        }
    
        public static AgentToolResult Dispatch(string toolName, JObject args)
        {
            if (_tools == null)
                RegisterTools();
    
            if (!_tools.TryGetValue(toolName, out var tool))
                return AgentToolResult.Fail($"Unknown tool: {toolName}");
    
            try
            {
                return tool.Execute(args);
            }
            catch (Exception ex)
            {
                return AgentToolResult.Fail($"{toolName} threw: {ex.Message}");
            }
        }
    
        private static void RegisterTools()
        {
            _tools = new Dictionary<string, AgentTool>(StringComparer.OrdinalIgnoreCase);

            // Register dynamic marketplace tools via Reflection & TypeCache
            var marketplaceTypes = TypeCache.GetTypesDerivedFrom<IMarketplaceTool>();
            foreach (var t in marketplaceTypes)
            {
                if (t.IsAbstract || t.IsInterface) continue;
                try
                {
                    var instance = (IMarketplaceTool)Activator.CreateInstance(t);
                    Register(new AgentTool
                    {
                        Name = instance.Name,
                        Description = instance.Description,
                        ParameterSchema = instance.ParameterSchema,
                        Execute = instance.Execute
                    });
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Marketplace] Failed to register tool '{t.Name}': {e.Message}");
                }
            }

            Register(new AgentTool
            {
                Name = "inspectScene",
                Description = "Returns a structured summary of all objects in the active Unity scene including their ids, kinds, positions, scales, components, and materials.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = ToolInspectScene
            });
    
            Register(new AgentTool
            {
                Name = "listAssets",
                Description = "Lists project assets: scenes, generated scripts, generated materials.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = ToolListAssets
            });
    
            Register(new AgentTool
            {
                Name = "searchAssets",
                Description = "Searches the local project Assets folder for materials, textures, models, or prefabs.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""keyword"":{""type"":""string""},
                        ""type"":{""type"":""string"",""enum"":[""model"",""texture"",""prefab"",""material"",""audioclip""]}
                    },
                    ""required"":[""keyword""]
                }",
                Execute = AssetFetchTool.ToolSearchAssets
            });

            Register(new AgentTool
            {
                Name = "generateCanvasUI",
                Description = "Generates a complete uGUI Canvas with panels, buttons, and text elements from a JSON layout.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""canvasName"":{""type"":""string""},
                        ""elements"":{
                            ""type"":""array"",
                            ""items"":{
                                ""type"":""object"",
                                ""properties"":{
                                    ""name"":{""type"":""string""},
                                    ""type"":{""type"":""string"",""enum"":[""panel"",""text"",""button""]},
                                    ""text"":{""type"":""string""},
                                    ""rect"":{
                                        ""type"":""object"",
                                        ""properties"":{
                                            ""x"":{""type"":""number""},
                                            ""y"":{""type"":""number""},
                                            ""width"":{""type"":""number""},
                                            ""height"":{""type"":""number""}
                                        }
                                    },
                                    ""children"":{""type"":""array""}
                                }
                            }
                        }
                    },
                    ""required"":[""elements""]
                }",
                Execute = UIBuilderTool.ToolGenerateCanvasUI
            });

            Register(new AgentTool
            {
                Name = "enterPlayMode",
                Description = "Enters Unity Play Mode to start simulating the game. Call exitPlayMode when done to fetch runtime logs.",
                ParameterSchema = @"{""type"":""object"",""properties"":{}}",
                Execute = PlayModeTester.ToolEnterPlayMode
            });

            Register(new AgentTool
            {
                Name = "exitPlayMode",
                Description = "Exits Unity Play Mode and returns a summary of any errors or warnings that occurred.",
                ParameterSchema = @"{""type"":""object"",""properties"":{}}",
                Execute = PlayModeTester.ToolExitPlayMode
            });

            Register(new AgentTool
            {
                Name = "createObject",
                Description = "Creates a single GameObject in the active scene. Supports Primitive (Cube/Sphere/Capsule/Plane/Cylinder), Empty, and Builtin types (Camera, DirectionalLight, PointLight, SpotLight).",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id"",""kind""],
                    ""properties"":{
                        ""id"":{""type"":""string"",""description"":""Unique name for the object""},
                        ""kind"":{""type"":""string"",""enum"":[""Primitive"",""Empty"",""Builtin""],""description"":""Object kind""},
                        ""primitiveType"":{""type"":""string"",""enum"":[""Cube"",""Sphere"",""Capsule"",""Plane"",""Cylinder"",""Quad""],""description"":""Required if kind=Primitive""},
                        ""builtinType"":{""type"":""string"",""enum"":[""Camera"",""DirectionalLight"",""PointLight"",""SpotLight""],""description"":""Required if kind=Builtin""},
                        ""posX"":{""type"":""number"",""description"":""World X position""},
                        ""posY"":{""type"":""number"",""description"":""World Y position""},
                        ""posZ"":{""type"":""number"",""description"":""World Z position""},
                        ""scaleX"":{""type"":""number"",""description"":""Local X scale""},
                        ""scaleY"":{""type"":""number"",""description"":""Local Y scale""},
                        ""scaleZ"":{""type"":""number"",""description"":""Local Z scale""},
                        ""rotX"":{""type"":""number""},
                        ""rotY"":{""type"":""number""},
                        ""rotZ"":{""type"":""number""},
                        ""parentId"":{""type"":""string"",""description"":""Optional parent object name""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolCreateObject
            });
    
            Register(new AgentTool
            {
                Name = "deleteObject",
                Description = "Deletes a GameObject from the active scene by its name/id.",
                ParameterSchema = @"{""type"":""object"",""required"":[""id""],""properties"":{""id"":{""type"":""string""}},""additionalProperties"":false}",
                Execute = ToolDeleteObject
            });
    
            Register(new AgentTool
            {
                Name = "modifyTransform",
                Description = "Modifies position, rotation, and/or scale of an existing object. Only specified values are changed.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id""],
                    ""properties"":{
                        ""id"":{""type"":""string""},
                        ""posX"":{""type"":""number""},""posY"":{""type"":""number""},""posZ"":{""type"":""number""},
                        ""scaleX"":{""type"":""number""},""scaleY"":{""type"":""number""},""scaleZ"":{""type"":""number""},
                        ""rotX"":{""type"":""number""},""rotY"":{""type"":""number""},""rotZ"":{""type"":""number""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolModifyTransform
            });
    
            Register(new AgentTool
            {
                Name = "addComponent",
                Description = "Adds a builtin component (Rigidbody, BoxCollider, SphereCollider, CapsuleCollider, CharacterController) to an object.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id"",""componentType""],
                    ""properties"":{
                        ""id"":{""type"":""string""},
                        ""componentType"":{""type"":""string"",""enum"":[""Rigidbody"",""BoxCollider"",""SphereCollider"",""CapsuleCollider"",""CharacterController""]}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolAddComponent
            });
    
            Register(new AgentTool
            {
                Name = "writeScript",
                Description = "Writes a C# MonoBehaviour script file. This script will safely be preserved to prevent interruptions, so you can safely call attach_script on it. Path must start with Assets/ and end with .cs.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""path"",""code""],
                    ""properties"":{
                        ""path"":{""type"":""string"",""description"":""e.g. Assets/Generated/PlayerController.cs""},
                        ""code"":{""type"":""string"",""description"":""Full C# file content""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolWriteScript
            });
    
            Register(new AgentTool
            {
                Name = "deleteScript",
                Description = "Deletes a generated script file from the project.",
                ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}},""additionalProperties"":false}",
                Execute = ToolDeleteScript
            });
    
            Register(new AgentTool
            {
                Name = "createMaterial",
                Description = "Creates a material asset with given properties.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""name""],
                    ""properties"":{
                        ""name"":{""type"":""string""},
                        ""shader"":{""type"":""string"",""description"":""Shader name e.g. Standard or Universal Render Pipeline/Lit""},
                        ""colorR"":{""type"":""number""},""colorG"":{""type"":""number""},""colorB"":{""type"":""number""},""colorA"":{""type"":""number""},
                        ""metallic"":{""type"":""number""},
                        ""smoothness"":{""type"":""number""},
                        ""assignTo"":{""type"":""string"",""description"":""Optional: object id to assign this material to""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolCreateMaterial
            });
    
            Register(new AgentTool
            {
                Name = "readConsoleErrors",
                Description = "Returns recent Unity console errors and warnings.",
                ParameterSchema = @"{""type"":""object"",""properties"":{""maxCount"":{""type"":""integer""}},""additionalProperties"":false}",
                Execute = ToolReadConsoleErrors
            });
    
            Register(new AgentTool
            {
                Name = "saveScene",
                Description = "Saves the currently active scene.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = ToolSaveScene
            });
    
            Register(new AgentTool
            {
                Name = "createScene",
                Description = "Creates a new empty scene and makes it active.",
                ParameterSchema = @"{""type"":""object"",""required"":[""name""],""properties"":{""name"":{""type"":""string""}},""additionalProperties"":false}",
                Execute = ToolCreateScene
            });
    
            Register(new AgentTool
            {
                Name = "runFullPlan",
                Description = "Applies an entire legacy JSON game plan using the full GameGenerationExecutor pipeline. Use this when you need to create a complete game from a single JSON plan.",
                ParameterSchema = @"{""type"":""object"",""required"":[""planJson""],""properties"":{""planJson"":{""type"":""string"",""description"":""The full JSON plan string""}},""additionalProperties"":false}",
                Execute = ToolRunFullPlan
            });
    
            Register(new AgentTool
            {
                Name = "attachScript",
                Description = "Attaches an existing MonoBehaviour script to a GameObject by name.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""objectId"",""scriptName""],
                    ""properties"":{
                        ""objectId"":{""type"":""string""},
                        ""scriptName"":{""type"":""string"",""description"":""Class name of the MonoBehaviour""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolAttachScript
            });
    
            Register(new AgentTool
            {
                Name = "generateTerrain",
                Description = "Generates terrain in the active scene using a preset.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""preset"":{""type"":""string"",""enum"":[""Plains"",""Hills"",""Mountains"",""MountainValley"",""Island""],""description"":""Terrain preset""},
                        ""seed"":{""type"":""integer"",""description"":""Random seed for terrain generation""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolGenerateTerrain
            });
    
            Register(new AgentTool
            {
                Name = "setObjectActive",
                Description = "Enables or disables a GameObject.",
                ParameterSchema = @"{""type"":""object"",""required"":[""id"",""active""],""properties"":{""id"":{""type"":""string""},""active"":{""type"":""boolean""}},""additionalProperties"":false}",
                Execute = ToolSetObjectActive
            });
            Register(new AgentTool
            {
                Name = "generateRoadNetwork",
                Description = "Generates a road network connecting the given explicit hubs (Vector3 points) on the terrain. Will create a temp flat terrain if none exists.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""hubs""],
                    ""properties"":{
                        ""hubs"":{
                            ""type"":""array"",
                            ""items"":{
                                ""type"":""object"",
                                ""properties"":{""x"":{""type"":""number""},""y"":{""type"":""number""},""z"":{""type"":""number""}},
                                ""required"":[""x"",""y"",""z""]
                            }
                        },
                        ""prompt"":{""type"":""string"",""description"":""Optional descriptions like 'bridges' or 'tunnels' or 'dirt road'""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolGenerateRoadNetwork
            });
    
            Register(new AgentTool
            {
                Name = "generateSettlements",
                Description = "Generates towns/villages at the given explicit centers.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""settlements""],
                    ""properties"":{
                        ""settlements"":{
                            ""type"":""array"",
                            ""items"":{
                                ""type"":""object"",
                                ""properties"":{
                                    ""x"":{""type"":""number""},
                                    ""y"":{""type"":""number""},
                                    ""z"":{""type"":""number""},
                                    ""radius"":{""type"":""number""},
                                    ""buildingCount"":{""type"":""integer""},
                                    ""style"":{""type"":""string"",""enum"":[""Auto"",""ModernCityBlocks"",""MedievalTown"",""Suburban""]}
                                },
                                ""required"":[""x"",""y"",""z"",""radius"",""buildingCount"",""style""]
                            }
                        },
                        ""prompt"":{""type"":""string"",""description"":""Optional style prompt or description""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolGenerateSettlements
            });

            Register(new AgentTool
            {
                Name = "generateCity",
                Description = "Generates a complete procedural city with roads, sidewalks, and buildings on terrain. Creates terrain automatically if none exists. This is the easiest way to build a city — just specify building count and style.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""buildingCount"":{""type"":""integer"",""description"":""Number of buildings (8-60)""},
                        ""terrainPreset"":{""type"":""string"",""enum"":[""Plains"",""Hills"",""Mountains"",""MountainValley"",""Island""],""description"":""Terrain type to generate, defaults to Hills""},
                        ""prompt"":{""type"":""string"",""description"":""Optional style description e.g. 'modern downtown' or 'medieval village'""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolGenerateCity
            });

            Register(new AgentTool
            {
                Name = "arrangeCityScene",
                Description = "Arranges existing buildings/objects in the scene into a proper city layout on terrain. Automatically builds roads, sidewalks, stamps terrain flat under buildings, and snaps everything to the terrain surface. Requires terrain and building objects to already exist in the scene.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""prompt"":{""type"":""string"",""description"":""Optional style description""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolArrangeCityScene
            });

            Register(new AgentTool
            {
                Name = "focusSceneView",
                Description = "Focuses the Unity Scene View camera on a specific object, or on the entire scene if no object is specified.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""objectName"":{""type"":""string"",""description"":""Name of the object to focus on. Leave empty to focus on all.""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolFocusSceneView
            });
        }
    
        private static void Register(AgentTool tool)
        {
            _tools[tool.Name] = tool;
        }
    
        // ──────────────────── TOOL IMPLEMENTATIONS ────────────────────
    
        private static AgentToolResult ToolInspectScene(JObject args)
        {
            var snap = SceneObserver.GetSnapshot();
            return AgentToolResult.Ok(SceneObserver.ToAgentContext(snap));
        }
    
        private static AgentToolResult ToolListAssets(JObject args)
        {
            return AgentToolResult.Ok(ProjectObserver.GetProjectSummary());
        }
    
        private static AgentToolResult ToolCreateObject(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return AgentToolResult.Fail("No active scene.");
    
            string id = args["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id))
                return AgentToolResult.Fail("id is required.");
    
            string kind = args["kind"]?.Value<string>() ?? "Primitive";
    
            GameObject go;
            switch (kind)
            {
                case "Empty":
                    go = new GameObject(id);
                    break;
                case "Builtin":
                    string builtinType = args["builtinType"]?.Value<string>() ?? "Camera";
                    go = CreateBuiltinObject(id, builtinType);
                    break;
                case "Primitive":
                default:
                    string primType = args["primitiveType"]?.Value<string>() ?? "Cube";
                    go = CreatePrimitiveObject(primType);
                    break;
            }
    
            go.name = id;
    
            // Position
            float px = args["posX"]?.Value<float>() ?? 0f;
            float py = args["posY"]?.Value<float>() ?? 0f;
            float pz = args["posZ"]?.Value<float>() ?? 0f;
            go.transform.position = new Vector3(px, py, pz);
    
            // Scale
            float sx = args["scaleX"]?.Value<float>() ?? 1f;
            float sy = args["scaleY"]?.Value<float>() ?? 1f;
            float sz = args["scaleZ"]?.Value<float>() ?? 1f;
            go.transform.localScale = new Vector3(sx, sy, sz);
    
            // Rotation
            float rx = args["rotX"]?.Value<float>() ?? 0f;
            float ry = args["rotY"]?.Value<float>() ?? 0f;
            float rz = args["rotZ"]?.Value<float>() ?? 0f;
            if (rx != 0 || ry != 0 || rz != 0)
                go.transform.rotation = Quaternion.Euler(rx, ry, rz);
    
            Undo.RegisterCreatedObjectUndo(go, "Agent Create Object");
            SceneManager.MoveGameObjectToScene(go, scene);
    
            // Parent
            string parentId = args["parentId"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(parentId))
            {
                var parent = FindInScene(scene, parentId);
                if (parent != null)
                    go.transform.SetParent(parent.transform, true);
            }
    
            return AgentToolResult.Ok($"Created {kind} '{id}' at ({px},{py},{pz})");
        }
    
        private static AgentToolResult ToolDeleteObject(JObject args)
        {
            string id = args["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id))
                return AgentToolResult.Fail("id is required.");
    
            var scene = SceneManager.GetActiveScene();
            var go = FindInScene(scene, id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");
    
            Undo.DestroyObjectImmediate(go);
            return AgentToolResult.Ok($"Deleted '{id}'.");
        }
    
        private static AgentToolResult ToolModifyTransform(JObject args)
        {
            string id = args["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id))
                return AgentToolResult.Fail("id is required.");
    
            var go = FindInScene(SceneManager.GetActiveScene(), id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");
    
            Undo.RecordObject(go.transform, "Agent Modify Transform");
    
            var pos = go.transform.position;
            if (args["posX"] != null) pos.x = args["posX"].Value<float>();
            if (args["posY"] != null) pos.y = args["posY"].Value<float>();
            if (args["posZ"] != null) pos.z = args["posZ"].Value<float>();
            go.transform.position = pos;
    
            var scale = go.transform.localScale;
            if (args["scaleX"] != null) scale.x = args["scaleX"].Value<float>();
            if (args["scaleY"] != null) scale.y = args["scaleY"].Value<float>();
            if (args["scaleZ"] != null) scale.z = args["scaleZ"].Value<float>();
            go.transform.localScale = scale;
    
            var rot = go.transform.eulerAngles;
            if (args["rotX"] != null) rot.x = args["rotX"].Value<float>();
            if (args["rotY"] != null) rot.y = args["rotY"].Value<float>();
            if (args["rotZ"] != null) rot.z = args["rotZ"].Value<float>();
            go.transform.rotation = Quaternion.Euler(rot);
    
            return AgentToolResult.Ok($"Modified transform of '{id}'.");
        }
    
        private static AgentToolResult ToolAddComponent(JObject args)
        {
            string id = args["id"]?.Value<string>();
            string compType = args["componentType"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(compType))
                return AgentToolResult.Fail("id and componentType are required.");
    
            var go = FindInScene(SceneManager.GetActiveScene(), id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");
    
            var comp = BuiltinComponentFactory.Add(go, compType);
            if (comp == null)
                return AgentToolResult.Fail($"Unknown component type: {compType}");
    
            return AgentToolResult.Ok($"Added {compType} to '{id}'.");
        }
    
        private static AgentToolResult ToolWriteScript(JObject args)
        {
            string path = args["path"]?.Value<string>();
            string code = args["code"]?.Value<string>();
    
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/") || !path.EndsWith(".cs"))
                return AgentToolResult.Fail("Invalid path. Must start with Assets/ and end with .cs");
            if (string.IsNullOrWhiteSpace(code))
                return AgentToolResult.Fail("Code is empty.");
    
            // Ensure directory exists
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
    
            // TEXT-FILE SWAP TRICK: Save as .txt so it doesn't trigger domain reload yet
            string txtPath = path + ".txt";
            File.WriteAllText(txtPath, code, System.Text.Encoding.UTF8);
    
            AgentOrchestrator.ActiveOrchestrator?.TrackScript(txtPath);
    
            return AgentToolResult.Ok($"Wrote script successfully. (Saved securely to prevent mid-loop compiler reload). You may now call attach_script if you need to use it.");
        }
    
        private static AgentToolResult ToolDeleteScript(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/"))
                return AgentToolResult.Fail("Invalid path.");
    
            if (!File.Exists(path))
                return AgentToolResult.Fail($"File not found: {path}");
    
            AssetDatabase.DeleteAsset(path);
            return AgentToolResult.Ok($"Deleted: {path}");
        }
    
        private static AgentToolResult ToolCreateMaterial(JObject args)
        {
            string name = args["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(name))
                return AgentToolResult.Fail("name is required.");
    
            string shaderName = args["shader"]?.Value<string>() ?? "Standard";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                if (shader == null)
                    return AgentToolResult.Fail("No shader could be found.");
            }
    
            var mat = new Material(shader);
            mat.name = name;
    
            float r = args["colorR"]?.Value<float>() ?? 1f;
            float g = args["colorG"]?.Value<float>() ?? 1f;
            float b = args["colorB"]?.Value<float>() ?? 1f;
            float a = args["colorA"]?.Value<float>() ?? 1f;
            mat.color = new Color(r, g, b, a);
    
            if (args["metallic"] != null)
                mat.SetFloat("_Metallic", args["metallic"].Value<float>());
            if (args["smoothness"] != null)
                mat.SetFloat("_Glossiness", args["smoothness"].Value<float>());
    
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
    
            string matPath = $"Assets/Generated/{name}.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();
    
            // Optionally assign to object
            string assignTo = args["assignTo"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(assignTo))
            {
                var go = FindInScene(SceneManager.GetActiveScene(), assignTo);
                if (go != null)
                {
                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Undo.RecordObject(renderer, "Agent Assign Material");
                        renderer.sharedMaterial = mat;
                    }
                }
            }
    
            return AgentToolResult.Ok($"Created material '{name}' at {matPath}");
        }
    
        private static AgentToolResult ToolReadConsoleErrors(JObject args)
        {
            int maxCount = args["maxCount"]?.Value<int>() ?? 30;
            var errors = ProjectObserver.GetConsoleErrors(maxCount);
    
            if (errors.Count == 0)
                return AgentToolResult.Ok("No console errors found.");
    
            return AgentToolResult.Ok($"{errors.Count} errors:\n" + string.Join("\n", errors));
        }
    
        private static AgentToolResult ToolSaveScene(JObject args)
        {
            EditorSceneManager.SaveOpenScenes();
            return AgentToolResult.Ok("Scene saved.");
        }
    
        private static AgentToolResult ToolCreateScene(JObject args)
        {
            string name = args["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(name))
                return AgentToolResult.Fail("name is required.");
    
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            scene.name = name;
            SceneManager.SetActiveScene(scene);
    
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
    
            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{name}.unity");
            return AgentToolResult.Ok($"Created and activated scene '{name}'.");
        }
    
        private static AgentToolResult ToolRunFullPlan(JObject args)
        {
            string planJson = args["planJson"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(planJson))
                return AgentToolResult.Fail("planJson is required.");
    
            try
            {
                GameGenerationExecutor.ExecuteCreate(planJson);
                return AgentToolResult.Ok("Full plan applied successfully.");
            }
            catch (Exception ex)
            {
                return AgentToolResult.Fail($"Plan execution failed: {ex.Message}");
            }
        }
    
        private static AgentToolResult ToolAttachScript(JObject args)
        {
            string objectId = args["objectId"]?.Value<string>();
            string scriptName = args["scriptName"]?.Value<string>();
    
            if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(scriptName))
                return AgentToolResult.Fail("objectId and scriptName are required.");
    
            var go = FindInScene(SceneManager.GetActiveScene(), objectId);
            if (go == null)
                return AgentToolResult.Fail($"Object '{objectId}' not found.");
    
            MonoScriptResolver.RebuildIndex();
            var ms = MonoScriptResolver.Find(scriptName);
            if (ms == null)
            {
                // TEXT SWAP TRICK logic:
                AgentOrchestrator.ActiveOrchestrator?.QueueAttachment(objectId, scriptName);
                return AgentToolResult.Ok($"Script '{scriptName}' is queued. It will be bound to '{objectId}' automatically after generation completes.");
            }
    
            var type = ms.GetClass();
            if (type == null)
                return AgentToolResult.Fail($"Script '{scriptName}' has no valid class.");
    
            Undo.AddComponent(go, type);
            return AgentToolResult.Ok($"Attached '{scriptName}' to '{objectId}'.");
        }
    
        private static AgentToolResult ToolGenerateTerrain(JObject args)
        {
            string presetStr = args["preset"]?.Value<string>() ?? "Hills";
            int seed = args["seed"]?.Value<int>() ?? UnityEngine.Random.Range(0, int.MaxValue);
    
            TerrainGenerator.TerrainPreset preset = presetStr switch
            {
                "Plains" => TerrainGenerator.TerrainPreset.Plains,
                "Hills" => TerrainGenerator.TerrainPreset.Hills,
                "Mountains" => TerrainGenerator.TerrainPreset.Mountains,
                "MountainValley" => TerrainGenerator.TerrainPreset.MountainValley,
                "Island" => TerrainGenerator.TerrainPreset.Island,
                _ => TerrainGenerator.TerrainPreset.Hills
            };
    
            var scene = SceneManager.GetActiveScene();
            var go = TerrainGenerator.CreateOrReplaceTerrain("GeneratedTerrain", preset, seed, Vector3.zero);
            SceneManager.MoveGameObjectToScene(go, scene);

            // Auto-select & frame in Scene View so the user can see it
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
    
            return AgentToolResult.Ok($"Generated terrain with preset={presetStr} seed={seed}. Terrain is now selected and framed in Scene View.");
        }
    
        private static AgentToolResult ToolSetObjectActive(JObject args)
        {
            string id = args["id"]?.Value<string>();
            bool active = args["active"]?.Value<bool>() ?? true;
    
            if (string.IsNullOrWhiteSpace(id))
                return AgentToolResult.Fail("id is required.");
    
            var go = FindInScene(SceneManager.GetActiveScene(), id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");
    
            Undo.RecordObject(go, "Agent Set Active");
            go.SetActive(active);
            return AgentToolResult.Ok($"Set '{id}' active={active}.");
        }
    
        private static AgentToolResult ToolGenerateRoadNetwork(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();

            // Auto-create terrain if none exists
            if (terrain == null)
            {
                var terrGo = TerrainGenerator.CreateOrReplaceTerrain("GeneratedTerrain", TerrainGenerator.TerrainPreset.Plains, UnityEngine.Random.Range(0, int.MaxValue), Vector3.zero);
                SceneManager.MoveGameObjectToScene(terrGo, scene);
                terrain = terrGo.GetComponent<Terrain>();
                if (terrain == null) return AgentToolResult.Fail("Failed to auto-create terrain for roads.");
            }
    
            var hubsToken = args["hubs"] as JArray;
            if (hubsToken == null || hubsToken.Count < 2) return AgentToolResult.Fail("Need at least 2 hubs to build roads.");
    
            List<Vector3> explicitHubs = new List<Vector3>();
            foreach(var h in hubsToken)
            {
                explicitHubs.Add(new Vector3(h["x"].Value<float>(), h["y"].Value<float>(), h["z"].Value<float>()));
            }
    
            string prompt = args["prompt"]?.Value<string>() ?? "";
            
            var pSettings = new RoadGenSettings();
            try
            {
                RoadNetworkPlanner.GenerateRoadSystem(scene, terrain, pSettings, explicitHubs, null, false, null);
                return AgentToolResult.Ok($"Generated road network with {explicitHubs.Count} hubs.");
            }
            catch(Exception e)
            {
                 return AgentToolResult.Fail($"Road generation failed: {e.Message}");
            }
        }
    
        private static AgentToolResult ToolGenerateSettlements(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (terrain == null) return AgentToolResult.Fail("No Terrain in scene for settlements.");
    
            var setToken = args["settlements"] as JArray;
            if (setToken == null || setToken.Count == 0) return AgentToolResult.Fail("Need at least 1 settlement.");
    
            List<SettlementNode> explicitSets = new List<SettlementNode>();
            foreach(var s in setToken)
            {
                explicitSets.Add(new SettlementNode {
                    center = new Vector3(s["x"].Value<float>(), s["y"].Value<float>(), s["z"].Value<float>()),
                    radius = s["radius"].Value<float>(),
                    buildingCount = s["buildingCount"].Value<int>(),
                    style = s["style"]?.Value<string>() ?? "Auto"
                });
            }
    
            string prompt = args["prompt"]?.Value<string>() ?? "";
            var setSettings = new SettlementGenSettings();
    
            try
            {
                var noRoads = new RoadNetworkPlanner.RoadNetworkResult();
                SettlementNetworkPlanner.GenerateSettlements(scene, terrain, noRoads, setSettings, explicitSets, prompt, null, null);
                return AgentToolResult.Ok($"Generated {explicitSets.Count} settlements.");
            }
            catch(Exception e)
            {
                  return AgentToolResult.Fail($"Settlement generation failed: {e.Message}");
            }
        }
    
        // ──────────────────── NEW TOOL IMPLEMENTATIONS ────────────────────

        private static AgentToolResult ToolGenerateCity(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return AgentToolResult.Fail("No active scene.");

            int buildingCount = args["buildingCount"]?.Value<int>() ?? 16;
            buildingCount = Mathf.Clamp(buildingCount, 4, 80);
            string terrainPresetStr = args["terrainPreset"]?.Value<string>() ?? "Hills";
            string prompt = args["prompt"]?.Value<string>() ?? "";

            // 1) Ensure terrain exists
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (terrain == null)
            {
                TerrainGenerator.TerrainPreset preset = terrainPresetStr switch
                {
                    "Plains" => TerrainGenerator.TerrainPreset.Plains,
                    "Mountains" => TerrainGenerator.TerrainPreset.Mountains,
                    "MountainValley" => TerrainGenerator.TerrainPreset.MountainValley,
                    "Island" => TerrainGenerator.TerrainPreset.Island,
                    _ => TerrainGenerator.TerrainPreset.Hills
                };
                var terrGo = TerrainGenerator.CreateOrReplaceTerrain("GeneratedTerrain", preset, UnityEngine.Random.Range(0, int.MaxValue), Vector3.zero);
                SceneManager.MoveGameObjectToScene(terrGo, scene);
                terrain = terrGo.GetComponent<Terrain>();
            }

            // 2) Create building objects
            bool isMedieval = prompt.ToLowerInvariant().Contains("medieval") || prompt.ToLowerInvariant().Contains("village");
            bool isModern = prompt.ToLowerInvariant().Contains("modern") || prompt.ToLowerInvariant().Contains("downtown") || prompt.ToLowerInvariant().Contains("city");

            for (int i = 0; i < buildingCount; i++)
            {
                string bName;
                float w, h, d;

                // Deterministic variety
                float hash = Hash01ForCity(i, 42);
                float hash2 = Hash01ForCity(i, 77);
                float hash3 = Hash01ForCity(i, 113);

                if (hash < 0.1f)
                {
                    bName = $"Tower_{i}";
                    w = Mathf.Lerp(6f, 12f, hash2);
                    h = Mathf.Lerp(25f, 55f, hash3);
                    d = Mathf.Lerp(6f, 12f, Hash01ForCity(i, 201));
                }
                else if (hash < 0.3f)
                {
                    bName = $"Office_{i}";
                    w = Mathf.Lerp(8f, 16f, hash2);
                    h = Mathf.Lerp(14f, 30f, hash3);
                    d = Mathf.Lerp(8f, 16f, Hash01ForCity(i, 201));
                }
                else if (hash < 0.55f)
                {
                    bName = isMedieval ? $"House_{i}" : $"Building_{i}";
                    w = Mathf.Lerp(6f, 14f, hash2);
                    h = Mathf.Lerp(8f, 18f, hash3);
                    d = Mathf.Lerp(6f, 14f, Hash01ForCity(i, 201));
                }
                else if (hash < 0.75f)
                {
                    bName = $"Shop_{i}";
                    w = Mathf.Lerp(5f, 10f, hash2);
                    h = Mathf.Lerp(4f, 10f, hash3);
                    d = Mathf.Lerp(5f, 10f, Hash01ForCity(i, 201));
                }
                else
                {
                    bName = $"House_{i}";
                    w = Mathf.Lerp(5f, 9f, hash2);
                    h = Mathf.Lerp(4f, 8f, hash3);
                    d = Mathf.Lerp(5f, 9f, Hash01ForCity(i, 201));
                }

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = bName;
                go.transform.localScale = new Vector3(w, h, d);
                go.transform.position = Vector3.zero; // CitySceneArranger will place it
                Undo.RegisterCreatedObjectUndo(go, "Create City Building");
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            // 3) Run CitySceneArranger to lay everything out
            try
            {
                CitySceneArranger.ArrangeScene(scene, terrain, prompt);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[generateCity] CitySceneArranger failed: {ex.Message}");
            }

            // 4) Auto-frame
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            return AgentToolResult.Ok($"Generated a city with {buildingCount} buildings on {terrainPresetStr} terrain. Roads, sidewalks, and building pads have been automatically created.");
        }

        private static AgentToolResult ToolArrangeCityScene(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return AgentToolResult.Fail("No active scene.");

            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            if (terrain == null)
                return AgentToolResult.Fail("No terrain in scene. Use generateTerrain first.");

            string prompt = args["prompt"]?.Value<string>() ?? "";

            try
            {
                CitySceneArranger.ArrangeScene(scene, terrain, prompt);

                if (SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.FrameSelected();

                return AgentToolResult.Ok("City scene arranged. Buildings placed near roads, terrain flattened under buildings, everything snapped to terrain surface.");
            }
            catch (Exception ex)
            {
                return AgentToolResult.Fail($"City arrangement failed: {ex.Message}");
            }
        }

        private static AgentToolResult ToolFocusSceneView(JObject args)
        {
            string objectName = args["objectName"]?.Value<string>();

            if (!string.IsNullOrWhiteSpace(objectName))
            {
                var go = FindInScene(SceneManager.GetActiveScene(), objectName);
                if (go == null)
                    return AgentToolResult.Fail($"Object '{objectName}' not found.");

                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
                return AgentToolResult.Ok(string.IsNullOrWhiteSpace(objectName)
                    ? "Focused Scene View on current selection."
                    : $"Focused Scene View on '{objectName}'.");
            }

            return AgentToolResult.Fail("No active Scene View found.");
        }

        private static float Hash01ForCity(int index, int salt)
        {
            unchecked
            {
                int h = unchecked((int)2166136261);
                h = (h ^ index) * 16777619;
                h = (h ^ salt) * 16777619;
                int v = h & 0x00FFFFFF;
                return v / 16777216f;
            }
        }
    
        // ──────────────────── HELPERS ────────────────────
    
        internal static GameObject FindInScene(Scene scene, string id)
        {
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(id)) return null;
    
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var found = FindRecursive(root.transform, id);
                if (found != null) return found.gameObject;
            }
            return null;
        }
    
        private static Transform FindRecursive(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    
        private static GameObject CreatePrimitiveObject(string typeName)
        {
            PrimitiveType pt = typeName switch
            {
                "Sphere" => PrimitiveType.Sphere,
                "Capsule" => PrimitiveType.Capsule,
                "Plane" => PrimitiveType.Plane,
                "Cylinder" => PrimitiveType.Cylinder,
                "Quad" => PrimitiveType.Quad,
                _ => PrimitiveType.Cube
            };
            return GameObject.CreatePrimitive(pt);
        }
    
        private static GameObject CreateBuiltinObject(string id, string builtinType)
        {
            var go = new GameObject(id);
            switch (builtinType)
            {
                case "Camera":
                    go.tag = "MainCamera";
                    go.AddComponent<Camera>();
                    if (UnityEngine.Object.FindObjectOfType<AudioListener>() == null)
                        go.AddComponent<AudioListener>();
                    break;
                case "DirectionalLight":
                    var dl = go.AddComponent<Light>();
                    dl.type = LightType.Directional;
                    dl.intensity = 1f;
                    break;
                case "PointLight":
                    var pl = go.AddComponent<Light>();
                    pl.type = LightType.Point;
                    pl.intensity = 1f;
                    pl.range = 10f;
                    break;
                case "SpotLight":
                    var sl = go.AddComponent<Light>();
                    sl.type = LightType.Spot;
                    sl.intensity = 1f;
                    sl.range = 20f;
                    sl.spotAngle = 30f;
                    break;
            }
            return go;
        }
    }
}
