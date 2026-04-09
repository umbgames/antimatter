using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Antimatter
{
    
    public static class GameGenerationExecutorEdit
    {
        private const string SessionKeyJson = "AI_GAMEGEN_EDIT_JSON";
        private const string SessionKeyPending = "AI_GAMEGEN_EDIT_PENDING";
    
        public static void Execute(string json)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("Stop Play Mode before running generator.");
    
            ValidateOnly(json);
    
            SessionState.SetString(SessionKeyJson, json);
            SessionState.SetBool(SessionKeyPending, true);
    
            var gen = JsonUtility.FromJson<GameGeneration>(json);
    
            // scripts (may trigger reload)
            ScriptGenerator.GenerateScripts(gen);
    
            ScheduleApplyInternal();
        }
    
        public static void ScheduleApplyFromBootstrap()
        {
            ScheduleApplyInternal();
        }
    
        public static void ValidateOnly(string json)
        {
            var gen = JsonUtility.FromJson<GameGeneration>(json);
            ValidatePlan(gen);
        }
    
        private static void ScheduleApplyInternal()
        {
            EditorApplication.update -= UpdateApplyLoop;
            EditorApplication.update += UpdateApplyLoop;
    
            EditorApplication.delayCall -= TryApplyIfReady;
            EditorApplication.delayCall += TryApplyIfReady;
        }
    
        private static void UpdateApplyLoop()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
    
            EditorApplication.update -= UpdateApplyLoop;
            TryApplyIfReady();
        }
    
        private static void TryApplyIfReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleApplyInternal();
                return;
            }
    
            if (!SessionState.GetBool(SessionKeyPending, false))
                return;
    
            var json = SessionState.GetString(SessionKeyJson, null);
            if (string.IsNullOrEmpty(json))
            {
                SessionState.SetBool(SessionKeyPending, false);
                return;
            }
    
            var gen = JsonUtility.FromJson<GameGeneration>(json);
            ValidatePlan(gen);
    
            var matMap = PlanAssetBuilder.BuildMaterials(gen);
            var terrainMap = PlanAssetBuilder.BuildTerrainData(gen);
    
            MonoScriptResolver.RebuildIndex();
    
            SessionState.SetBool(SessionKeyPending, false);
    
            ApplyEditsToActiveScene(gen, matMap, terrainMap);
    
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
    
            Debug.Log("AI Game Generator: Edit apply complete (no wipe).");
        }
    
        private static void ApplyEditsToActiveScene(
            GameGeneration gen,
            Dictionary<string, Material> matMap,
            Dictionary<string, TerrainData> terrainMap)
        {
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid())
                throw new Exception("No active scene is open.");
    
            // Match scene by name if possible, else use first
            SceneDefinition target = null;
            for (int i = 0; i < gen.scenes.Count; i++)
            {
                if (gen.scenes[i].name == active.name)
                {
                    target = gen.scenes[i];
                    break;
                }
            }
            target ??= gen.scenes[0];
    
            bool ensure = gen.settings?.ensureCameraAndLight ?? true;
            if (target.settings != null && target.settings.ensureCameraAndLight.HasValue)
                ensure = target.settings.ensureCameraAndLight.Value;
    
            // Upsert only objects mentioned (never delete others)
            ApplySceneObjects(active, target, matMap, terrainMap);
    
            // Ensure camera/light unless disabled
            SceneDefaults.EnsureCameraAndLightIfNeeded(active, ensure);
        }
    
        private static void ApplySceneObjects(
            Scene scene,
            SceneDefinition sceneDef,
            Dictionary<string, Material> matMap,
            Dictionary<string, TerrainData> terrainMap)
        {
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
    
                ApplyTransform(go, objDef, isEditMode:true);
                ApplyComponents(go, objDef);
                ApplyMaterialIfAny(go, objDef, matMap);
    
                created[objDef.id] = go;
            }
    
            // Parenting pass
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
    
            if (!isEditMode || def.scale != Vector3.zero)
                go.transform.localScale = def.scale == Vector3.zero ? go.transform.localScale : def.scale;
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
                    throw new PlanValidationException($"Component type missing/unknown on '{def.id}'.");
    
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
    
            if (!string.IsNullOrWhiteSpace(comp.builtinComponent))
            {
                isBuiltin = true;
                return BuiltinComponentFactory.ResolveType(comp.builtinComponent);
            }
    
            string scriptName = ResolveMissingScriptName(obj, comp);
            if (string.IsNullOrWhiteSpace(scriptName))
                return null;
    
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
    
            if (gen.scripts != null)
            {
                foreach (var s in gen.scripts)
                {
                    if (string.IsNullOrWhiteSpace(s.path) || !s.path.StartsWith("Assets/"))
                        throw new PlanValidationException($"Invalid script path (must be under Assets/): {s.path}");
                    if (string.IsNullOrWhiteSpace(s.name))
                        throw new PlanValidationException("Invalid script: name missing.");
                    if (string.IsNullOrWhiteSpace(s.code))
                        throw new PlanValidationException($"Invalid script '{s.name}': code missing.");
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
                throw new PlanValidationException($"Unsupported object kind '{kind}'.");
    
            if (kind == "Primitive" && string.IsNullOrWhiteSpace(o.primitiveType))
                o.primitiveType = "Cube";
    
            if (kind == "Builtin" && string.IsNullOrWhiteSpace(o.builtinType))
                throw new PlanValidationException($"Builtin missing builtinType on '{o.id}'.");
    
            if (!string.IsNullOrWhiteSpace(o.materialId))
            {
                bool ok = gen.assets?.materials != null && gen.assets.materials.Exists(m => m.id == o.materialId);
                if (!ok)
                    throw new PlanValidationException($"materialId '{o.materialId}' referenced by '{o.id}' but not defined in assets.materials.");
            }
        }
    }
}
