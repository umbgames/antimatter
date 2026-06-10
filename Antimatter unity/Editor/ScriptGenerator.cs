using UnityEditor;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Antimatter
{
    
    public static class ScriptGenerator
    {
        public static void GenerateScripts(GameGeneration gen)
        {
            if (gen == null || gen.scripts == null) return;
    
            foreach (var s in gen.scripts)
            {
                if (string.IsNullOrWhiteSpace(s.path)) continue;
    
                EnsureFolderExistsForFile(s.path);
    
                // Deterministic patch pass BEFORE writing
                string code = ApplyDeterministicPatches(s.code ?? "");
    
                File.WriteAllText(s.path, code, new UTF8Encoding(false));
            }
    
            AssetDatabase.Refresh();
        }
    
        private static void EnsureFolderExistsForFile(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath)?.Replace("\\", "/");
            if (string.IsNullOrEmpty(dir)) return;
    
            // Create folders under Assets
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string[] parts = dir.Split('/');
                string current = parts[0]; // "Assets"
    
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }
    
        private static string ApplyDeterministicPatches(string code)
        {
            // Normalize line endings
            code = code.Replace("\r\n", "\n").Replace("\r", "\n");
    
            // Patch 1: SceneManager usage requires using UnityEngine.SceneManagement;
            if (code.Contains("SceneManager.") && !Regex.IsMatch(code, @"using\s+UnityEngine\.SceneManagement\s*;"))
            {
                code = InjectUsing(code, "using UnityEngine.SceneManagement;");
            }
    
            // Patch 2: InputSystem common mistake (optional)
            // If you ever allow new Input System references, you’d patch here.
    
            return code;
        }
    
        private static string InjectUsing(string code, string usingLine)
        {
            // If there are using statements, insert after the last using.
            var match = Regex.Matches(code, @"^\s*using\s+[A-Za-z0-9_.]+\s*;\s*$", RegexOptions.Multiline);
            if (match.Count > 0)
            {
                int insertPos = match[match.Count - 1].Index + match[match.Count - 1].Length;
                return code.Insert(insertPos, "\n" + usingLine);
            }
    
            // No usings found: insert at top
            return usingLine + "\n" + code;
        }
    }
}
