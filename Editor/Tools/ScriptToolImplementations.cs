using System.IO;
using UnityEditor;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    internal static class ScriptToolImplementations
    {
        internal static AgentToolResult WriteScript(JObject args)
        {
            string path = args["path"]?.Value<string>();
            string code = args["code"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/") || !path.EndsWith(".cs"))
                return AgentToolResult.Fail("Invalid path. Must start with Assets/ and end with .cs");
            if (string.IsNullOrWhiteSpace(code))
                return AgentToolResult.Fail("Code is empty.");

            // ── TIER 3: Script Validation ──
            if (code.Contains("using UnityEditor;") && !path.Contains("/Editor/"))
            {
                return AgentToolResult.Fail("VALIDATION ERROR: You included 'using UnityEditor;' but the script is not in an 'Editor' folder. UnityEditor scripts MUST be placed in a folder named 'Editor'. Otherwise, they will break the game build.");
            }
            if (!code.Contains("using UnityEngine;"))
            {
                // Most Unity scripts need this, just a soft warning or we can let it pass, but it's usually a mistake if it's a MonoBehaviour
                if (code.Contains("MonoBehaviour"))
                {
                    return AgentToolResult.Fail("VALIDATION ERROR: Script derives from MonoBehaviour but is missing 'using UnityEngine;'. Please include it.");
                }
            }

            // Ensure directory exists
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // TEXT-FILE SWAP TRICK: Save as .txt so it doesn't trigger domain reload yet
            string txtPath = path + ".txt";
            File.WriteAllText(txtPath, code, System.Text.Encoding.UTF8);

            AgentOrchestrator.ActiveOrchestrator?.TrackScript(txtPath);

            return AgentToolResult.Ok($"Wrote script successfully. (Saved securely to prevent mid-loop compiler reload). You may now call attach_script if you need to use it.");
        }

        internal static AgentToolResult DeleteScript(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/"))
                return AgentToolResult.Fail("Invalid path.");

            if (!File.Exists(path))
                return AgentToolResult.Fail($"File not found: {path}");

            AssetDatabase.DeleteAsset(path);
            return AgentToolResult.Ok($"Deleted: {path}");
        }

        internal static AgentToolResult WriteShader(JObject args)
        {
            string path = args["path"]?.Value<string>();
            string code = args["code"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/") || (!path.EndsWith(".shader") && !path.EndsWith(".hlsl")))
                return AgentToolResult.Fail("Invalid path. Must start with Assets/ and end with .shader or .hlsl");
            if (string.IsNullOrWhiteSpace(code))
                return AgentToolResult.Fail("Code is empty.");

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, code, System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(path);

            return AgentToolResult.Ok($"Wrote shader successfully to {path}.");
        }

        internal static AgentToolResult AttachScript(JObject args)
        {
            string objectId = args["objectId"]?.Value<string>();
            string scriptName = args["scriptName"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(scriptName))
                return AgentToolResult.Fail("objectId and scriptName are required.");

            var go = EditorToolDispatcher.FindInScene(SceneManager.GetActiveScene(), objectId);
            if (go == null)
                return AgentToolResult.Fail($"Object '{objectId}' not found.");

            MonoScriptResolver.RebuildIndex();
            var ms = MonoScriptResolver.Find(scriptName);
            if (ms == null)
            {
                // TEXT SWAP TRICK logic:
                AgentOrchestrator.ActiveOrchestrator?.QueueAttachment(objectId, scriptName);
                return AgentToolResult.Ok($"Script '{scriptName}' is queued. It will be bound to '{objectId}' automatically after generation completes.");
            }

            var type = ms.GetClass();
            if (type == null)
                return AgentToolResult.Fail($"Script '{scriptName}' has no valid class.");

            Undo.AddComponent(go, type);
            return AgentToolResult.Ok($"Attached '{scriptName}' to '{objectId}'.");
        }
    }
}
