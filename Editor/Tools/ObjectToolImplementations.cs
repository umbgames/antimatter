using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    internal static class ObjectToolImplementations
    {
        internal static AgentToolResult ProposePlan(JObject args)
        {
            string designDocument = args["designDocument"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(designDocument))
                return AgentToolResult.Fail("No designDocument provided in the plan.");

            PlanReviewWindow.ShowModal(designDocument);

            if (PlanReviewWindow.UserProceeded)
            {
                return AgentToolResult.Ok($"Plan approved! The user reviewed and finalized the plan as follows:\n\n{PlanReviewWindow.FinalPlanText}\n\nPlease proceed to execute this plan using your available tools (createObject, writeScript, etc).");
            }
            else
            {
                return AgentToolResult.Fail("User REJECTED the plan. Ask the user what needs to be changed and propose a new plan.");
            }
        }

        internal static AgentToolResult CreateObject(JObject args)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return AgentToolResult.Fail("No active scene.");

            string id = args["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id))
                return AgentToolResult.Fail("id is required.");

            // Check parentId
            GameObject parent = null;
            string parentId = args["parentId"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(parentId))
            {
                parent = EditorToolDispatcher.FindInScene(scene, parentId);
            }

            var go = CreateObjectRecursive(args, parent, scene);

            return AgentToolResult.Ok($"Created complex object '{go.name}'.");
        }

        internal static GameObject CreateObjectRecursive(JObject args, GameObject parent, Scene scene)
        {
            string id = args["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id)) id = "NewObject";

            string kind = args["kind"]?.Value<string>() ?? "Primitive";

            GameObject go = EditorToolDispatcher.FindInScene(scene, id);
            if (go == null)
            {
                switch (kind)
                {
                    case "Empty":
                        go = new GameObject(id);
                        break;
                    case "Builtin":
                        string builtinType = args["builtinType"]?.Value<string>() ?? "Camera";
                        go = CreateBuiltinObject(id, builtinType);
                        break;
                    case "Procedural":
                        go = CreateProceduralObject(id, args);
                        break;
                    case "Primitive":
                    default:
                        string primType = args["primitiveType"]?.Value<string>() ?? "Cube";
                        go = CreatePrimitiveObject(primType);
                        break;
                }
                go.name = id;
            }

            // Position
            float px = args["posX"]?.Value<float>() ?? 0f;
            float py = args["posY"]?.Value<float>() ?? 0f;
            float pz = args["posZ"]?.Value<float>() ?? 0f;

            bool snapToTerrain = args["snapToTerrain"]?.Value<bool>() ?? true;
            if (snapToTerrain && parent == null)
            {
                var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
                if (terrain != null && terrain.terrainData != null)
                {
                    // Treat px and pz as relative to the center of the terrain
                    
                    
                    py = terrain.SampleHeight(new Vector3(px, 0, pz)) + terrain.transform.position.y;
                }
            }

            if (parent != null)
                go.transform.localPosition = new Vector3(px, py, pz);
            else
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
            {
                if (parent != null)
                    go.transform.localRotation = Quaternion.Euler(rx, ry, rz);
                else
                    go.transform.rotation = Quaternion.Euler(rx, ry, rz);
            }

            // --- STAMP TERRAIN LOGIC --- // REMOVED to prevent flattening terrain

            Undo.RegisterCreatedObjectUndo(go, "Agent Create Object");
            
            if (parent != null)
                go.transform.SetParent(parent.transform, false);
            else
                SceneManager.MoveGameObjectToScene(go, scene);

            // Relative Positioning (Anchors & Pivots)
            string anchorPoint = args["anchorPoint"]?.Value<string>();
            string myPivot = args["myPivot"]?.Value<string>();

            if (!string.IsNullOrEmpty(anchorPoint) || !string.IsNullOrEmpty(myPivot))
            {
                string targetId = args["anchorTargetId"]?.Value<string>();
                GameObject anchorTarget = null;
                
                if (!string.IsNullOrEmpty(targetId))
                    anchorTarget = EditorToolDispatcher.FindInScene(scene, targetId);
                
                if (anchorTarget == null) 
                    anchorTarget = parent;

                if (anchorTarget != null)
                {
                    Bounds targetBounds = GetBoundsExcluding(anchorTarget, go);
                    Bounds myBounds = GetBoundsExcluding(go, null);

                    Vector3 targetPos = GetPointOnBounds(targetBounds, anchorPoint ?? "Center");
                    Vector3 myPos = GetPointOnBounds(myBounds, myPivot ?? "Center");

                    Vector3 offset = targetPos - myPos;
                    go.transform.position += offset;

                    float ox = args["anchorOffsetX"]?.Value<float>() ?? 0f;
                    float oy = args["anchorOffsetY"]?.Value<float>() ?? 0f;
                    float oz = args["anchorOffsetZ"]?.Value<float>() ?? 0f;
                    
                    go.transform.position += new Vector3(ox, oy, oz);
                }
            }

            // Apply components
            if (args["components"] is JArray componentsToken)
            {
                foreach (var compToken in componentsToken)
                {
                    string compType = compToken.Value<string>();
                    if (!string.IsNullOrWhiteSpace(compType))
                        BuiltinComponentFactory.Add(go, compType);
                }
            }

            // Process recursive children
            if (args["children"] is JArray childrenToken)
            {
                foreach (var childToken in childrenToken)
                {
                    if (childToken is JObject childObj)
                    {
                        CreateObjectRecursive(childObj, go, scene);
                    }
                }
            }

            return go;
        }

        internal static Bounds GetBoundsExcluding(GameObject target, GameObject exclude)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            var validRenderers = renderers.Where(r => r.gameObject != exclude && (exclude == null || !r.transform.IsChildOf(exclude.transform))).ToList();
            
            if (validRenderers.Count == 0) return new Bounds(target.transform.position, Vector3.zero);
            
            Bounds bounds = validRenderers[0].bounds;
            for (int i = 1; i < validRenderers.Count; i++)
                bounds.Encapsulate(validRenderers[i].bounds);
            return bounds;
        }

        internal static Vector3 GetPointOnBounds(Bounds b, string pointName)
        {
            Vector3 center = b.center;
            Vector3 extents = b.extents;
            
            switch (pointName.ToLower())
            {
                case "top": return center + new Vector3(0, extents.y, 0);
                case "bottom": return center + new Vector3(0, -extents.y, 0);
                case "left": return center + new Vector3(-extents.x, 0, 0);
                case "right": return center + new Vector3(extents.x, 0, 0);
                case "front": return center + new Vector3(0, 0, extents.z);
                case "back": return center + new Vector3(0, 0, -extents.z);
                case "center":
                default:
                    return center;
            }
        }

        internal static AgentToolResult DeleteObject(JObject args)
        {
            string id = args["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id))
                return AgentToolResult.Fail("id is required.");

            var scene = SceneManager.GetActiveScene();
            var go = EditorToolDispatcher.FindInScene(scene, id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");

            Undo.DestroyObjectImmediate(go);
            return AgentToolResult.Ok($"Deleted '{id}'.");
        }

        internal static AgentToolResult ModifyTransform(JObject args)
        {
            string id = args["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id))
                return AgentToolResult.Fail("id is required.");

            var go = EditorToolDispatcher.FindInScene(SceneManager.GetActiveScene(), id);
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

        internal static AgentToolResult AddComponent(JObject args)
        {
            string id = args["id"]?.Value<string>();
            string compType = args["componentType"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(compType))
                return AgentToolResult.Fail("id and componentType are required.");

            var go = EditorToolDispatcher.FindInScene(SceneManager.GetActiveScene(), id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");

            var comp = BuiltinComponentFactory.Add(go, compType);
            if (comp == null)
                return AgentToolResult.Fail($"Unknown component type: {compType}");

            return AgentToolResult.Ok($"Added {compType} to '{id}'.");
        }

        internal static AgentToolResult SetObjectActive(JObject args)
        {
            string id = args["id"]?.Value<string>();
            bool active = args["active"]?.Value<bool>() ?? true;

            if (string.IsNullOrWhiteSpace(id))
                return AgentToolResult.Fail("id is required.");

            var go = EditorToolDispatcher.FindInScene(SceneManager.GetActiveScene(), id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");

            Undo.RecordObject(go, "Agent Set Active");
            go.SetActive(active);
            return AgentToolResult.Ok($"Set '{id}' active={active}.");
        }

        internal static GameObject CreatePrimitiveObject(string typeName)
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

        internal static GameObject CreateBuiltinObject(string id, string builtinType)
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

        internal static GameObject CreateProceduralObject(string id, JObject args)
        {
            string procType = args["proceduralType"]?.Value<string>() ?? "Cone";
            if (!string.IsNullOrEmpty(procType) && char.IsLower(procType[0])) {
                procType = char.ToUpper(procType[0]) + procType.Substring(1);
            }

            float width       = args["meshWidth"]?.Value<float>()      ?? 1f;
            float height      = args["meshHeight"]?.Value<float>()     ?? 1f;
            float depth       = args["meshDepth"]?.Value<float>()      ?? 1f;
            float radius      = args["meshRadius"]?.Value<float>()     ?? 0.5f;
            float innerRadius = args["meshInnerRadius"]?.Value<float>()?? 0.15f;
            int   segments    = args["meshSegments"]?.Value<int>()     ?? 24;
            int   secSegments = 12;
            int   count       = args["meshCount"]?.Value<int>()        ?? 6;
            float thickness   = args["meshThickness"]?.Value<float>()  ?? 0.1f;
            float taperRatio  = args["meshTaperRatio"]?.Value<float>() ?? 0.6f;

            Mesh mesh = ProceduralMeshFactory.Create(procType,
                width: width, height: height, depth: depth,
                radius: radius, innerRadius: innerRadius,
                segments: segments, secondarySegments: secSegments,
                count: count, thickness: thickness, taperRatio: taperRatio);

            if (mesh == null)
            {
                // Fallback to a cube if the shape is unknown
                Debug.LogWarning($"[Antimatter] Unknown procedural shape '{procType}', falling back to Cube.");
                return GameObject.CreatePrimitive(PrimitiveType.Cube);
            }

            var go = new GameObject(id);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();

            // Auto-create material with optional color
            string colorHex = args["colorHex"]?.Value<string>();
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader ?? Shader.Find("Diffuse"));

            if (!string.IsNullOrWhiteSpace(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color col))
                mat.color = col;
            else
                mat.color = new Color(0.8f, 0.8f, 0.8f, 1f); // neutral light grey

            mr.sharedMaterial = mat;
            return go;
        }

        // ══════════════════════════════════════════════════════════════════
        // TIER 1 QUICK WINS
        // ══════════════════════════════════════════════════════════════════

        internal static AgentToolResult CloneObject(JObject args)
        {
            string id = args["id"]?.Value<string>();
            string newId = args["newId"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newId))
                return AgentToolResult.Fail("id and newId are required.");

            var scene = SceneManager.GetActiveScene();
            var source = EditorToolDispatcher.FindInScene(scene, id);
            if (source == null)
                return AgentToolResult.Fail($"Source object '{id}' not found.");

            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = newId;

            float ox = args["offsetX"]?.Value<float>() ?? 0f;
            float oy = args["offsetY"]?.Value<float>() ?? 0f;
            float oz = args["offsetZ"]?.Value<float>() ?? 0f;

            clone.transform.position += new Vector3(ox, oy, oz);

            Undo.RegisterCreatedObjectUndo(clone, "Agent Clone Object");
            return AgentToolResult.Ok($"Cloned '{id}' to '{newId}'.");
        }

        internal static AgentToolResult RenameObject(JObject args)
        {
            string id = args["id"]?.Value<string>();
            string newName = args["newName"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newName))
                return AgentToolResult.Fail("id and newName are required.");

            var go = EditorToolDispatcher.FindInScene(SceneManager.GetActiveScene(), id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");

            Undo.RecordObject(go, "Agent Rename Object");
            go.name = newName;

            return AgentToolResult.Ok($"Renamed '{id}' to '{newName}'.");
        }

        internal static AgentToolResult ReparentObject(JObject args)
        {
            string id = args["id"]?.Value<string>();
            string newParentId = args["newParentId"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(id))
                return AgentToolResult.Fail("id is required.");

            var scene = SceneManager.GetActiveScene();
            var go = EditorToolDispatcher.FindInScene(scene, id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");

            Transform newParent = null;
            if (!string.IsNullOrWhiteSpace(newParentId))
            {
                var parentGo = EditorToolDispatcher.FindInScene(scene, newParentId);
                if (parentGo == null)
                    return AgentToolResult.Fail($"Parent '{newParentId}' not found.");
                newParent = parentGo.transform;
            }

            Undo.SetTransformParent(go.transform, newParent, true, "Agent Reparent Object");

            return AgentToolResult.Ok(newParent == null 
                ? $"Moved '{id}' to root." 
                : $"Reparented '{id}' under '{newParentId}'.");
        }

        internal static AgentToolResult GetObjectInfo(JObject args)
        {
            string id = args["id"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(id))
                return AgentToolResult.Fail("id is required.");

            var go = EditorToolDispatcher.FindInScene(SceneManager.GetActiveScene(), id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"INFO FOR '{id}':");
            sb.AppendLine($"  Position: {go.transform.position}");
            sb.AppendLine($"  Rotation: {go.transform.eulerAngles}");
            sb.AppendLine($"  Scale: {go.transform.localScale}");
            sb.AppendLine($"  Active: {go.activeSelf} (Hierarchy: {go.activeInHierarchy})");
            sb.AppendLine($"  Static: {go.isStatic}");
            sb.AppendLine($"  Layer: {LayerMask.LayerToName(go.layer)}");
            sb.AppendLine($"  Tag: {go.tag}");
            
            var parent = go.transform.parent;
            sb.AppendLine($"  Parent: {(parent != null ? parent.name : "None")}");

            sb.Append("  Children: ");
            if (go.transform.childCount == 0) sb.AppendLine("None");
            else
            {
                var childNames = new System.Collections.Generic.List<string>();
                for (int i = 0; i < go.transform.childCount; i++)
                    childNames.Add(go.transform.GetChild(i).name);
                sb.AppendLine(string.Join(", ", childNames));
            }

            var bounds = GetBoundsExcluding(go, null);
            sb.AppendLine($"  Bounds: Center={bounds.center}, Size={bounds.size}");

            sb.AppendLine("  Components:");
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                sb.AppendLine($"    - {comp.GetType().Name}");
                
                // Print key properties for common components
                if (comp is Rigidbody rb)
                    sb.AppendLine($"        Mass={rb.mass}, Drag={rb.drag}, Kinematic={rb.isKinematic}, Gravity={rb.useGravity}");
                else if (comp is Collider coll)
                    sb.AppendLine($"        IsTrigger={coll.isTrigger}, Bounds={coll.bounds.size}");
                else if (comp is Light l)
                    sb.AppendLine($"        Type={l.type}, Range={l.range}, Color={l.color}, Intensity={l.intensity}");
                else if (comp is Camera cam)
                    sb.AppendLine($"        FOV={cam.fieldOfView}, Near={cam.nearClipPlane}, Far={cam.farClipPlane}");
                else if (comp is Renderer r)
                    sb.AppendLine($"        Material={(r.sharedMaterial ? r.sharedMaterial.name : "None")}");
            }

            return AgentToolResult.Ok(sb.ToString());
        }

        internal static AgentToolResult SetComponentProperty(JObject args)
        {
            string id = args["id"]?.Value<string>();
            string compTypeStr = args["componentType"]?.Value<string>();
            string propName = args["propertyName"]?.Value<string>();
            var valueToken = args["value"];

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(compTypeStr) || string.IsNullOrWhiteSpace(propName) || valueToken == null)
                return AgentToolResult.Fail("id, componentType, propertyName, and value are required.");

            var go = EditorToolDispatcher.FindInScene(SceneManager.GetActiveScene(), id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");

            // Find the component type safely
            System.Type type = BuiltinComponentFactory.ResolveType(compTypeStr);
            if (type == null)
            {
                // Fallback to basic string matching
                type = System.Type.GetType(compTypeStr) ?? System.Type.GetType($"UnityEngine.{compTypeStr}, UnityEngine");
            }
            if (type == null)
                return AgentToolResult.Fail($"Unknown component type '{compTypeStr}'.");

            var comp = go.GetComponent(type);
            if (comp == null)
                return AgentToolResult.Fail($"Object '{id}' does not have a {compTypeStr} component.");

            Undo.RecordObject(comp, "Agent Set Property");

            using (var so = new SerializedObject(comp))
            {
                var prop = so.FindProperty(propName);
                if (prop == null)
                    return AgentToolResult.Fail($"Property '{propName}' not found on {compTypeStr}.");

                try
                {
                    switch (prop.propertyType)
                    {
                        case SerializedPropertyType.Integer:
                            prop.intValue = valueToken.Value<int>();
                            break;
                        case SerializedPropertyType.Boolean:
                            prop.boolValue = valueToken.Value<bool>();
                            break;
                        case SerializedPropertyType.Float:
                            prop.floatValue = valueToken.Value<float>();
                            break;
                        case SerializedPropertyType.String:
                            prop.stringValue = valueToken.Value<string>();
                            break;
                        case SerializedPropertyType.Color:
                            if (ColorUtility.TryParseHtmlString(valueToken.Value<string>(), out Color c))
                                prop.colorValue = c;
                            else
                                return AgentToolResult.Fail($"Invalid color hex: {valueToken.Value<string>()}");
                            break;
                        case SerializedPropertyType.Vector2:
                            prop.vector2Value = new Vector2(valueToken["x"]?.Value<float>() ?? 0, valueToken["y"]?.Value<float>() ?? 0);
                            break;
                        case SerializedPropertyType.Vector3:
                            prop.vector3Value = new Vector3(valueToken["x"]?.Value<float>() ?? 0, valueToken["y"]?.Value<float>() ?? 0, valueToken["z"]?.Value<float>() ?? 0);
                            break;
                        default:
                            return AgentToolResult.Fail($"Unsupported property type: {prop.propertyType}");
                    }
                    so.ApplyModifiedProperties();
                    return AgentToolResult.Ok($"Set {compTypeStr}.{propName} on '{id}'.");
                }
                catch (System.Exception e)
                {
                    return AgentToolResult.Fail($"Failed to set property: {e.Message}");
                }
            }
        }
    }
}
