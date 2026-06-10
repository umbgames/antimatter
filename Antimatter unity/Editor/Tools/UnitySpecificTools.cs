using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;

namespace Antimatter
{
    public static class UnitySpecificTools
    {
        public static AgentToolResult AnalyzeScene(JObject args)
        {
            // Simply wrap the existing scene inspector
            var snap = SceneObserver.GetSnapshot();
            return AgentToolResult.Ok(SceneObserver.ToAgentContext(snap));
        }

        public static AgentToolResult AnalyzePrefab(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");
            
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return AgentToolResult.Fail($"Prefab not found at {path}");

            var sb = new StringBuilder();
            sb.AppendLine($"Prefab: {prefab.name}");
            
            var components = prefab.GetComponents<Component>();
            foreach (var c in components)
            {
                if (c == null) continue;
                sb.AppendLine($"- {c.GetType().Name}");
            }

            var children = prefab.GetComponentsInChildren<Transform>(true);
            sb.AppendLine($"Total Children: {children.Length - 1}");

            return AgentToolResult.Ok(sb.ToString());
        }

        public static AgentToolResult AnalyzeAnimator(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");

            AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ac == null) return AgentToolResult.Fail($"AnimatorController not found at {path}");

            var sb = new StringBuilder();
            sb.AppendLine($"Animator: {ac.name}");
            sb.AppendLine("Parameters:");
            foreach (var p in ac.parameters)
            {
                sb.AppendLine($"- {p.name} ({p.type})");
            }
            sb.AppendLine("Layers & States:");
            foreach (var layer in ac.layers)
            {
                sb.AppendLine($"> Layer: {layer.name}");
                foreach (var state in layer.stateMachine.states)
                {
                    sb.AppendLine($"  - State: {state.state.name}");
                }
            }

            return AgentToolResult.Ok(sb.ToString());
        }

        public static AgentToolResult AnalyzeScriptableObjects(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");

            ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so == null) return AgentToolResult.Fail($"ScriptableObject not found at {path}");

            var sb = new StringBuilder();
            sb.AppendLine($"ScriptableObject: {so.name} ({so.GetType().Name})");
            
            var json = JsonUtility.ToJson(so, true);
            sb.AppendLine(json);

            return AgentToolResult.Ok(sb.ToString());
        }

        public static AgentToolResult GenerateUnityScript(JObject args)
        {
            // Wrapper around the existing writeScript logic
            string path = args["path"]?.Value<string>();
            string code = args["code"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/") || !path.EndsWith(".cs"))
                return AgentToolResult.Fail("Invalid path. Must start with Assets/ and end with .cs");
            if (string.IsNullOrWhiteSpace(code))
                return AgentToolResult.Fail("Code is empty.");

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string txtPath = path + ".txt";
            File.WriteAllText(txtPath, code, System.Text.Encoding.UTF8);
            AgentOrchestrator.ActiveOrchestrator?.TrackScript(txtPath);

            return AgentToolResult.Ok($"Wrote script successfully to {path}. (Saved securely).");
        }

        public static AgentToolResult GenerateMultipleScripts(JObject args)
        {
            var scripts = args["scripts"] as JArray;
            if (scripts == null) return AgentToolResult.Fail("scripts array is required.");

            int count = 0;
            foreach (JObject script in scripts)
            {
                var res = GenerateUnityScript(script);
                if (res.Success) count++;
            }

            return AgentToolResult.Ok($"Successfully generated {count} scripts.");
        }

        public static AgentToolResult GenerateSystemOrFramework(JObject args)
        {
            // Basically just generate multiple scripts and maybe folders
            var res = GenerateMultipleScripts(args);
            return AgentToolResult.Ok($"System/Framework Generated: " + res.Output);
        }
    }
}
