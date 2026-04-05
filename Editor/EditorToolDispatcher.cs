// Assets/Editor/EditorToolDispatcher.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

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
            "inspect_scene" => "Analyzing scene structure...",
            "list_assets" => "Listing project assets...",
            "create_object" => "Creating GameObject...",
            "delete_object" => "Deleting GameObject...",
            "modify_transform" => "Updating transform...",
            "add_component" => "Adding component...",
            "write_script" => "Writing C# script...",
            "delete_script" => "Deleting script...",
            "create_material" => "Creating material...",
            "read_console_errors" => "Checking console for errors...",
            "save_scene" => "Saving scene...",
            "create_scene" => "Creating new scene...",
            "run_full_plan" => "Executing implementation plan...",
            "attach_script" => "Attaching script to object...",
            "generate_terrain" => "Generating terrain...",
            "set_object_active" => "Toggling object visibility...",
            "generate_road_network" => "Generating road system...",
            "generate_settlements" => "Generating city settlements...",
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

        Register(new AgentTool
        {
            Name = "inspect_scene",
            Description = "Returns a structured summary of all objects in the active Unity scene including their ids, kinds, positions, scales, components, and materials.",
            ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
            Execute = ToolInspectScene
        });

        Register(new AgentTool
        {
            Name = "list_assets",
            Description = "Lists project assets: scenes, generated scripts, generated materials.",
            ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
            Execute = ToolListAssets
        });

        Register(new AgentTool
        {
            Name = "create_object",
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
            Name = "delete_object",
            Description = "Deletes a GameObject from the active scene by its name/id.",
            ParameterSchema = @"{""type"":""object"",""required"":[""id""],""properties"":{""id"":{""type"":""string""}},""additionalProperties"":false}",
            Execute = ToolDeleteObject
        });

        Register(new AgentTool
        {
            Name = "modify_transform",
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
            Name = "add_component",
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
            Name = "write_script",
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
            Name = "delete_script",
            Description = "Deletes a generated script file from the project.",
            ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}},""additionalProperties"":false}",
            Execute = ToolDeleteScript
        });

        Register(new AgentTool
        {
            Name = "create_material",
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
            Name = "read_console_errors",
            Description = "Returns recent Unity console errors and warnings.",
            ParameterSchema = @"{""type"":""object"",""properties"":{""maxCount"":{""type"":""integer""}},""additionalProperties"":false}",
            Execute = ToolReadConsoleErrors
        });

        Register(new AgentTool
        {
            Name = "save_scene",
            Description = "Saves the currently active scene.",
            ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
            Execute = ToolSaveScene
        });

        Register(new AgentTool
        {
            Name = "create_scene",
            Description = "Creates a new empty scene and makes it active.",
            ParameterSchema = @"{""type"":""object"",""required"":[""name""],""properties"":{""name"":{""type"":""string""}},""additionalProperties"":false}",
            Execute = ToolCreateScene
        });

        Register(new AgentTool
        {
            Name = "run_full_plan",
            Description = "Applies an entire legacy JSON game plan using the full GameGenerationExecutor pipeline. Use this when you need to create a complete game from a single JSON plan.",
            ParameterSchema = @"{""type"":""object"",""required"":[""planJson""],""properties"":{""planJson"":{""type"":""string"",""description"":""The full JSON plan string""}},""additionalProperties"":false}",
            Execute = ToolRunFullPlan
        });

        Register(new AgentTool
        {
            Name = "attach_script",
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
            Name = "generate_terrain",
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
            Name = "set_object_active",
            Description = "Enables or disables a GameObject.",
            ParameterSchema = @"{""type"":""object"",""required"":[""id"",""active""],""properties"":{""id"":{""type"":""string""},""active"":{""type"":""boolean""}},""additionalProperties"":false}",
            Execute = ToolSetObjectActive
        });
        Register(new AgentTool
        {
            Name = "generate_road_network",
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
            Name = "generate_settlements",
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

        return AgentToolResult.Ok($"Generated terrain with preset={presetStr} seed={seed}.");
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
        if (terrain == null) return AgentToolResult.Fail("No Terrain in scene for roads.");

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
