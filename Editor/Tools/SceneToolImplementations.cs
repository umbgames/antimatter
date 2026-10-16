using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    internal static class SceneToolImplementations
    {
        internal static AgentToolResult InspectScene(JObject args)
        {
            var snap = SceneObserver.GetSnapshot();
            return AgentToolResult.Ok(SceneObserver.ToAgentContext(snap));
        }

        internal static AgentToolResult ListAssets(JObject args)
        {
            return AgentToolResult.Ok(ProjectObserver.GetProjectSummary());
        }

        internal static AgentToolResult SaveScene(JObject args)
        {
            EditorSceneManager.SaveOpenScenes();
            return AgentToolResult.Ok("Scene saved.");
        }

        internal static AgentToolResult CreateScene(JObject args)
        {
            string name = args["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(name))
                return AgentToolResult.Fail("name is required.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            scene.name = name;
            SceneManager.SetActiveScene(scene);

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{name}.unity");
            return AgentToolResult.Ok($"Created and activated scene '{name}'.");
        }

        internal static AgentToolResult FocusSceneView(JObject args)
        {
            string objectName = args["objectName"]?.Value<string>();

            if (!string.IsNullOrWhiteSpace(objectName))
            {
                var go = EditorToolDispatcher.FindInScene(SceneManager.GetActiveScene(), objectName);
                if (go == null)
                    return AgentToolResult.Fail($"Object '{objectName}' not found.");

                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
                return AgentToolResult.Ok(string.IsNullOrWhiteSpace(objectName)
                    ? "Focused Scene View on current selection."
                    : $"Focused Scene View on '{objectName}'.");
            }

            return AgentToolResult.Fail("No active Scene View found.");
        }

        internal static AgentToolResult SetSceneEnvironment(JObject args)
        {
            string skyboxColorHex = args["skyboxColorHex"]?.Value<string>();
            string ambientColorHex = args["ambientColorHex"]?.Value<string>();
            bool? enableFog = args["enableFog"]?.Value<bool>();
            string fogColorHex = args["fogColorHex"]?.Value<string>();

            if (RenderSettings.sun != null) Undo.RecordObject(RenderSettings.sun, "Agent Set Environment");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Environment updated:");

            if (!string.IsNullOrWhiteSpace(skyboxColorHex))
            {
                if (ColorUtility.TryParseHtmlString(skyboxColorHex, out Color sc))
                {
                    RenderSettings.skybox = null; // Clear skybox material to use solid color
                    if (Camera.main != null)
                    {
                        Undo.RecordObject(Camera.main, "Agent Set Environment Camera");
                        Camera.main.clearFlags = CameraClearFlags.SolidColor;
                        Camera.main.backgroundColor = sc;
                    }
                    sb.AppendLine($"- Skybox color set to {skyboxColorHex}");
                }
                else
                {
                    sb.AppendLine($"- Invalid skybox color hex: {skyboxColorHex}");
                }
            }

            if (!string.IsNullOrWhiteSpace(ambientColorHex))
            {
                if (ColorUtility.TryParseHtmlString(ambientColorHex, out Color ac))
                {
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                    RenderSettings.ambientLight = ac;
                    sb.AppendLine($"- Ambient color set to {ambientColorHex}");
                }
                else
                {
                    sb.AppendLine($"- Invalid ambient color hex: {ambientColorHex}");
                }
            }

            if (enableFog.HasValue)
            {
                RenderSettings.fog = enableFog.Value;
                sb.AppendLine($"- Fog enabled: {enableFog.Value}");
            }

            if (!string.IsNullOrWhiteSpace(fogColorHex))
            {
                if (ColorUtility.TryParseHtmlString(fogColorHex, out Color fc))
                {
                    RenderSettings.fogColor = fc;
                    sb.AppendLine($"- Fog color set to {fogColorHex}");
                }
                else
                {
                    sb.AppendLine($"- Invalid fog color hex: {fogColorHex}");
                }
            }

            return AgentToolResult.Ok(sb.ToString());
        }
    }
}
