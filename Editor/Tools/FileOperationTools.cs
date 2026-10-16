using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using System;

namespace Antimatter
{
    public static class FileOperationTools
    {
        public static AgentToolResult ReadFile(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");
            if (!File.Exists(path)) return AgentToolResult.Fail($"File not found: {path}");

            string content = File.ReadAllText(path);
            return AgentToolResult.Ok(content);
        }

        public static AgentToolResult ReadLargeFile(JObject args)
        {
            string path = args["path"]?.Value<string>();
            int maxLines = args["maxLines"]?.Value<int>() ?? 100;
            int startLine = args["startLine"]?.Value<int>() ?? 0;

            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");
            if (!File.Exists(path)) return AgentToolResult.Fail($"File not found: {path}");

            var lines = File.ReadLines(path).Skip(startLine).Take(maxLines).ToList();
            return AgentToolResult.Ok(string.Join("\n", lines));
        }

        public static AgentToolResult SearchFiles(JObject args)
        {
            string folder = args["folder"]?.Value<string>() ?? "Assets";
            string pattern = args["pattern"]?.Value<string>() ?? "*.*";
            string regexKeyword = args["regexKeyword"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(regexKeyword)) return AgentToolResult.Fail("regexKeyword is required.");
            if (!Directory.Exists(folder)) return AgentToolResult.Fail($"Folder not found: {folder}");

            var files = Directory.GetFiles(folder, pattern, SearchOption.AllDirectories);
            var results = new List<string>();
            Regex r = new Regex(regexKeyword, RegexOptions.IgnoreCase);

            foreach (var f in files)
            {
                if (f.EndsWith(".meta") || f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".asset")) continue;

                try
                {
                    string content = File.ReadAllText(f);
                    if (r.IsMatch(content))
                    {
                        results.Add(f.Replace("\\", "/"));
                    }
                }
                catch { /* Ignore unreadable */ }
            }

            return AgentToolResult.Ok($"Found {results.Count} matches in: \n" + string.Join("\n", results));
        }

        public static AgentToolResult SearchEntireProject(JObject args)
        {
            string keyword = args["keyword"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(keyword)) return AgentToolResult.Fail("keyword is required.");

            JObject newArgs = new JObject();
            newArgs["folder"] = "Assets";
            newArgs["pattern"] = "*.*";
            newArgs["regexKeyword"] = Regex.Escape(keyword);
            return SearchFiles(newArgs);
        }

        public static AgentToolResult CreateFile(JObject args)
        {
            string path = args["path"]?.Value<string>();
            string content = args["content"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");
            if (string.IsNullOrWhiteSpace(content)) return AgentToolResult.Fail("content is required.");

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(path);

            return AgentToolResult.Ok($"File created at {path}");
        }

        public static AgentToolResult EditFile(JObject args)
        {
            string path = args["path"]?.Value<string>();
            string search = args["search"]?.Value<string>();
            string replace = args["replace"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");
            if (!File.Exists(path)) return AgentToolResult.Fail($"File not found: {path}");

            string content = File.ReadAllText(path);
            if (!string.IsNullOrEmpty(search))
            {
                if (!content.Contains(search))
                    return AgentToolResult.Fail("Search string not found in file.");
                content = content.Replace(search, replace ?? "");
            }
            else
            {
                // Full replace if no search specified
                content = args["content"]?.Value<string>() ?? content;
            }

            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(path);

            return AgentToolResult.Ok($"File edited at {path}");
        }

        public static AgentToolResult DeleteFile(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");

            if (!File.Exists(path)) return AgentToolResult.Fail($"File not found: {path}");

            AssetDatabase.DeleteAsset(path);
            return AgentToolResult.Ok($"Deleted file {path}");
        }

        public static AgentToolResult RenameFile(JObject args)
        {
            string path = args["path"]?.Value<string>();
            string newName = args["newName"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(newName)) 
                return AgentToolResult.Fail("path and newName are required.");

            if (!File.Exists(path)) return AgentToolResult.Fail($"File not found: {path}");

            string error = AssetDatabase.RenameAsset(path, newName);
            if (!string.IsNullOrEmpty(error)) return AgentToolResult.Fail($"Rename failed: {error}");

            return AgentToolResult.Ok($"Renamed to {newName}");
        }
    }
}
