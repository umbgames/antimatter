using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Reflection;

public static class GameGenerationExecutor_Create
{
    private const string SessionKeyJson = "AI_GAMEGEN_JSON";
    private const string SessionKeyPending = "AI_GAMEGEN_PENDING";

    public static void Execute(string json)
    {
        if (EditorApplication.isPlaying)
            throw new Exception("Stop Play Mode before generating.");

        ValidateOnly(json);

        var gen = JsonUtility.FromJson<GameGeneration>(json);
        ValidatePlan(gen); // Apply auto-corrections to this instance before generation

        SessionState.SetString(SessionKeyJson, JsonUtility.ToJson(gen));
        SessionState.SetBool(SessionKeyPending, true);

        // Generate scripts (may trigger domain reload)
        ScriptGenerator.GenerateScripts(gen);

        // Schedule build; bootstrap will resume if domain reload occurs
        ScheduleBuildInternal();
    }

    public static void ScheduleBuildFromBootstrap()
    {
        ScheduleBuildInternal();
    }

    public static void ValidateOnly(string json)
    {
        var gen = JsonUtility.FromJson<GameGeneration>(json);
        ValidatePlan(gen);
    }

    private static void ScheduleBuildInternal()
    {
        EditorApplication.update -= UpdateBuildLoop;
        EditorApplication.update += UpdateBuildLoop;

        EditorApplication.delayCall -= TryBuildIfReady;
        EditorApplication.delayCall += TryBuildIfReady;
    }

    private static void UpdateBuildLoop()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        EditorApplication.update -= UpdateBuildLoop;
        TryBuildIfReady();
    }

    private static void TryBuildIfReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            ScheduleBuildInternal();
            return;
        }

        if (!SessionState.GetBool(SessionKeyPending, false))
            return;

        string json = SessionState.GetString(SessionKeyJson, null);
        if (string.IsNullOrEmpty(json))
        {
            SessionState.SetBool(SessionKeyPending, false);
            return;
        }

        var gen = JsonUtility.FromJson<GameGeneration>(json);
        ValidatePlan(gen);

        // Build asset maps (materials, terrain data)
        var matMap = PlanAssetBuilder.BuildMaterials(gen);
        var terrainMap = PlanAssetBuilder.BuildTerrainData(gen);

        // Scripts must resolve now
        MonoScriptResolver.RebuildIndex();

        SessionState.SetBool(SessionKeyPending, false);

        BuildAllScenes(gen, matMap, terrainMap);

        Debug.Log("AI Game Generator: Create build complete.");
    }

    private static void BuildAllScenes(
        GameGeneration gen,
        Dictionary<string, Material> matMap,
        Dictionary<string, TerrainData> terrainMap)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        foreach (var sceneDef in gen.scenes)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            scene.name = sceneDef.name;

            SceneManager.SetActiveScene(scene);

            bool ensure = gen.settings?.ensureCameraAndLight ?? true;
            if (sceneDef.settings != null && sceneDef.settings.ensureCameraAndLight.HasValue)
                ensure = sceneDef.settings.ensureCameraAndLight.Value;

            // Upsert all objects into fresh scene
            ApplySceneObjects(scene, sceneDef, matMap, terrainMap, isEditMode:false);

            // Ensure camera/light unless disabled or user already included
            SceneDefaults.EnsureCameraAndLightIfNeeded(scene, ensure);

            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{sceneDef.name}.unity");
        }

        AssetDatabase.SaveAssets();
    }

    private static void ApplySceneObjects(
        Scene scene,
        SceneDefinition sceneDef,
        Dictionary<string, Material> matMap,
        Dictionary<string, TerrainData> terrainMap,
        bool isEditMode)
    {
        // First pass: create/upsert all objects as root objects
        // Second pass: apply parenting

        var created = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        foreach (var objDef in sceneDef.objects)
        {
            var go = FindByIdInScene(scene, objDef.id);
            if (go == null)
            {
                go = CreateFromDefinition(objDef, terrainMap);
                go.name = objDef.id;
                SceneManager.MoveGameObjectToScene(go, scene);
            }

            ApplyTransform(go, objDef, isEditMode);
            ApplyComponents(go, objDef);
            ApplyMaterialIfAny(go, objDef, matMap);

            created[objDef.id] = go;
        }

        // Parenting pass (optional)
        foreach (var objDef in sceneDef.objects)
        {
            if (string.IsNullOrWhiteSpace(objDef.parentId)) continue;

            if (!created.TryGetValue(objDef.id, out var child)) continue;

            var parent = FindByIdInScene(scene, objDef.parentId);
            if (parent != null && child.transform.parent != parent.transform)
                child.transform.SetParent(parent.transform, worldPositionStays:true);
        }
    }

    private static void ApplyTransform(GameObject go, ObjectDefinition def, bool isEditMode)
    {
        go.transform.position = def.position;

        if (def.rotationEuler != Vector3.zero)
            go.transform.rotation = Quaternion.Euler(def.rotationEuler);

        // Deterministic protection: don’t trash objects with zero scale in edit mode
        if (!isEditMode || def.scale != Vector3.zero)
            go.transform.localScale = def.scale == Vector3.zero ? Vector3.one : def.scale;
    }

    private static GameObject FindByIdInScene(Scene scene, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var found = FindInHierarchyByName(roots[i].transform, id);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static Transform FindInHierarchyByName(Transform root, string name)
    {
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            var found = FindInHierarchyByName(child, name);
            if (found != null) return found;
        }

        return null;
    }

    private static GameObject CreateFromDefinition(ObjectDefinition def, Dictionary<string, TerrainData> terrainMap)
    {
        string kind = string.IsNullOrWhiteSpace(def.kind) ? "Primitive" : def.kind;

        GameObject go;
        switch (kind)
        {
            case "Empty":
                go = new GameObject(def.id);
                break;

            case "Builtin":
                go = CreateBuiltin(def);
                break;

            case "Terrain":
                go = CreateTerrain(def, terrainMap);
                break;

            case "Primitive":
            default:
                go = CreatePrimitive(def);
                break;
        }

        go.name = def.id;
        return go;
    }

    private static GameObject CreatePrimitive(ObjectDefinition def)
    {
        // Default primitive if omitted
        string p = string.IsNullOrWhiteSpace(def.primitiveType) ? "Cube" : def.primitiveType;

        PrimitiveType pt = p switch
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

    private static GameObject CreateBuiltin(ObjectDefinition def)
    {
        string t = def.builtinType ?? "";

        if (t == "Camera")
        {
            var go = new GameObject(def.id);
            go.tag = "MainCamera";
            go.AddComponent<Camera>();

            if (UnityEngine.Object.FindObjectOfType<AudioListener>() == null)
                go.AddComponent<AudioListener>();

            return go;
        }

        if (t == "DirectionalLight")
        {
            var go = new GameObject(def.id);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1f;
            return go;
        }

        if (t == "PointLight")
        {
            var go = new GameObject(def.id);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = 1f;
            l.range = 10f;
            return go;
        }

        if (t == "SpotLight")
        {
            var go = new GameObject(def.id);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.intensity = 1f;
            l.range = 20f;
            l.spotAngle = 30f;
            return go;
        }

        return new GameObject(def.id);
    }

    private static GameObject CreateTerrain(ObjectDefinition def, Dictionary<string, TerrainData> terrainMap)
    {
        TerrainData td = null;
        if (!string.IsNullOrWhiteSpace(def.terrainDataId))
            terrainMap?.TryGetValue(def.terrainDataId, out td);

        if (td == null) td = new TerrainData();
        return Terrain.CreateTerrainGameObject(td);
    }

    private static void ApplyMaterialIfAny(GameObject go, ObjectDefinition def, Dictionary<string, Material> matMap)
    {
        if (string.IsNullOrWhiteSpace(def.materialId)) return;
        if (matMap == null || !matMap.TryGetValue(def.materialId, out var mat)) return;

        var r = go.GetComponent<Renderer>();
        if (r != null)
            r.sharedMaterial = mat;
    }

    private static void ApplyComponents(GameObject go, ObjectDefinition def)
    {
        if (def.components == null) return;

        // Remove & re-add only components referenced (deterministic)
        foreach (var compDef in def.components)
        {
            var type = ResolveComponentType(def, compDef, out bool isBuiltin);
            if (type == null) continue;

            var existing = go.GetComponents(type);
            for (int i = 0; i < existing.Length; i++)
                UnityEngine.Object.DestroyImmediate(existing[i]);
        }

        foreach (var compDef in def.components)
        {
            var type = ResolveComponentType(def, compDef, out bool isBuiltin);
            if (type == null)
            {
                UnityEngine.Debug.LogWarning($"[Agent] Component type missing/unknown on '{def.id}'. Skipping.");
                continue;
            }

            Component component;

            if (isBuiltin)
            {
                component = BuiltinComponentFactory.Add(go, compDef.builtinComponent);
                if (component == null)
                    throw new PlanValidationException($"Unsupported builtinComponent '{compDef.builtinComponent}' on '{def.id}'.");
            }
            else
            {
                component = go.AddComponent(type);
            }

            if (compDef.properties == null) continue;
            foreach (var p in compDef.properties)
                SetMemberValue(component, type, p);
        }
    }

    private static Type ResolveComponentType(ObjectDefinition obj, ComponentDefinition comp, out bool isBuiltin)
    {
        isBuiltin = false;

        // Built-in component whitelist path
        if (!string.IsNullOrWhiteSpace(comp.builtinComponent))
        {
            isBuiltin = true;
            return BuiltinComponentFactory.ResolveType(comp.builtinComponent);
        }

        // MonoBehaviour script class path
        string scriptName = ResolveMissingScriptName(obj, comp);
        if (string.IsNullOrWhiteSpace(scriptName))
            return null;

        // Graceful fallback: If AI put a builtin component name inside the 'script' property
        Type builtInType = BuiltinComponentFactory.ResolveType(scriptName);
        if (builtInType != null)
        {
            isBuiltin = true;
            comp.builtinComponent = scriptName;
            return builtInType;
        }

        var ms = MonoScriptResolver.Find(scriptName);
        if (ms == null) return null;

        return ms.GetClass();
    }

    private static string ResolveMissingScriptName(ObjectDefinition obj, ComponentDefinition comp)
    {
        if (comp != null && !string.IsNullOrWhiteSpace(comp.script))
            return comp.script;

        if (!string.IsNullOrWhiteSpace(obj?.id) &&
            obj.id.Equals("Player", StringComparison.OrdinalIgnoreCase))
            return "PlayerController";

        if (!string.IsNullOrWhiteSpace(obj?.id))
            return obj.id + "Controller";

        return null;
    }

    private static void SetMemberValue(Component component, Type type, PropertyValue p)
    {
        if (component == null || p == null) return;
        if (string.IsNullOrWhiteSpace(p.name) || string.IsNullOrWhiteSpace(p.type))
            return;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var field = type.GetField(p.name, flags);
        if (field != null)
        {
            object v = ConvertTypedValue(field.FieldType, p);
            if (v != null) field.SetValue(component, v);
            return;
        }

        var prop = type.GetProperty(p.name, flags);
        if (prop != null && prop.CanWrite)
        {
            object v = ConvertTypedValue(prop.PropertyType, p);
            if (v != null) prop.SetValue(component, v);
        }
    }

    private static object ConvertTypedValue(Type targetType, PropertyValue p)
    {
        // Extended types: color, vector3
        switch (p.type)
        {
            case "float":
                if (targetType == typeof(float)) return p.floatValue;
                if (targetType == typeof(int)) return (int)p.floatValue;
                if (targetType == typeof(string)) return p.floatValue.ToString();
                break;

            case "int":
                if (targetType == typeof(int)) return p.intValue;
                if (targetType == typeof(float)) return (float)p.intValue;
                if (targetType == typeof(string)) return p.intValue.ToString();
                break;

            case "bool":
                if (targetType == typeof(bool)) return p.boolValue;
                if (targetType == typeof(string)) return p.boolValue.ToString();
                break;

            case "string":
                if (targetType == typeof(string)) return p.stringValue ?? "";
                break;

            case "color":
                if (targetType == typeof(Color)) return p.colorValue;
                break;

            case "vector3":
                if (targetType == typeof(Vector3)) return p.vector3Value;
                break;
        }

        return null;
    }

    private static void ValidatePlan(GameGeneration gen)
    {
        if (gen == null) throw new PlanValidationException("Invalid JSON: could not parse.");
        if (gen.scenes == null || gen.scenes.Count == 0) throw new PlanValidationException("Invalid plan: no scenes.");
        if (gen.flow == null) throw new PlanValidationException("Invalid plan: missing flow.");

        if (gen.settings == null)
            gen.settings = new GenerationSettings { ensureCameraAndLight = true };

        // scripts: allow empty, validate if present
        if (gen.scripts != null)
        {
            foreach (var s in gen.scripts)
            {
                // Force script name to match the C# class name exactly to prevent Unity component attach failures
                var match = System.Text.RegularExpressions.Regex.Match(s.code ?? "", @"class\s+([A-Za-z0-9_]+)");
                if (match.Success)
                {
                    s.name = match.Groups[1].Value;
                }
                else if (string.IsNullOrWhiteSpace(s.name))
                {
                    s.name = "GeneratedScript_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                }

                if (string.IsNullOrWhiteSpace(s.path) || !s.path.StartsWith("Assets/"))
                    s.path = $"Assets/Scripts/{s.name}.cs";

                if (string.IsNullOrWhiteSpace(s.code))
                {
                    UnityEngine.Debug.LogWarning($"[Agent] Script '{s.name}' missing code. Auto-generating a blank MonoBehaviour shell.");
                    s.code = $"using UnityEngine;\n\npublic class {s.name} : MonoBehaviour\n{{\n}}";
                }
            }
        }

        foreach (var scene in gen.scenes)
        {
            if (string.IsNullOrWhiteSpace(scene.name))
                throw new PlanValidationException("Scene name missing.");

            if (scene.objects == null) scene.objects = new List<ObjectDefinition>();

            foreach (var o in scene.objects)
                ValidateSceneObject(o, gen);
        }
    }

    private static void ValidateSceneObject(ObjectDefinition o, GameGeneration gen)
    {
        if (string.IsNullOrWhiteSpace(o.id))
            throw new PlanValidationException("Object id missing.");

        string kind = string.IsNullOrWhiteSpace(o.kind) ? "Primitive" : o.kind;

        if (kind != "Primitive" && kind != "Empty" && kind != "Builtin" && kind != "Terrain")
        {
            UnityEngine.Debug.LogWarning($"[Agent] Unknown object kind '{kind}' for object '{o.id}'. Defaulting to 'Primitive'.");
            kind = "Primitive";
            o.kind = "Primitive";
        }

        if (kind == "Primitive")
        {
            if (string.IsNullOrWhiteSpace(o.primitiveType))
                o.primitiveType = "Cube";
        }

        if (kind == "Builtin")
        {
            if (string.IsNullOrWhiteSpace(o.builtinType))
                throw new PlanValidationException($"Builtin missing builtinType on '{o.id}'.");
        }

        if (kind == "Terrain")
        {
            // If terrainDataId is empty, still allowed (creates default TerrainData),
            // but recommended to reference an asset.
        }

        // Material reference optional; if present, must exist in assets plan
        if (!string.IsNullOrWhiteSpace(o.materialId))
        {
            bool ok = gen.assets?.materials != null && gen.assets.materials.Exists(m => m.id == o.materialId);
            if (!ok)
                throw new PlanValidationException($"materialId '{o.materialId}' referenced by '{o.id}' but not defined in assets.materials.");
        }
    }
}
