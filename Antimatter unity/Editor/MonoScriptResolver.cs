using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace Antimatter
{
    public static class MonoScriptResolver
    {
        private static Dictionary<string, MonoScript> byClassName; // exact class name
        private static Dictionary<string, MonoScript> byFileName;  // file name without extension

        public static void RebuildIndex()
        {
            byClassName = new Dictionary<string, MonoScript>(StringComparer.Ordinal);
            byFileName  = new Dictionary<string, MonoScript>(StringComparer.OrdinalIgnoreCase);

            var guids = AssetDatabase.FindAssets("t:MonoScript");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms == null) continue;

                var type = ms.GetClass();
                if (type != null)
                {
                    // Class name index
                    if (!byClassName.ContainsKey(type.Name))
                        byClassName[type.Name] = ms;
                }

                // File name index
                var file = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrWhiteSpace(file) && !byFileName.ContainsKey(file))
                    byFileName[file] = ms;
            }
        }

        public static MonoScript Find(string scriptRef)
        {
            if (string.IsNullOrWhiteSpace(scriptRef))
                return null;

            if (byClassName == null || byFileName == null)
                RebuildIndex();

            // 1) Exact class name
            if (byClassName.TryGetValue(scriptRef, out var ms))
                return ms;

            // 2) If passed "Something.cs" or "Assets/.../Something.cs"
            var file = scriptRef;
            if (scriptRef.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                file = Path.GetFileNameWithoutExtension(scriptRef);
            else if (scriptRef.Contains("/") || scriptRef.Contains("\\"))
                file = Path.GetFileNameWithoutExtension(scriptRef);

            if (!string.IsNullOrWhiteSpace(file) && byFileName.TryGetValue(file, out ms))
                return ms;

            // 3) Last attempt: AssetDatabase search (slower)
            foreach (var guid in AssetDatabase.FindAssets($"{file} t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) continue;

                var t = script.GetClass();
                if (t != null && (t.Name == scriptRef || t.Name == file))
                    return script;
            }

            return null;
        }
    }
}
