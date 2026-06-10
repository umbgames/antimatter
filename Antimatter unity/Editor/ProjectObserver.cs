using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Antimatter
{
    /// <summary>
    /// Reads the project asset structure to give the agent awareness of what files exist.
    /// </summary>
    public static class ProjectObserver
    {
        public static List<string> GetGeneratedScripts()
        {
            var results = new List<string>();
            string folder = "Assets/Generated";
            if (!AssetDatabase.IsValidFolder(folder)) return results;

            var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".cs"))
                    results.Add(path);
            }

            return results;
        }

        public static List<string> GetAllScripts()
        {
            var results = new List<string>();
            var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".cs") && !path.Contains("/Editor/"))
                    results.Add(path);
            }
            return results;
        }

        public static List<string> GetGeneratedMaterials()
        {
            var results = new List<string>();
            string folder = "Assets/Generated";
            if (!AssetDatabase.IsValidFolder(folder)) return results;

            var guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
            foreach (var guid in guids)
                results.Add(AssetDatabase.GUIDToAssetPath(guid));

            return results;
        }

        public static List<string> GetSceneFiles()
        {
            var results = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            foreach (var guid in guids)
                results.Add(AssetDatabase.GUIDToAssetPath(guid));
            return results;
        }

        public static List<string> GetConsoleErrors(int maxCount = 30)
        {
            var errors = new List<string>();

            // Use reflection to access Unity's internal LogEntries
            var logEntriesType = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
            if (logEntriesType == null) return errors;

            var getCountMethod = logEntriesType.GetMethod("GetCount",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            var getEntryMethod = logEntriesType.GetMethod("GetEntryInternal",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

            if (getCountMethod == null) return errors;

            int count = (int)getCountMethod.Invoke(null, null);
            if (count == 0) return errors;

            // Try simpler approach: StartGettingEntries/GetEntryAtIndex
            var startMethod = logEntriesType.GetMethod("StartGettingEntries",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            var endMethod = logEntriesType.GetMethod("EndGettingEntries",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

            if (startMethod != null)
            {
                try
                {
                    startMethod.Invoke(null, null);

                    var logEntryType = System.Type.GetType("UnityEditor.LogEntry, UnityEditor");
                    if (logEntryType != null)
                    {
                        var conditionField = logEntryType.GetField("condition") ?? logEntryType.GetField("message");
                        var modeField = logEntryType.GetField("mode");

                        var getEntryInternal = logEntriesType.GetMethod("GetEntryInternal",
                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                        if (getEntryInternal != null && conditionField != null)
                        {
                            int start = System.Math.Max(0, count - maxCount);
                            for (int i = start; i < count; i++)
                            {
                                var entry = System.Activator.CreateInstance(logEntryType);
                                getEntryInternal.Invoke(null, new object[] { i, entry });

                                string condition = conditionField.GetValue(entry) as string ?? "";
                                int mode = modeField != null ? (int)modeField.GetValue(entry) : 0;

                                // mode bits: 1=error, 2=assert, 4=log, 8=fatal, 16=sticky, etc.
                                bool isError = (mode & 1) != 0 || (mode & 2) != 0 || (mode & 8) != 0;
                                if (isError && !string.IsNullOrWhiteSpace(condition))
                                {
                                    // Truncate long messages
                                    if (condition.Length > 300)
                                        condition = condition.Substring(0, 300) + "...";
                                    errors.Add(condition);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    endMethod?.Invoke(null, null);
                }
            }

            return errors;
        }

        public static string GetProjectSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("PROJECT ASSETS:");

            var scenes = GetSceneFiles();
            sb.AppendLine($"  Scenes ({scenes.Count}): {string.Join(", ", scenes.Take(20))}");

            var scripts = GetAllScripts();
            sb.AppendLine($"  Runtime Scripts ({scripts.Count}): {string.Join(", ", scripts.Take(30))}");

            var genScripts = GetGeneratedScripts();
            if (genScripts.Count > 0)
                sb.AppendLine($"  Generated Scripts ({genScripts.Count}): {string.Join(", ", genScripts.Take(20))}");

            var mats = GetGeneratedMaterials();
            if (mats.Count > 0)
                sb.AppendLine($"  Generated Materials ({mats.Count}): {string.Join(", ", mats.Take(20))}");

            return sb.ToString();
        }
    }
}
