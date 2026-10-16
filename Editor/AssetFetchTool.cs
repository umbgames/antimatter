using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Antimatter
{
    public static class AssetFetchTool
    {
        public static AgentToolResult ToolSearchAssets(JObject args)
        {
            string keyword = args["keyword"]?.Value<string>()?.ToLower() ?? "";
            string type = args["type"]?.Value<string>()?.ToLower() ?? "model"; // model, texture, prefab, etc
            int maxResults = args["maxResults"]?.Value<int>() ?? 10;

            if (string.IsNullOrEmpty(keyword))
                return AgentToolResult.Fail("Keyword is required.");

            string[] searchFolders = new string[] { "Assets" };
            
            // Map types to Unity filter strings
            string filter = "";
            if (type == "model") filter = "t:Model";
            else if (type == "texture") filter = "t:Texture2D";
            else if (type == "prefab") filter = "t:Prefab";
            else if (type == "material") filter = "t:Material";
            else if (type == "audioclip") filter = "t:AudioClip";

            string[] guids = AssetDatabase.FindAssets($"{keyword} {filter}", searchFolders);
            
            var results = new List<string>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // Exclude Antimatter internal files to avoid polluting game data
                if (path.Contains("/Antimatter/"))
                    continue;

                results.Add(path);
                if (results.Count >= maxResults) break;
            }

            if (results.Count == 0)
                return AgentToolResult.Ok($"No {type} assets found matching '{keyword}'.");

            return AgentToolResult.Ok($"Found {results.Count} assets matching '{keyword}':\n" + string.Join("\n", results));
        }

        public static AgentToolResult ToolSearchMarketplace(JObject args)
        {
            string keyword = args["keyword"]?.Value<string>()?.ToLower() ?? "";
            
            if (string.IsNullOrEmpty(keyword))
                return AgentToolResult.Fail("Keyword is required.");

            string[] searchFolders = new string[] { 
                "Assets/Antimatter unity/Assets/Marketplace", 
                "Assets/Antimatter unity/Editor/Tools/Marketplace" 
            };

            var results = new List<string>();

            foreach (var folder in searchFolders)
            {
                if (!Directory.Exists(folder)) continue;

                string[] metadataFiles = Directory.GetFiles(folder, "metadata.json", SearchOption.AllDirectories);
                foreach (var file in metadataFiles)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var data = JObject.Parse(json);
                        string title = data["title"]?.Value<string>() ?? "";
                        string desc = data["description"]?.Value<string>() ?? "";
                        string filename = data["filename"]?.Value<string>() ?? "";

                        if (title.ToLower().Contains(keyword) || desc.ToLower().Contains(keyword))
                        {
                            string dir = Path.GetDirectoryName(file).Replace("\\", "/");
                            string mainFilePath = string.IsNullOrEmpty(filename) ? dir : Path.Combine(dir, filename).Replace("\\", "/");
                            results.Add($"- Title: {title}\n  Main File: {mainFilePath}\n  Description: {desc}");
                        }
                    }
                    catch { }
                }
            }

            if (results.Count == 0)
                return AgentToolResult.Ok($"No downloaded marketplace assets found matching '{keyword}'.");

            return AgentToolResult.Ok($"Found {results.Count} marketplace assets matching '{keyword}':\n\n" + string.Join("\n\n", results));
        }
    }
}
