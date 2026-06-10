using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Antimatter
{
    public static class ContextManagementTools
    {
        // These tools primarily serve as a signal to the LLM that this information
        // has been explicitly pulled into the active context window. 
        // We return the content so the LLM remembers it.

        public static AgentToolResult AddFileToContext(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path)) return AgentToolResult.Fail("path is required.");
            if (!File.Exists(path)) return AgentToolResult.Fail($"File not found: {path}");

            string content = File.ReadAllText(path);
            return AgentToolResult.Ok($"[CONTEXT ADDED: {path}]\n\n{content}");
        }

        public static AgentToolResult AddFolderToContext(JObject args)
        {
            // Same as analyze_folder but explicitly marked for context memory
            var res = ProjectAnalysisTools.AnalyzeFolder(args);
            if (!res.Success) return res;
            
            return AgentToolResult.Ok($"[CONTEXT ADDED: Folder {args["folder"]?.Value<string>()}]\n\n{res.Output}");
        }

        public static AgentToolResult AddSceneToContext(JObject args)
        {
            var res = UnitySpecificTools.AnalyzeScene(args);
            if (!res.Success) return res;
            
            return AgentToolResult.Ok($"[CONTEXT ADDED: Active Scene]\n\n{res.Output}");
        }

        public static AgentToolResult AddEntireProjectToContext(JObject args)
        {
            var res = ProjectAnalysisTools.FullProjectScan(args);
            if (!res.Success) return res;
            
            return AgentToolResult.Ok($"[CONTEXT ADDED: Entire Project Manifest]\n\n{res.Output}");
        }
    }
}
