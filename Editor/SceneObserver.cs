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

        /// <summary>
        /// Computes a human-readable diff between two scene snapshots.
        /// </summary>
        public static string ComputeDiff(SceneSnapshot before, SceneSnapshot after)
        {
            if (before == null || after == null) return "No scene data to diff.";

            var beforeDict = before.objects.GroupBy(o => o.id).ToDictionary(g => g.Key, g => g.ToList());
            var afterDict = after.objects.GroupBy(o => o.id).ToDictionary(g => g.Key, g => g.ToList());

            var added = new List<string>();
            var removed = new List<string>();
            var changed = new List<string>();

            foreach (var kvp in afterDict)
            {
                if (!beforeDict.ContainsKey(kvp.Key))
                {
                    added.Add($"{kvp.Value.Count}x {kvp.Key} [{kvp.Value[0].kind}]");
                }
                else
                {
                    var bList = beforeDict[kvp.Key];
                    var aList = kvp.Value;
                    
                    if (aList.Count > bList.Count)
                        added.Add($"{aList.Count - bList.Count}x {kvp.Key} [{aList[0].kind}]");
                    else if (bList.Count > aList.Count)
                        removed.Add($"{bList.Count - aList.Count}x {kvp.Key} [{bList[0].kind}]");
                    else
                    {
                        var b = bList[0];
                        var a = aList[0];
                        if (Vector3.Distance(b.position, a.position) > 0.01f ||
                            Vector3.Distance(b.rotation, a.rotation) > 0.01f ||
                            Vector3.Distance(b.scale, a.scale) > 0.01f ||
                            string.Join(",", b.components) != string.Join(",", a.components) ||
                            b.parentId != a.parentId ||
                            b.materialName != a.materialName)
                        {
                            changed.Add(kvp.Key);
                        }
                    }
                }
            }

            foreach (var kvp in beforeDict)
            {
                if (!afterDict.ContainsKey(kvp.Key))
                    removed.Add($"{kvp.Value.Count}x {kvp.Key} [{kvp.Value[0].kind}]");
            }

            var sb = new StringBuilder();
            if (added.Count > 0) sb.AppendLine($"Added: {string.Join(", ", added)}");
            if (removed.Count > 0) sb.AppendLine($"Removed: {string.Join(", ", removed)}");
            if (changed.Count > 0) sb.AppendLine($"Changed: {string.Join(", ", changed)}");

            if (sb.Length == 0) return "No detectable scene changes.";
            return sb.ToString();
        }
    }
}
