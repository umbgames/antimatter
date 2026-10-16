using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;

namespace Antimatter
{
    internal static class PrefabToolImplementations
    {
        internal static AgentToolResult CreatePrefab(JObject args)
        {
            string id = args["id"]?.Value<string>();
            string path = args["path"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(path))
                return AgentToolResult.Fail("id and path are required.");

            if (!path.EndsWith(".prefab")) path += ".prefab";
            if (!path.StartsWith("Assets/")) path = "Assets/" + path;

            var go = EditorToolDispatcher.FindInScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), id);
            if (go == null)
                return AgentToolResult.Fail($"Object '{id}' not found.");

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            bool success;
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction, out success);

            if (success)
                return AgentToolResult.Ok($"Successfully created prefab at {path} from '{id}'.");
            else
                return AgentToolResult.Fail($"Failed to create prefab at {path}.");
        }

        internal static AgentToolResult InstantiatePrefab(JObject args)
        {
            string path = args["path"]?.Value<string>();
            string newId = args["newId"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(path))
                return AgentToolResult.Fail("path is required.");

            if (!path.EndsWith(".prefab")) path += ".prefab";
            if (!path.StartsWith("Assets/")) path = "Assets/" + path;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return AgentToolResult.Fail($"No prefab found at {path}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (!string.IsNullOrWhiteSpace(newId))
                instance.name = newId;

            float x = args["x"]?.Value<float>() ?? 0;
            float y = args["y"]?.Value<float>() ?? 0;
            float z = args["z"]?.Value<float>() ?? 0;
            instance.transform.position = new Vector3(x, y, z);

            Undo.RegisterCreatedObjectUndo(instance, "Agent Instantiate Prefab");
            return AgentToolResult.Ok($"Instantiated prefab '{path}' as '{instance.name}' at {instance.transform.position}.");
        }

        internal static AgentToolResult ListPrefabs(JObject args)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            var paths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).ToList();
            
            if (paths.Count == 0)
                return AgentToolResult.Ok("No prefabs found in project.");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Found {paths.Count} prefabs:");
            foreach (var p in paths)
                sb.AppendLine($"- {p}");

            return AgentToolResult.Ok(sb.ToString());
        }
    }
}
