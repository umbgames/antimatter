using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Antimatter
{
    /// <summary>
    /// Builds a structured snapshot of the active scene for the agent's perception.
    /// </summary>
    public static class SceneObserver
    {
        [Serializable]
        public class SceneSnapshot
        {
            public string sceneName;
            public string scenePath;
            public bool isDirty;
            public int objectCount;
            public List<ObjectSnapshot> objects = new List<ObjectSnapshot>();
        }

        [Serializable]
        public class ObjectSnapshot
        {
            public string id;
            public string kind;       // Primitive, Empty, Builtin, Terrain, etc.
            public Vector3 position;
            public Vector3 rotation;
            public Vector3 scale;
            public List<string> components = new List<string>();
            public string parentId;
            public List<string> children = new List<string>();
            public string materialName;
        }

        public static SceneSnapshot GetSnapshot()
        {
            var scene = SceneManager.GetActiveScene();
            var snap = new SceneSnapshot
            {
                sceneName = scene.IsValid() ? scene.name : "(none)",
                scenePath = scene.IsValid() ? scene.path : "",
                isDirty = scene.IsValid() && scene.isDirty
            };

            if (!scene.IsValid()) return snap;

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                CollectObjects(root.transform, null, snap.objects);
            }

            snap.objectCount = snap.objects.Count;
            return snap;
        }

        private static void CollectObjects(Transform t, string parentId, List<ObjectSnapshot> list)
        {
            var go = t.gameObject;
            var obj = new ObjectSnapshot
            {
                id = go.name,
                position = t.position,
                rotation = t.eulerAngles,
                scale = t.localScale,
                parentId = parentId ?? ""
            };

            // Determine kind
            if (go.GetComponent<Terrain>() != null)
                obj.kind = "Terrain";
            else if (go.GetComponent<Camera>() != null)
                obj.kind = "Builtin(Camera)";
            else if (go.GetComponent<Light>() != null)
                obj.kind = $"Builtin(Light:{go.GetComponent<Light>().type})";
            else if (go.GetComponent<MeshFilter>() != null)
                obj.kind = "Primitive";
            else
                obj.kind = "Empty";

            // Components
            var components = go.GetComponents<Component>();
            foreach (var c in components)
            {
                if (c == null) continue;
                if (c is Transform) continue;
                obj.components.Add(c.GetType().Name);
            }

            // Material
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                obj.materialName = renderer.sharedMaterial.name;

            // Children
            for (int i = 0; i < t.childCount; i++)
                obj.children.Add(t.GetChild(i).name);

            list.Add(obj);

            // Recurse
            for (int i = 0; i < t.childCount; i++)
                CollectObjects(t.GetChild(i), go.name, list);
        }

        /// <summary>
        /// Produces a compact human-readable summary suitable for injecting into an agent system prompt.
        /// </summary>
        public static string ToAgentContext(SceneSnapshot snap)
        {
            if (snap == null) return "No scene loaded.";

            var sb = new StringBuilder();
            sb.AppendLine($"ACTIVE SCENE: {snap.sceneName} ({snap.objectCount} objects){(snap.isDirty ? " [unsaved]" : "")}");

            if (snap.objects.Count == 0)
            {
                sb.AppendLine("  (empty scene)");
                return sb.ToString();
            }

            // Show up to 40 objects in compact format (reduced from 80 to save tokens)
            int shown = 0;
            foreach (var o in snap.objects)
            {
                if (shown >= 40)
                {
                    sb.AppendLine($"  ... and {snap.objects.Count - 40} more objects");
                    break;
                }
                string indent = string.IsNullOrEmpty(o.parentId) ? "  " : "    ";
                sb.Append($"{indent}{o.id} [{o.kind}] pos=({o.position.x:F1},{o.position.y:F1},{o.position.z:F1})");
                sb.Append($" scale=({o.scale.x:F1},{o.scale.y:F1},{o.scale.z:F1})");

                if (o.components.Count > 0)
                    sb.Append($" components=[{string.Join(",", o.components)}]");
                if (!string.IsNullOrEmpty(o.materialName))
                    sb.Append($" mat={o.materialName}");

                sb.AppendLine();
                shown++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns a single-line summary (name + count) to save tokens in the initial prompt.
        /// </summary>
        public static string GetMinimalSummary(SceneSnapshot snap)
        {
            if (snap == null) return "No scene loaded.";
            return $"ACTIVE SCENE: {snap.sceneName} ({snap.objectCount} objects). Use 'inspectScene' tool for details.";
        }

        /// <summary>
        /// Produces JSON representation for structured tool results.
        /// </summary>
        public static string ToJson(SceneSnapshot snap)
        {
            return JsonUtility.ToJson(snap, true);
        }
    }
}
