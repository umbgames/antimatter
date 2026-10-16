using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Antimatter
{
    public static class AgentExecutionTools
    {
        public static AgentToolResult ExecuteCommand(JObject args)
        {
            string command = args["command"]?.Value<string>();
            string workingDirectory = args["workingDirectory"]?.Value<string>() ?? Directory.GetCurrentDirectory();

            if (string.IsNullOrWhiteSpace(command)) 
                return AgentToolResult.Fail("command is required.");

            // Display confirmation dialog
            bool isApproved = EditorUtility.DisplayDialog(
                "Agent Shell Command Approval",
                $"The Antimatter Agent wants to execute the following shell command:\n\n{command}\n\nWorking Directory: {workingDirectory}\n\nDo you want to allow this?",
                "Execute",
                "Deny"
            );

            if (!isApproved)
            {
                return AgentToolResult.Fail("User denied execution of the command.");
            }

            try
            {
                var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null) return AgentToolResult.Fail("Failed to start process.");
                    
                    // Wait for 15 seconds max to prevent editor freezes
                    process.WaitForExit(15000);
                    
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    
                    if (process.ExitCode != 0)
                        return AgentToolResult.Fail($"Command failed with exit code {process.ExitCode}.\nError: {error}");

                    return AgentToolResult.Ok($"Command output:\n{output}");
                }
            }
            catch (System.Exception ex)
            {
                return AgentToolResult.Fail($"Execution error: {ex.Message}");
            }
        }

        public static AgentToolResult ExecuteBuild(JObject args)
        {
            // Trigger a default build
            string buildPath = "Builds/GeneratedBuild";
            Directory.CreateDirectory("Builds");

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                return AgentToolResult.Fail("No enabled scenes in Build Settings.");

            var report = BuildPipeline.BuildPlayer(scenes, buildPath, EditorUserBuildSettings.activeBuildTarget, BuildOptions.None);
            
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                return AgentToolResult.Ok($"Build succeeded. Time: {report.summary.totalTime}");
            }
            else
            {
                return AgentToolResult.Fail($"Build failed. Errors: {report.summary.totalErrors}");
            }
        }

        public static AgentToolResult RunAutomatedFix(JObject args)
        {
            // Check current console errors using the existing readConsoleErrors logic
            var res = EditorToolDispatcher.Dispatch("readConsoleErrors", JObject.Parse("{\"maxCount\":10}"));
            if (!res.Success) return res;

            string errors = res.Output;
            if (errors.Contains("No errors found"))
            {
                return AgentToolResult.Ok("No errors to fix.");
            }

            // Return a structured directive to the LLM
            var sb = new StringBuilder();
            sb.AppendLine("AUTOMATED FIX DIRECTIVE:");
            sb.AppendLine("The following errors were found in the project. Please analyze them and use edit_file or generate_unity_script to resolve them:");
            sb.AppendLine();
            sb.AppendLine(errors);

            return AgentToolResult.Ok(sb.ToString());
        }

    }
}
