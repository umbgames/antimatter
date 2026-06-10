using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Antimatter
{
    public static class PlayModeTester
    {
        private static List<string> _playModeLogs = new List<string>();

        public static AgentToolResult ToolEnterPlayMode(JObject args)
        {
            if (EditorApplication.isPlaying)
                return AgentToolResult.Ok("Already in Play Mode.");

            float duration = args["duration"]?.Value<float>() ?? 0f;

            _playModeLogs.Clear();
            Application.logMessageReceived += HandlePlayModeLog;

            EditorApplication.isPlaying = true;

            // Note: Since this is an editor script running synchronously in the agent loop,
            // we cannot easily "wait" and halt the agent loop to observe real-time playmode for N seconds 
            // without blocking the main thread.
            // For now, it simply enters play mode and returns immediately.
            // A more advanced system would pause the agent Orchestrator Task and resume it.
            
            return AgentToolResult.Ok("Entered Play Mode. Use exitPlayMode later to stop. The game is currently running.");
        }

        public static AgentToolResult ToolExitPlayMode(JObject args)
        {
            if (!EditorApplication.isPlaying)
                return AgentToolResult.Ok("Not in Play Mode.");

            Application.logMessageReceived -= HandlePlayModeLog;
            EditorApplication.isPlaying = false;

            string logSummary = _playModeLogs.Count > 0 
                ? $"Caught {_playModeLogs.Count} logs/errors during runtime:\n" + string.Join("\n", _playModeLogs) 
                : "No errors or warnings occurred during play mode.";

            _playModeLogs.Clear();

            return AgentToolResult.Ok($"Exited Play Mode.\n{logSummary}");
        }

        private static void HandlePlayModeLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
            {
                if (_playModeLogs.Count < 50)
                {
                    _playModeLogs.Add($"[{type}] {logString}");
                }
            }
        }
    }
}
