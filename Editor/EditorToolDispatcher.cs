// Assets/Editor/EditorToolDispatcher.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                // File Operations
                "read_file" => "Reading file...",
                "read_large_file" => "Reading large file...",
                "search_files" => "Searching files...",
                "search_entire_project" => "Scanning entire project...",
                "search_marketplace" => "Searching marketplace...",
                "create_file" => "Creating file...",
                "edit_file" => "Editing file...",
                "delete_file" => "Deleting file...",
                "rename_file" => "Renaming file...",

                // Project Analysis
                "analyze_script" => "Analyzing script...",
                "analyze_folder" => "Analyzing folder...",
                "analyze_project_structure" => "Analyzing project structure...",
                "dependency_analysis" => "Analyzing dependencies...",
                "full_project_scan" => "Performing full project scan...",

                // Unity Specific
                "analyze_prefab" => "Analyzing prefab...",
                "analyze_animator" => "Analyzing animator...",
                "analyze_scriptable_objects" => "Analyzing ScriptableObject...",

                // Agent Execution
                "execute_command" => "Executing shell command...",
                "execute_build" => "Executing Unity build...",
                "run_automated_fix" => "Running automated fixes...",

                // Core tools
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
                "runfullplan" => "Executing generation plan...",
                "attachscript" => "Attaching script to object...",
                "generateterrain" => "Generating terrain...",
                "setobjectactive" => "Toggling object visibility...",
                "generateroadnetwork" => "Generating road system...",
                "generatesettlements" => "Generating city settlements...",
                "generateworld" => "Generating complete world...",
                "queryterraininfo" => "Querying terrain info...",
                "sampleterrain" => "Sampling terrain heights...",
                "terrainheightgrid" => "Getting terrain height grid...",
                "focussceneview" => "Focusing Scene View camera...",
                "searchassets" => "Searching local project assets...",
                "generatecanvasui" => "Generating Canvas UI...",
                "enterplaymode" => "Entering Play Mode...",
                "exitplaymode" => "Exiting Play Mode...",
                "createparticlesystem" => "Creating particle system...",
                "placenature" => "Planting trees and foliage...",

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
            Register(new AgentTool { Name = "read_file", Description = "Read a single file by path.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = FileOperationTools.ReadFile });
            Register(new AgentTool { Name = "read_large_file", Description = "Read a large file with pagination (startLine + maxLines).", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""},""maxLines"":{""type"":""integer""},""startLine"":{""type"":""integer""}}}", Execute = FileOperationTools.ReadLargeFile });
            Register(new AgentTool { Name = "search_files", Description = "Regex search across files in a folder.", ParameterSchema = @"{""type"":""object"",""required"":[""regexKeyword""],""properties"":{""folder"":{""type"":""string""},""pattern"":{""type"":""string""},""regexKeyword"":{""type"":""string""}}}", Execute = FileOperationTools.SearchFiles });
            Register(new AgentTool { Name = "search_entire_project", Description = "Search entire Unity project by keyword. WARNING: use search_marketplace first for tools/assets.", ParameterSchema = @"{""type"":""object"",""required"":[""keyword""],""properties"":{""keyword"":{""type"":""string""}}}", Execute = FileOperationTools.SearchEntireProject });
            Register(new AgentTool { Name = "create_file", Description = "Create a new file with content.", ParameterSchema = @"{""type"":""object"",""required"":[""path"",""content""],""properties"":{""path"":{""type"":""string""},""content"":{""type"":""string""}}}", Execute = FileOperationTools.CreateFile });
            Register(new AgentTool { Name = "edit_file", Description = "Modify an existing file (search/replace or full overwrite).", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""},""search"":{""type"":""string""},""replace"":{""type"":""string""},""content"":{""type"":""string""}}}", Execute = FileOperationTools.EditFile });
            Register(new AgentTool { Name = "delete_file", Description = "Delete a file from the project.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = FileOperationTools.DeleteFile });
            Register(new AgentTool { Name = "rename_file", Description = "Rename a file.", ParameterSchema = @"{""type"":""object"",""required"":[""path"",""newName""],""properties"":{""path"":{""type"":""string""},""newName"":{""type"":""string""}}}", Execute = FileOperationTools.RenameFile });

            // ── PROJECT ANALYSIS ──
            Register(new AgentTool { Name = "analyze_script", Description = "Read and analyze a single script file.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = ProjectAnalysisTools.AnalyzeScript });
            Register(new AgentTool { Name = "analyze_folder", Description = "List folder contents (files and subdirectories).", ParameterSchema = @"{""type"":""object"",""properties"":{""folder"":{""type"":""string""}}}", Execute = ProjectAnalysisTools.AnalyzeFolder });
            Register(new AgentTool { Name = "analyze_project_structure", Description = "Show the full Assets directory tree.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = ProjectAnalysisTools.AnalyzeProjectStructure });
            Register(new AgentTool { Name = "dependency_analysis", Description = "List asset dependencies for a given path.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = ProjectAnalysisTools.DependencyAnalysis });
            Register(new AgentTool { Name = "full_project_scan", Description = "Comprehensive scan of all scripts, prefabs, and scenes.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = ProjectAnalysisTools.FullProjectScan });

            // ── UNITY SPECIFIC ──
            Register(new AgentTool { Name = "analyze_prefab", Description = "Analyze a prefab's components and children.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = UnitySpecificTools.AnalyzePrefab });
            Register(new AgentTool { Name = "analyze_animator", Description = "Analyze animator controller parameters and state machines.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = UnitySpecificTools.AnalyzeAnimator });
            Register(new AgentTool { Name = "analyze_scriptable_objects", Description = "Inspect a ScriptableObject asset as JSON.", ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}}}", Execute = UnitySpecificTools.AnalyzeScriptableObjects });

            // ── AGENT EXECUTION ──
            Register(new AgentTool { Name = "execute_command", Description = "Execute a shell command (requires user approval).", ParameterSchema = @"{""type"":""object"",""required"":[""command""],""properties"":{""command"":{""type"":""string""},""workingDirectory"":{""type"":""string""}}}", Execute = AgentExecutionTools.ExecuteCommand });
            Register(new AgentTool { Name = "execute_build", Description = "Trigger a Unity build.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = AgentExecutionTools.ExecuteBuild });
            Register(new AgentTool { Name = "run_automated_fix", Description = "Check console errors and provide fix directives.", ParameterSchema = @"{""type"":""object"",""properties"":{}}", Execute = AgentExecutionTools.RunAutomatedFix });

            Register(new AgentTool
            {
                Name = "inspectScene",
                Description = "Returns a structured summary of all objects in the active Unity scene including their ids, kinds, positions, scales, components, and materials.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = SceneToolImplementations.InspectScene
            });
    
            Register(new AgentTool
            {
                Name = "listAssets",
                Description = "Lists project assets: scenes, generated scripts, generated materials.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = SceneToolImplementations.ListAssets
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
                Name = "search_marketplace",
                Description = "Searches your previously downloaded marketplace assets and tools (scripts, models, etc) by keyword. ALWAYS use this tool FIRST when looking for a tool or asset you don't already know the path to.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""keyword"":{""type"":""string""}
                    },
                    ""required"":[""keyword""]
                }",
                Execute = AssetFetchTool.ToolSearchMarketplace
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
                Description = "Creates a complex GameObject in the active scene. Treat the terrain as your absolute canvas. If a terrain exists, simply provide X and Z and set snapToTerrain=true to flawlessly anchor objects to the ground. Supports nested children and procedural shapes.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id"",""kind""],
                    ""properties"":{
                        ""id"":{""type"":""string"",""description"":""Unique name for the object""},
                        ""kind"":{""type"":""string"",""enum"":[""Primitive"",""Empty"",""Builtin"",""Procedural""],""description"":""Object kind. Use Procedural for advanced 3D shapes.""},
                        ""primitiveType"":{""type"":""string"",""enum"":[""Cube"",""Sphere"",""Capsule"",""Plane"",""Cylinder"",""Quad""],""description"":""Required if kind=Primitive""},
                        ""builtinType"":{""type"":""string"",""enum"":[""Camera"",""DirectionalLight"",""PointLight"",""SpotLight""],""description"":""Required if kind=Builtin""},
                        ""proceduralType"":{""type"":""string"",""enum"":[""Cone"",""Torus"",""HalfSphere"",""Wedge"",""Arch"",""Stairs"",""Tube"",""Pyramid"",""Ring"",""Ramp"",""LShape"",""TShape"",""Gear"",""Bowl"",""Fence"",""Net"",""Prism"",""House"",""Frustum"",""Cross"",""Barrel"",""Pole"",""RadioTower"",""Bunker"",""Windmill"",""Lighthouse"",""Castle"",""Skyscraper"",""House"",""Cabin"",""Tent"",""Bridge"",""Factory"",""Warehouse"",""Barn"",""Silo"",""WaterTower"",""Car"",""Truck"",""Bus"",""Van"",""SUV"",""Motorcycle"",""Bicycle"",""Train"",""Carriage"",""Tank"",""Jeep"",""Helicopter"",""Airplane"",""Boat"",""Submarine"",""Sword"",""Dagger"",""AxeWeapon"",""Spear"",""Bow"",""Crossbow"",""Pistol"",""Rifle"",""Shotgun"",""SniperRifle"",""RocketLauncher"",""Shield"",""Bomb"",""Mine"",""Turret"",""Chair"",""Table"",""Desk"",""Bed"",""Sofa"",""Bookshelf"",""Cabinet"",""Wardrobe"",""Dresser"",""Nightstand"",""Stool"",""Bench"",""TVStand"",""DiningTable"",""CoffeeTable"",""TV"",""Monitor"",""Keyboard"",""ComputerMouse"",""Laptop"",""PC"",""Phone"",""Tablet"",""Printer"",""Speaker"",""Microphone"",""Camera"",""Router"",""Controller"",""Drone"",""PineTree"",""OakTree"",""PalmTree"",""DeadTree"",""Bush"",""Fern"",""Cactus"",""Mushroom"",""Rock"",""Boulder"",""Log"",""Stump"",""Flower"",""GrassTuft"",""Vine"",""Fridge"",""Stove"",""Microwave"",""Oven"",""Dishwasher"",""Washer"",""Dryer"",""Toaster"",""Blender"",""CoffeeMaker"",""Vacuum"",""Iron"",""Fan"",""AirConditioner"",""Heater"",""Hammer"",""Wrench"",""Screwdriver"",""Saw"",""Drill"",""Pliers"",""Shovel"",""Rake"",""Pickaxe"",""AxeTool"",""Crowbar"",""TapeMeasure"",""Level"",""Trowel"",""Toolbox"",""Apple"",""Banana"",""Burger"",""Pizza"",""Hotdog"",""Bread"",""Cheese"",""Cake"",""Pie"",""Donut"",""IceCream"",""Steak"",""Chicken"",""Fish"",""Can"",""WoodenBarrel"",""Crate"",""CardboardBox"",""Chest"",""Safe"",""TrashCan"",""Dumpster"",""Streetlight"",""TrafficLight"",""Hydrant"",""Mailbox"",""Sign"",""WoodenFence"",""BrickWall"",""Pillar""],""description"":""Required if kind=Procedural. The 3D shape to generate.""},
                        ""meshWidth"":{""type"":""number"",""description"":""Width of the procedural mesh (default 1)""},
                        ""meshHeight"":{""type"":""number"",""description"":""Height of the procedural mesh (default 1)""},
                        ""meshDepth"":{""type"":""number"",""description"":""Depth of the procedural mesh (default 1)""},
                        ""meshRadius"":{""type"":""number"",""description"":""Radius for round shapes (default 0.5)""},
                        ""meshInnerRadius"":{""type"":""number"",""description"":""Inner radius for Torus tube, Tube inner wall, Ring hole (default 0.15)""},
                        ""meshSegments"":{""type"":""integer"",""description"":""Tessellation segments for round shapes (default 24)""},
                        ""meshCount"":{""type"":""integer"",""description"":""Count parameter: stair steps, fence posts, net rings, gear teeth (default 6)""},
                        ""meshThickness"":{""type"":""number"",""description"":""Wall/strand thickness for Arch, Tube, Fence, etc. (default 0.1)""},
                        ""meshTaperRatio"":{""type"":""number"",""description"":""Bottom-to-top taper for Net shape, 0-1 (default 0.6)""},
                        ""colorHex"":{""type"":""string"",""description"":""Hex color for auto-created material, e.g. #FF6600""},
                        ""posX"":{""type"":""number"",""description"":""Local/World X position""},
                        ""posY"":{""type"":""number"",""description"":""Local/World Y position. Ignored if snapToTerrain is true and terrain exists.""},
                        ""posZ"":{""type"":""number"",""description"":""Local/World Z position""},
                        ""snapToTerrain"":{""type"":""boolean"",""description"":""If true, raycasts down from X,Z to place the object perfectly on the terrain surface. Defaults to true.""},
                        ""scaleX"":{""type"":""number"",""description"":""Local X scale""},
                        ""scaleY"":{""type"":""number"",""description"":""Local Y scale""},
                        ""scaleZ"":{""type"":""number"",""description"":""Local Z scale""},
                        ""rotX"":{""type"":""number""},
                        ""rotY"":{""type"":""number""},
                        ""rotZ"":{""type"":""number""},
                        ""parentId"":{""type"":""string"",""description"":""Optional parent object name""},
                        ""anchorTargetId"":{""type"":""string"",""description"":""Optional ID of the object to anchor to. Defaults to parent if omitted.""},
                        ""anchorPoint"":{""type"":""string"",""enum"":[""Center"",""Top"",""Bottom"",""Left"",""Right"",""Front"",""Back""],""description"":""Where on the target object to attach.""},
                        ""myPivot"":{""type"":""string"",""enum"":[""Center"",""Top"",""Bottom"",""Left"",""Right"",""Front"",""Back""],""description"":""What part of this object should touch the anchor point.""},
                        ""anchorOffsetX"":{""type"":""number"",""description"":""Additional X offset after anchoring""},
                        ""anchorOffsetY"":{""type"":""number"",""description"":""Additional Y offset after anchoring""},
                        ""anchorOffsetZ"":{""type"":""number"",""description"":""Additional Z offset after anchoring""},
                        ""components"":{
                            ""type"":""array"",
                            ""items"":{""type"":""string"",""enum"":[""Rigidbody"",""BoxCollider"",""SphereCollider"",""CapsuleCollider"",""MeshCollider"",""CharacterController""]}
                        },
                        ""children"":{
                            ""type"":""array"",
                            ""items"":{""type"":""object"",""description"":""Recursive child objects (same schema)""}
                        }
                    },
                    ""additionalProperties"":false
                }",
                Execute = ObjectToolImplementations.CreateObject
            });
    
            Register(new AgentTool
            {
                Name = "proposePlan",
                Description = "Proposes a game design, script architecture, and logic breakdown to the user for approval. Use this to outline how the game will be built, detailing what scripts are needed and the overall logic. The user can read, modify, and approve your plan before you execute it.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""designDocument""],
                    ""properties"":{
                        ""designDocument"":{""type"":""string"",""description"":""A detailed breakdown of how the game would be, the scripts needed, and the logic.""}
                    }
                }",
                Execute = ObjectToolImplementations.ProposePlan
            });

            Register(new AgentTool
            {
                Name = "deleteObject",
                Description = "Deletes a GameObject from the active scene by its name/id.",
                ParameterSchema = @"{""type"":""object"",""required"":[""id""],""properties"":{""id"":{""type"":""string""}},""additionalProperties"":false}",
                Execute = ObjectToolImplementations.DeleteObject
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
                Execute = ObjectToolImplementations.ModifyTransform
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
                Execute = ObjectToolImplementations.AddComponent
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
                Execute = ScriptToolImplementations.WriteScript
            });
    
            Register(new AgentTool
            {
                Name = "deleteScript",
                Description = "Deletes a generated script file from the project.",
                ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}},""additionalProperties"":false}",
                Execute = ScriptToolImplementations.DeleteScript
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
                Execute = ScriptToolImplementations.WriteShader
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
                Execute = MaterialToolImplementations.CreateMaterial
            });
    
            Register(new AgentTool
            {
                Name = "readConsoleErrors",
                Description = "Returns recent Unity console errors and warnings.",
                ParameterSchema = @"{""type"":""object"",""properties"":{""maxCount"":{""type"":""integer""}},""additionalProperties"":false}",
                Execute = ConsoleToolImplementations.ReadConsoleErrors
            });
    
            Register(new AgentTool
            {
                Name = "saveScene",
                Description = "Saves the currently active scene.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = SceneToolImplementations.SaveScene
            });
    
            Register(new AgentTool
            {
                Name = "createScene",
                Description = "Creates a new empty scene and makes it active.",
                ParameterSchema = @"{""type"":""object"",""required"":[""name""],""properties"":{""name"":{""type"":""string""}},""additionalProperties"":false}",
                Execute = SceneToolImplementations.CreateScene
            });
    
            Register(new AgentTool
            {
                Name = "runFullPlan",
                Description = "Delegates a complex game generation prompt to the local GenerationOrchestrator pipeline. Use this when you need to create a complete game.",
                ParameterSchema = @"{""type"":""object"",""required"":[""prompt""],""properties"":{""prompt"":{""type"":""string"",""description"":""The detailed natural language prompt/goal for the game""}},""additionalProperties"":false}",
                Execute = GenerationToolImplementations.RunFullPlan
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
                Execute = ScriptToolImplementations.AttachScript
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
                Execute = GenerationToolImplementations.GenerateTerrain
            });
    
            Register(new AgentTool
            {
                Name = "setObjectActive",
                Description = "Enables or disables a GameObject.",
                ParameterSchema = @"{""type"":""object"",""required"":[""id"",""active""],""properties"":{""id"":{""type"":""string""},""active"":{""type"":""boolean""}},""additionalProperties"":false}",
                Execute = ObjectToolImplementations.SetObjectActive
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
                Execute = GenerationToolImplementations.GenerateRoadNetwork
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
                }",
                Execute = ObjectToolImplementations.AddComponent
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
                Execute = ScriptToolImplementations.WriteScript
            });
    
            Register(new AgentTool
            {
                Name = "deleteScript",
                Description = "Deletes a generated script file from the project.",
                ParameterSchema = @"{""type"":""object"",""required"":[""path""],""properties"":{""path"":{""type"":""string""}},""additionalProperties"":false}",
                Execute = ScriptToolImplementations.DeleteScript
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
                Execute = ScriptToolImplementations.WriteShader
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
                Execute = MaterialToolImplementations.CreateMaterial
            });
    
            Register(new AgentTool
            {
                Name = "readConsoleErrors",
                Description = "Returns recent Unity console errors and warnings.",
                ParameterSchema = @"{""type"":""object"",""properties"":{""maxCount"":{""type"":""integer""}},""additionalProperties"":false}",
                Execute = ConsoleToolImplementations.ReadConsoleErrors
            });
    
            Register(new AgentTool
            {
                Name = "saveScene",
                Description = "Saves the currently active scene.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = SceneToolImplementations.SaveScene
            });
    
            Register(new AgentTool
            {
                Name = "createScene",
                Description = "Creates a new empty scene and makes it active.",
                ParameterSchema = @"{""type"":""object"",""required"":[""name""],""properties"":{""name"":{""type"":""string""}},""additionalProperties"":false}",
                Execute = SceneToolImplementations.CreateScene
            });
    
            Register(new AgentTool
            {
                Name = "runFullPlan",
                Description = "Delegates a complex game generation prompt to the local GenerationOrchestrator pipeline. Use this when you need to create a complete game.",
                ParameterSchema = @"{""type"":""object"",""required"":[""prompt""],""properties"":{""prompt"":{""type"":""string"",""description"":""The detailed natural language prompt/goal for the game""}},""additionalProperties"":false}",
                Execute = GenerationToolImplementations.RunFullPlan
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
                Execute = ScriptToolImplementations.AttachScript
            });
    
            Register(new AgentTool
            {
                Name = "generateTerrain",
                Description = "Generates terrain in the active scene. Use a preset for quick generation, or provide full parameter overrides for fine-tuned AI control. Any non-zero parameter overrides the preset default.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""preset"":{""type"":""string"",""enum"":[""Plains"",""Hills"",""Mountains"",""MountainValley"",""Island""],""description"":""Base terrain preset (default Hills)""},
                        ""prompt"":{""type"":""string"",""description"":""Optional descriptions like 'massive', 'huge', or 'large' to trigger tiling""},
                        ""seed"":{""type"":""integer"",""description"":""Random seed""},
                        ""sizeXZ"":{""type"":""integer"",""description"":""Terrain width/length in metres""},
                        ""gridSizeX"":{""type"":""integer"",""description"":""Number of tiles in X (massive terrains)""},
                        ""gridSizeZ"":{""type"":""integer"",""description"":""Number of tiles in Z (massive terrains)""},
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
                Execute = GenerationToolImplementations.GenerateTerrain
            });
    
            Register(new AgentTool
            {
                Name = "setObjectActive",
                Description = "Enables or disables a GameObject.",
                ParameterSchema = @"{""type"":""object"",""required"":[""id"",""active""],""properties"":{""id"":{""type"":""string""},""active"":{""type"":""boolean""}},""additionalProperties"":false}",
                Execute = ObjectToolImplementations.SetObjectActive
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
                Execute = GenerationToolImplementations.GenerateRoadNetwork
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
                Execute = GenerationToolImplementations.GenerateSettlements
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
                Execute = SceneToolImplementations.FocusSceneView
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
                        ""gridSizeX"":{""type"":""integer"",""description"":""Number of tiles in X (massive terrains)""},
                        ""gridSizeZ"":{""type"":""integer"",""description"":""Number of tiles in Z (massive terrains)""},
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
                Execute = GenerationToolImplementations.GenerateWorld
            });

            Register(new AgentTool
            {
                Name = "placeNature",
                Description = "Scatters trees, rocks, and foliage across the active terrain based on the biome settings. Call this after generating terrain when you want to add nature.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""seed"":{""type"":""integer"",""description"":""Optional seed""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = GenerationToolImplementations.PlaceNature
            });

            // ── Tier 1 Quick Wins Tools ──
            Register(new AgentTool
            {
                Name = "cloneObject",
                Description = "Clones an existing object in the scene. Useful for placing multiple identical objects.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id"",""newId""],
                    ""properties"":{
                        ""id"":{""type"":""string"",""description"":""The name of the object to clone.""},
                        ""newId"":{""type"":""string"",""description"":""The name for the newly cloned object.""},
                        ""offsetX"":{""type"":""number"",""description"":""Offset position X relative to the original.""},
                        ""offsetY"":{""type"":""number"",""description"":""Offset position Y relative to the original.""},
                        ""offsetZ"":{""type"":""number"",""description"":""Offset position Z relative to the original.""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ObjectToolImplementations.CloneObject
            });

            Register(new AgentTool
            {
                Name = "renameObject",
                Description = "Renames an existing object in the scene.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id"",""newName""],
                    ""properties"":{
                        ""id"":{""type"":""string""},
                        ""newName"":{""type"":""string""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ObjectToolImplementations.RenameObject
            });

            Register(new AgentTool
            {
                Name = "reparentObject",
                Description = "Changes the parent of an object in the scene hierarchy. Pass empty newParentId to unparent (move to root).",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id""],
                    ""properties"":{
                        ""id"":{""type"":""string""},
                        ""newParentId"":{""type"":""string"",""description"":""Name of the new parent object. Leave empty to move to root.""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ObjectToolImplementations.ReparentObject
            });

            Register(new AgentTool
            {
                Name = "getObjectInfo",
                Description = "Returns detailed information about an object (position, components, bounds, children, properties). VERY IMPORTANT: Call this to understand an object's current state before modifying properties.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id""],
                    ""properties"":{
                        ""id"":{""type"":""string""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = ObjectToolImplementations.GetObjectInfo
            });

            Register(new AgentTool
            {
                Name = "setComponentProperty",
                Description = "Sets a property on a specific component of an object (e.g. Rigidbody.mass, Light.color).",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id"",""componentType"",""propertyName"",""value""],
                    ""properties"":{
                        ""id"":{""type"":""string""},
                        ""componentType"":{""type"":""string"",""description"":""e.g. 'Rigidbody', 'Light', 'BoxCollider'""},
                        ""propertyName"":{""type"":""string"",""description"":""e.g. 'mass', 'color', 'isKinematic', 'intensity'""},
                        ""value"":{
                            ""type"":[""number"",""boolean"",""string"",""object""],
                            ""description"":""For Vector2/Vector3 pass {x,y,z}. For Color pass hex string e.g. '#FF0000'.""
                        }
                    },
                    ""additionalProperties"":false
                }",
                Execute = ObjectToolImplementations.SetComponentProperty
            });

            Register(new AgentTool
            {
                Name = "setSceneEnvironment",
                Description = "Modifies global scene environment settings (skybox, ambient light, fog).",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""skyboxColorHex"":{""type"":""string"",""description"":""Changes camera background to solid color""},
                        ""ambientColorHex"":{""type"":""string""},
                        ""enableFog"":{""type"":""boolean""},
                        ""fogColorHex"":{""type"":""string""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = SceneToolImplementations.SetSceneEnvironment
            });


            // ── Tier 2 Games Focus Tools ──
            Register(new AgentTool
            {
                Name = "bakeNavMesh",
                Description = "Bakes the NavMesh for the current scene. Call this after placing terrain and objects if you need AI agents to navigate.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = PhysicsToolImplementations.BakeNavMesh
            });

            Register(new AgentTool
            {
                Name = "raycast",
                Description = "Casts a ray into the scene to find intersection points.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""origin"",""direction""],
                    ""properties"":{
                        ""origin"":{
                            ""type"":""object"",
                            ""properties"":{""x"":{""type"":""number""},""y"":{""type"":""number""},""z"":{""type"":""number""}}
                        },
                        ""direction"":{
                            ""type"":""object"",
                            ""properties"":{""x"":{""type"":""number""},""y"":{""type"":""number""},""z"":{""type"":""number""}}
                        },
                        ""maxDistance"":{""type"":""number""},
                        ""layerMask"":{""type"":""integer""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = PhysicsToolImplementations.Raycast
            });

            Register(new AgentTool
            {
                Name = "createPrefab",
                Description = "Creates a prefab from an existing scene object.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""id"",""path""],
                    ""properties"":{
                        ""id"":{""type"":""string""},
                        ""path"":{""type"":""string"",""description"":""e.g. Assets/Prefabs/MyPrefab.prefab""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = PrefabToolImplementations.CreatePrefab
            });

            Register(new AgentTool
            {
                Name = "instantiatePrefab",
                Description = "Instantiates a prefab from the project into the scene.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""path""],
                    ""properties"":{
                        ""path"":{""type"":""string""},
                        ""newId"":{""type"":""string""},
                        ""x"":{""type"":""number""},
                        ""y"":{""type"":""number""},
                        ""z"":{""type"":""number""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = PrefabToolImplementations.InstantiatePrefab
            });

            Register(new AgentTool
            {
                Name = "listPrefabs",
                Description = "Lists all prefabs in the project.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = PrefabToolImplementations.ListPrefabs
            });

            Register(new AgentTool
            {
                Name = "createAnimator",
                Description = "Creates a new AnimatorController and optionally attaches it to a GameObject.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""path""],
                    ""properties"":{
                        ""path"":{""type"":""string""},
                        ""objectId"":{""type"":""string""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = AnimationToolImplementations.CreateAnimator
            });

            Register(new AgentTool
            {
                Name = "createAnimationClip",
                Description = "Creates a new empty AnimationClip asset.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""path""],
                    ""properties"":{
                        ""path"":{""type"":""string""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = AnimationToolImplementations.CreateAnimationClip
            });

            Register(new AgentTool
            {
                Name = "addAudioSource",
                Description = "Adds or configures an AudioSource component on an object.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""objectId""],
                    ""properties"":{
                        ""objectId"":{""type"":""string""},
                        ""clipPath"":{""type"":""string""},
                        ""playOnAwake"":{""type"":""boolean""},
                        ""loop"":{""type"":""boolean""},
                        ""spatialBlend"":{""type"":""number"",""description"":""0=2D, 1=3D""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = AudioToolImplementations.AddAudioSource
            });

            Register(new AgentTool
            {
                Name = "listAudioClips",
                Description = "Lists all AudioClips in the project.",
                ParameterSchema = @"{""type"":""object"",""properties"":{},""additionalProperties"":false}",
                Execute = AudioToolImplementations.ListAudioClips
            });

            Register(new AgentTool
            {
                Name = "configureLighting",
                Description = "Configures global lighting settings and bakes GI.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""properties"":{
                        ""autoGenerate"":{""type"":""boolean""},
                        ""bakeGI"":{""type"":""boolean""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = LightingToolImplementations.ConfigureLighting
            });

            Register(new AgentTool
            {
                Name = "createProceduralSkybox",
                Description = "Creates a procedural skybox material and optionally applies it.",
                ParameterSchema = @"{
                    ""type"":""object"",
                    ""required"":[""path""],
                    ""properties"":{
                        ""path"":{""type"":""string""},
                        ""sunSize"":{""type"":""string""},
                        ""skyTintHex"":{""type"":""string""},
                        ""groundColorHex"":{""type"":""string""},
                        ""exposure"":{""type"":""string""},
                        ""applyToScene"":{""type"":""boolean""}
                    },
                    ""additionalProperties"":false
                }",
                Execute = LightingToolImplementations.CreateProceduralSkybox
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
                Execute = MaterialToolImplementations.CreateParticleSystem
            });
        }
    
        private static void Register(AgentTool tool)
        {
            _tools[tool.Name] = tool;
        }

        public static GameObject FindInScene(Scene scene, string name)
        {
            if (!scene.IsValid()) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                var found = root.transform.Find(name);
                if (found != null) return found.gameObject;

                // Deep search
                var all = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    if (t.gameObject.name == name)
                        return t.gameObject;
                }
            }
            return null;
        }
    }
}
