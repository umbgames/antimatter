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
    }
}
