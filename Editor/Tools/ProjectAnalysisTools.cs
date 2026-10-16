using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace Antimatter
{
    public static class ProjectAnalysisTools
    {
        public static AgentToolResult AnalyzeScript(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");
            if (!File.Exists(path)) return AgentToolResult.Fail($"File not found: {path}");

            // Quick parsing using regex to get class signatures
            string content = File.ReadAllText(path);
            var sb = new StringBuilder();
            sb.AppendLine($"Script: {path}");
            
            // Just return the content since it's an AI and reading the raw script is often best
            // To save tokens we could strip comments, but full context is usually required.
            return AgentToolResult.Ok(content);
        }

        public static AgentToolResult AnalyzeFolder(JObject args)
        {
            string folder = args["folder"]?.Value<string>() ?? "Assets";
            if (!Directory.Exists(folder)) return AgentToolResult.Fail($"Folder not found: {folder}");

            var files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith(".meta")).Select(f => Path.GetFileName(f)).ToList();
            var dirs = Directory.GetDirectories(folder, "*", SearchOption.TopDirectoryOnly)
                .Select(d => Path.GetFileName(d) + "/").ToList();

            var all = dirs.Concat(files).ToList();
            return AgentToolResult.Ok($"Contents of {folder}:\n" + string.Join("\n", all));
        }

        public static AgentToolResult AnalyzeProjectStructure(JObject args)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Project Structure (Assets/):");
            
            var dirs = Directory.GetDirectories("Assets", "*", SearchOption.AllDirectories);
            foreach (var d in dirs)
            {
                int depth = d.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
                if (depth <= 4) // Limit depth to avoid token explosion
                {
                    sb.AppendLine(d.Replace("\\", "/"));
                }
            }
            return AgentToolResult.Ok(sb.ToString());
        }

        public static AgentToolResult DependencyAnalysis(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");
            
            var dependencies = AssetDatabase.GetDependencies(path, false);
            return AgentToolResult.Ok($"Dependencies for {path}:\n" + string.Join("\n", dependencies));
        }


        public static AgentToolResult FullProjectScan(JObject args)
        {
            // Comprehensive scan of all scripts, prefabs, and scenes
            var scripts = AssetDatabase.FindAssets("t:MonoScript").Select(g => AssetDatabase.GUIDToAssetPath(g)).Where(p => p.StartsWith("Assets/"));
            var prefabs = AssetDatabase.FindAssets("t:Prefab").Select(g => AssetDatabase.GUIDToAssetPath(g)).Where(p => p.StartsWith("Assets/"));
            var scenes = AssetDatabase.FindAssets("t:Scene").Select(g => AssetDatabase.GUIDToAssetPath(g)).Where(p => p.StartsWith("Assets/"));

            var sb = new StringBuilder();
            sb.AppendLine("--- SCRIPTS ---");
            sb.AppendLine(string.Join("\n", scripts));
            sb.AppendLine("--- PREFABS ---");
            sb.AppendLine(string.Join("\n", prefabs));
            sb.AppendLine("--- SCENES ---");
            sb.AppendLine(string.Join("\n", scenes));

            return AgentToolResult.Ok(sb.ToString());
        }
    }
}
