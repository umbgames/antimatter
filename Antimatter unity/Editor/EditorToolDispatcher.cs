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
                "writeshader" => "Writing Shader...",
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
                "generateworld" => "Generating complete world (terrain + roads + settlements)...",
                "queryterraininfo" => "Querying terrain info...",
                "sampleterrain" => "Sampling terrain heights...",
                "terrainheightgrid" => "Getting terrain height grid...",
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

            // ── FILE OPERATIONS ──
            Register(new AgentTool { Name = "read_file", Description = "Read a single file.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = FileOperationTools.ReadFile });
            Register(new AgentTool { Name = "read_large_file", Description = "Read a large file with pagination.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""},""maxLines"":{""type"":""integer""},""startLine"":{""type"":""integer""}}}", Execute = FileOperationTools.ReadLargeFile });
            Register(new AgentTool { Name = "search_files", Description = "Search across files.", ParameterSchema = @"{""type"":""object"",""required"":[""regexKeyword""],""properties"":{""folder"":{""type"":""string""},""pattern"":{""type"":""string""},""regexKeyword"":{""type"":""string""}}}", Execute = FileOperationTools.SearchFiles });
            Register(new AgentTool { Name = "search_entire_project", Description = "Search entire Unity project.", ParameterSchema = @"{""type"":""object"",""required"":[""keyword""],""properties"":{""keyword"":{""type"":""string""}}}", Execute = FileOperationTools.SearchEntireProject });
            Register(new AgentTool { Name = "create_file", Description = "Create new file.", ParameterSchema = @"{""type"":""object"",""required"":[""path"",""content""],""properties"":{""path"":{""type"":""string""},""content"":{""type"":""string""}}}", Execute = FileOperationTools.CreateFile });
            Register(new AgentTool { Name = "edit_file", Description = "Modify existing file.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""},""search"":{""type"":""string""},""replace"":{""type"":""string""},""content"":{""type"":""string""}}}", Execute = FileOperationTools.EditFile });
            Register(new AgentTool { Name = "delete_file", Description = "Delete file.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = FileOperationTools.DeleteFile });
            Register(new AgentTool { Name = "rename_file", Description = "Rename file.", ParameterSchema = @"{""type"":""object"",""required"":[""path"",""newName""],""properties"":{""path"":{""type"":""string""},""newName"":{""type"":""string""}}}", Execute = FileOperationTools.RenameFile });

            // ── PROJECT ANALYSIS ──
            Register(new AgentTool { Name = "analyze_script", Description = "Analyze individual script.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = ProjectAnalysisTools.AnalyzeScript });
            Register(new AgentTool { Name = "analyze_folder", Description = "Analyze folder contents.", ParameterSchema = @"{""type"":""object"",""properties"":{""folder"":{""type"":""string""}}}", Execute = ProjectAnalysisTools.AnalyzeFolder });
            Register(new AgentTool { Name = "analyze_project_structure", Description = "Analyze overall project structure.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = ProjectAnalysisTools.AnalyzeProjectStructure });
            Register(new AgentTool { Name = "dependency_analysis", Description = "Analyze references and dependencies.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = ProjectAnalysisTools.DependencyAnalysis });
            Register(new AgentTool { Name = "architecture_review", Description = "Review architecture patterns and project organization.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = ProjectAnalysisTools.ArchitectureReview });
            Register(new AgentTool { Name = "full_project_scan", Description = "Comprehensive project scan.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = ProjectAnalysisTools.FullProjectScan });

            // ── UNITY SPECIFIC ──
            Register(new AgentTool { Name = "analyze_scene", Description = "Analyze Unity scenes.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = UnitySpecificTools.AnalyzeScene });
            Register(new AgentTool { Name = "analyze_prefab", Description = "Analyze prefabs.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = UnitySpecificTools.AnalyzePrefab });
            Register(new AgentTool { Name = "analyze_animator", Description = "Analyze animator controllers and state machines.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = UnitySpecificTools.AnalyzeAnimator });
            Register(new AgentTool { Name = "analyze_scriptable_objects", Description = "Analyze ScriptableObjects.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = UnitySpecificTools.AnalyzeScriptableObjects });
            Register(new AgentTool { Name = "generate_unity_script", Description = "Generate a single Unity script.", ParameterSchema = @"{""type"":""object"",""required"":[""path"",""code""],""properties"":{""path"":{""type"":""string""},""code"":{""type"":""string""}}}", Execute = UnitySpecificTools.GenerateUnityScript });
            Register(new AgentTool { Name = "generate_multiple_scripts", Description = "Generate multiple related scripts.", ParameterSchema = @"{""type"":""object"",""required"":[""scripts""],""properties"":{""scripts"":{""type"":""array"",""items"":{""type"":""object"",""properties"":{""path"":{""type"":""string""},""code"":{""type"":""string""}}}}}}", Execute = UnitySpecificTools.GenerateMultipleScripts });
            Register(new AgentTool { Name = "generate_system_or_framework", Description = "Generate complete systems or frameworks.", ParameterSchema = @"{""type"":""object"",""required"":[""scripts""],""properties"":{""scripts"":{""type"":""array"",""items"":{""type"":""object"",""properties"":{""path"":{""type"":""string""},""code"":{""type"":""string""}}}}}}", Execute = UnitySpecificTools.GenerateSystemOrFramework });

            // ── AGENT EXECUTION ──
            Register(new AgentTool { Name = "execute_command", Description = "Execute shell command on user's machine.", ParameterSchema = @"{""type"":""object"",""required"":[""command""],""properties"":{""command"":{""type"":""string""},""workingDirectory"":{""type"":""string""}}}", Execute = AgentExecutionTools.ExecuteCommand });
            Register(new AgentTool { Name = "execute_build", Description = "Trigger Unity build workflows.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = AgentExecutionTools.ExecuteBuild });
            Register(new AgentTool { Name = "run_automated_fix", Description = "Attempt automated fixes for console errors.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = AgentExecutionTools.RunAutomatedFix });
            Register(new AgentTool { Name = "retry_fix_attempt", Description = "Retry previous fixes.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = AgentExecutionTools.RetryFixAttempt });
            Register(new AgentTool { Name = "debug_session_start", Description = "Start a debugging session (Play Mode).", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = AgentExecutionTools.DebugSessionStart });
            Register(new AgentTool { Name = "error_investigation", Description = "Investigate errors and exceptions.", ParameterSchema = @"{""type"":""object"",""properties"":{""maxCount"":{""type"":""integer""}}}", Execute = AgentExecutionTools.ErrorInvestigation });

            // ── CONTEXT MANAGEMENT ──
            Register(new AgentTool { Name = "add_file_to_context", Description = "Add file context to active memory.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = ContextManagementTools.AddFileToContext });
            Register(new AgentTool { Name = "add_folder_to_context", Description = "Add folder context to active memory.", ParameterSchema = @"{""type"":""object"",""required"":[""folder""],""properties"":{""folder"":{""type"":""string""}}}", Execute = ContextManagementTools.AddFolderToContext });
            Register(new AgentTool { Name = "add_scene_to_context", Description = "Add scene context to active memory.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = ContextManagementTools.AddSceneToContext });
            Register(new AgentTool { Name = "add_entire_project_to_context", Description = "Add entire project context to active memory.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = ContextManagementTools.AddEntireProjectToContext });

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
                Name = "writeShader",
                Description = "Writes a Unity Shader file (.shader or .hlsl).",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""path"",""code""],
                    ""properties"":{
                        ""path"":{""type"":""string"",""description"":""e.g. Assets/Generated/MyShader.shader""},
                        ""code"":{""type"":""string"",""description"":""Full Shader/HLSL code content""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolWriteShader
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
                Description = "Generates terrain in the active scene. Use a preset for quick generation, or provide full parameter overrides for fine-tuned AI control. Any non-zero parameter overrides the preset default.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""preset"":{""type"":""string"",""enum"":[""Plains"",""Hills"",""Mountains"",""MountainValley"",""Island""],""description"":""Base terrain preset (default Hills)""},
                        ""seed"":{""type"":""integer"",""description"":""Random seed""},
                        ""sizeXZ"":{""type"":""integer"",""description"":""Terrain width/length in metres""},
                        ""heightY"":{""type"":""integer"",""description"":""Max height in metres""},
                        ""heightmapResolution"":{""type"":""integer"",""description"":""Heightmap resolution (e.g. 257, 513)""},
                        ""octaves"":{""type"":""integer"",""description"":""Noise octaves (1-8)""},
                        ""lacunarity"":{""type"":""number"",""description"":""Noise lacunarity""},
                        ""persistence"":{""type"":""number"",""description"":""Noise persistence""},
                        ""baseFrequency"":{""type"":""number"",""description"":""Base noise frequency""},
                        ""ridgeStrength"":{""type"":""number"",""description"":""Ridge noise strength 0..1""},
                        ""erosionPasses"":{""type"":""number"",""description"":""Smoothing passes 0..50""},
                        ""seaLevel"":{""type"":""number"",""description"":""Sea level 0..1""},
                        ""valleyWidth"":{""type"":""number"",""description"":""Valley width 0..1""},
                        ""valleyDepth"":{""type"":""number"",""description"":""Valley depth 0..1""},
                        ""biome"":{""type"":""string"",""description"":""e.g. 'Desert', 'Snow', 'Lush'""}
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

            // ── Spatial Awareness Tools ──
            Register(new AgentTool
            {
                Name = "queryTerrainInfo",
                Description = "Returns terrain bounds, resolution, size, and world-space extents. Call this FIRST after generating terrain to understand the coordinate system before placing objects.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = TerrainQueryTool.ToolQueryTerrainInfo
            });

            Register(new AgentTool
            {
                Name = "sampleTerrain",
                Description = "Samples terrain height, slope, and surface normal at specific world-space (x, z) coordinates. Use this to check exact heights before placing buildings or roads. Max 64 points per call.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""points""],
                    ""properties"":{
                        ""points"":{
                            ""type"":""array"",
                            ""items"":{
                                ""type"":""object"",
                                ""properties"":{""x"":{""type"":""number""},""z"":{""type"":""number""}},
                                ""required"":[""x"",""z""]
                            }
                        }
                    },
                    ""additionalProperties"":false
                }",
                Execute = TerrainQueryTool.ToolSampleTerrain
            });

            Register(new AgentTool
            {
                Name = "terrainHeightGrid",
                Description = "Returns a low-resolution grid of terrain heights (4x4 to 16x16) for spatial overview. Helps understand the general shape of the terrain landscape before placing settlements or roads.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""gridSize"":{""type"":""integer"",""description"":""Grid resolution (4-16, default 8)""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = TerrainQueryTool.ToolTerrainHeightGrid
            });

            Register(new AgentTool
            {
                Name = "generateWorld",
                Description = "Generates a complete world in one call: terrain + road network + settlements. This is the most efficient way to create a full open-world scene. Provide terrain params, road hubs, and settlement nodes. All sub-systems are optional.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""terrainPreset"":{""type"":""string"",""enum"":[""Plains"",""Hills"",""Mountains"",""MountainValley"",""Island""]},
                        ""terrainSeed"":{""type"":""integer""},
                        ""sizeXZ"":{""type"":""integer"",""description"":""Terrain size in metres""},
                        ""heightY"":{""type"":""integer"",""description"":""Max terrain height""},
                        ""octaves"":{""type"":""integer""},
                        ""lacunarity"":{""type"":""number""},
                        ""persistence"":{""type"":""number""},
                        ""baseFrequency"":{""type"":""number""},
                        ""ridgeStrength"":{""type"":""number""},
                        ""erosionPasses"":{""type"":""number""},
                        ""biome"":{""type"":""string"",""description"":""e.g. 'Desert', 'Snow', 'Lush'""},
                        ""roadHubs"":{
                            ""type"":""array"",
                            ""items"":{
                                ""type"":""object"",
                                ""properties"":{""x"":{""type"":""number""},""y"":{""type"":""number""},""z"":{""type"":""number""}},
                                ""required"":[""x"",""y"",""z""]
                            }
                        },
                        ""settlements"":{
                            ""type"":""array"",
                            ""items"":{
                                ""type"":""object"",
                                ""properties"":{
                                    ""x"":{""type"":""number""},""y"":{""type"":""number""},""z"":{""type"":""number""},
                                    ""radius"":{""type"":""number""},
                                    ""buildingCount"":{""type"":""integer""},
                                    ""style"":{""type"":""string"",""enum"":[""Auto"",""ModernCityBlocks"",""MedievalTown"",""Suburban""]}
                                },
                                ""required"":[""x"",""y"",""z"",""radius"",""buildingCount"",""style""]
                            }
                        },
                        ""prompt"":{""type"":""string"",""description"":""Style prompt (e.g. 'medieval mountain kingdom with bridges')""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolGenerateWorld
            });

            Register(new AgentTool
            {
                Name = "createParticleSystem",
                Description = "Spawns a Particle System. Use this to create water splashes, fire, smoke, sparks, etc.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id"", ""style""],
                    ""properties"":{
                        ""id"":{""type"":""string""},
                        ""x"":{""type"":""number""},
                        ""y"":{""type"":""number""},
                        ""z"":{""type"":""number""},
                        ""style"":{""type"":""string"",""enum"":[""Fire"",""Smoke"",""WaterSplash"",""Magic"",""Sparks""]},
                        ""colorHex"":{""type"":""string"",""description"":""e.g. #FF0000""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ToolCreateParticleSystem
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

        private static AgentToolResult ToolWriteShader(JObject args)
        {
            string path = args["path"]?.Value<string>();
            string code = args["code"]?.Value<string>();
    
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/") || (!path.EndsWith(".shader") && !path.EndsWith(".hlsl")))
                return AgentToolResult.Fail("Invalid path. Must start with Assets/ and end with .shader or .hlsl");
            if (string.IsNullOrWhiteSpace(code))
                return AgentToolResult.Fail("Code is empty.");
    
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
    
            File.WriteAllText(path, code, System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(path);
    
            return AgentToolResult.Ok($"Wrote shader successfully to {path}.");
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
    
            // Build SchemaTerrainParams from the args for AI-controlled generation
            var schema = new SchemaTerrainParams
            {
                seed = seed,
                sizeXZ = args["sizeXZ"]?.Value<int>() ?? 0,
                heightY = args["heightY"]?.Value<int>() ?? 0,
                heightmapResolution = args["heightmapResolution"]?.Value<int>() ?? 0,
                octaves = args["octaves"]?.Value<int>() ?? 0,
                lacunarity = args["lacunarity"]?.Value<float>() ?? 0f,
                persistence = args["persistence"]?.Value<float>() ?? 0f,
                baseFrequency = args["baseFrequency"]?.Value<float>() ?? 0f,
                ridgeStrength = args["ridgeStrength"]?.Value<float>() ?? 0f,
                erosionPasses = args["erosionPasses"]?.Value<float>() ?? 0f,
                seaLevel = args["seaLevel"]?.Value<float>() ?? 0f,
                valleyWidth = args["valleyWidth"]?.Value<float>() ?? 0f,
                valleyDepth = args["valleyDepth"]?.Value<float>() ?? 0f,
                biome = args["biome"]?.Value<string>()
            };

            var scene = SceneManager.GetActiveScene();
            var go = TerrainGenerator.CreateOrReplaceTerrain("GeneratedTerrain", schema, presetStr, Vector3.zero);
            SceneManager.MoveGameObjectToScene(go, scene);

            // Auto-select & frame in Scene View so the user can see it
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            var t = go.GetComponent<Terrain>();
            string sizeInfo = t != null ? $" Size=({t.terrainData.size.x:F0},{t.terrainData.size.y:F0},{t.terrainData.size.z:F0})" : "";
            return AgentToolResult.Ok($"Generated terrain with preset={presetStr} seed={seed}.{sizeInfo} Terrain is now selected and framed in Scene View.");
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

        // generateCity and arrangeCityScene removed — use generateSettlements + generateRoadNetwork instead.

        private static AgentToolResult ToolGenerateWorld(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return AgentToolResult.Fail("No active scene.");

            string prompt = args["prompt"]?.Value<string>() ?? "";
            var sb = new System.Text.StringBuilder();

            // ── 1. Terrain ──
            string presetStr = args["terrainPreset"]?.Value<string>() ?? "Hills";
            int terrainSeed = args["terrainSeed"]?.Value<int>() ?? UnityEngine.Random.Range(0, int.MaxValue);

            var schema = new SchemaTerrainParams
            {
                seed = terrainSeed,
                sizeXZ = args["sizeXZ"]?.Value<int>() ?? 0,
                heightY = args["heightY"]?.Value<int>() ?? 0,
                octaves = args["octaves"]?.Value<int>() ?? 0,
                lacunarity = args["lacunarity"]?.Value<float>() ?? 0f,
                persistence = args["persistence"]?.Value<float>() ?? 0f,
                baseFrequency = args["baseFrequency"]?.Value<float>() ?? 0f,
                ridgeStrength = args["ridgeStrength"]?.Value<float>() ?? 0f,
                erosionPasses = args["erosionPasses"]?.Value<float>() ?? 0f,
                biome = args["biome"]?.Value<string>()
            };

            var terrainGo = TerrainGenerator.CreateOrReplaceTerrain("GeneratedTerrain", schema, presetStr, Vector3.zero);
            SceneManager.MoveGameObjectToScene(terrainGo, scene);
            var terrain = terrainGo.GetComponent<Terrain>();
            sb.AppendLine($"Terrain generated: preset={presetStr}, seed={terrainSeed}, size=({terrain.terrainData.size.x:F0},{terrain.terrainData.size.y:F0},{terrain.terrainData.size.z:F0})");

            // ── 2. Roads ──
            RoadNetworkPlanner.RoadNetworkResult roadsResult = null;
            var hubsToken = args["roadHubs"] as JArray;
            if (hubsToken != null && hubsToken.Count >= 2)
            {
                var hubs = new List<Vector3>();
                foreach (var h in hubsToken)
                    hubs.Add(new Vector3(h["x"].Value<float>(), h["y"].Value<float>(), h["z"].Value<float>()));

                var roadSettings = new RoadGenSettings { seed = terrainSeed };
                try
                {
                    RoadNetworkPlanner.GenerateRoadSystem(scene, terrain, roadSettings, hubs, null, false, null);
                    roadsResult = RoadNetworkPlanner.LastResult;
                    sb.AppendLine($"Roads generated: {hubs.Count} hubs");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Roads failed: {ex.Message}");
                }
            }
            else
            {
                // Fallback: generate automatic road network if not provided by AI
                var roadSettings = new RoadGenSettings { seed = terrainSeed };
                try
                {
                    RoadNetworkPlanner.GenerateRoadSystem(scene, terrain, roadSettings, null, null, false, null);
                    roadsResult = RoadNetworkPlanner.LastResult;
                    sb.AppendLine($"Roads generated (automatic): {roadSettings.hubCount} hubs");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Roads failed (automatic): {ex.Message}");
                }
            }

            // ── 3. Settlements ──
            var setToken = args["settlements"] as JArray;
            if (setToken != null && setToken.Count > 0 && roadsResult != null)
            {
                var explicitSets = new List<SettlementNode>();
                
                float dynamicRadius = terrain.terrainData.size.x * 0.12f; // e.g. 12% of terrain width
                float dynamicBuildings = (terrain.terrainData.size.x * terrain.terrainData.size.z) / 4000f;

                foreach (var s in setToken)
                {
                    explicitSets.Add(new SettlementNode
                    {
                        center = new Vector3(s["x"].Value<float>(), s["y"].Value<float>(), s["z"].Value<float>()),
                        radius = s["radius"]?.Value<float>() > 0 ? s["radius"].Value<float>() : dynamicRadius,
                        buildingCount = s["buildingCount"]?.Value<int>() > 0 ? s["buildingCount"].Value<int>() : (int)dynamicBuildings,
                        style = s["style"]?.Value<string>() ?? "Auto"
                    });
                }

                var setSettings = new SettlementGenSettings 
                { 
                    seed = terrainSeed,
                    maxSlopeDeg = 55f // Brute force slope tolerance
                };
                try
                {
                    SettlementNetworkPlanner.GenerateSettlements(scene, terrain, roadsResult, setSettings, explicitSets, prompt, null, null);
                    sb.AppendLine($"Settlements generated: {explicitSets.Count}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Settlements failed: {ex.Message}");
                }
            }

            // Snap objects to terrain
            GameGenerationExecutor.TerrainSnapper.SnapSceneObjectsToTerrain(scene, terrain);

            // Ensure camera and light
            SceneDefaults.EnsureCameraAndLightIfNeeded(scene, true);

            // Auto-select & frame terrain
            Selection.activeGameObject = terrainGo;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            return AgentToolResult.Ok(sb.ToString());
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

        private static AgentToolResult ToolCreateParticleSystem(JObject args)
        {
            string id = args["id"]?.Value<string>();
            if (string.IsNullOrEmpty(id)) return AgentToolResult.Fail("id is required.");

            float x = args["x"]?.Value<float>() ?? 0f;
            float y = args["y"]?.Value<float>() ?? 0f;
            float z = args["z"]?.Value<float>() ?? 0f;
            string style = args["style"]?.Value<string>() ?? "Smoke";
            string colorHex = args["colorHex"]?.Value<string>() ?? "#FFFFFF";

            GameObject go = new GameObject(id);
            go.transform.position = new Vector3(x, y, z);
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;

            if (ColorUtility.TryParseHtmlString(colorHex, out Color c))
            {
                main.startColor = c;
            }

            switch (style)
            {
                case "Fire":
                    main.startSpeed = 2f;
                    main.startSize = 1.5f;
                    main.startLifetime = 1.5f;
                    emission.rateOverTime = 20;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 15;
                    main.startColor = new Color(1f, 0.5f, 0f, 0.8f);
                    break;
                case "Smoke":
                    main.startSpeed = 1f;
                    main.startSize = new ParticleSystem.MinMaxCurve(1f, 3f);
                    main.startLifetime = 3f;
                    emission.rateOverTime = 10;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 25;
                    main.startColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    break;
                case "WaterSplash":
                    main.startSpeed = 5f;
                    main.startSize = 0.2f;
                    main.startLifetime = 0.8f;
                    main.gravityModifier = 1f;
                    emission.rateOverTime = 50;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 30;
                    main.startColor = new Color(0.7f, 0.9f, 1f, 0.8f);
                    break;
                case "Magic":
                    main.startSpeed = 0.5f;
                    main.startSize = 0.5f;
                    main.startLifetime = 2f;
                    emission.rateOverTime = 30;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 2f;
                    break;
                case "Sparks":
                    main.startSpeed = 10f;
                    main.startSize = 0.1f;
                    main.startLifetime = 0.5f;
                    main.gravityModifier = 1.5f;
                    emission.rateOverTime = 0;
                    emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });
                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    main.startColor = new Color(1f, 0.8f, 0.2f, 1f);
                    break;
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create ParticleSystem {id}");
            return AgentToolResult.Ok($"Created {style} particle system at {x}, {y}, {z}");
        }
    }
}
