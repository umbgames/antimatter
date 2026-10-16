using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Antimatter
{
    internal static class AudioToolImplementations
    {
        internal static AgentToolResult AddAudioSource(JObject args)
        {
            string objectId = args["objectId"]?.Value<string>();
            string clipPath = args["clipPath"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(objectId))
                return AgentToolResult.Fail("objectId is required.");

            var go = EditorToolDispatcher.FindInScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), objectId);
            if (go == null)
                return AgentToolResult.Fail($"Object '{objectId}' not found.");

            var audioSource = go.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = go.AddComponent<AudioSource>();
                Undo.RegisterCreatedObjectUndo(audioSource, "Agent Add AudioSource");
            }
            else
            {
                Undo.RecordObject(audioSource, "Agent Modify AudioSource");
            }

            if (!string.IsNullOrWhiteSpace(clipPath))
            {
                if (!clipPath.StartsWith("Assets/")) clipPath = "Assets/" + clipPath;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip != null)
                {
                    audioSource.clip = clip;
                }
                else
                {
                    return AgentToolResult.Fail($"AudioClip not found at {clipPath}");
                }
            }

            bool playOnAwake = args["playOnAwake"]?.Value<bool>() ?? true;
            bool loop = args["loop"]?.Value<bool>() ?? false;
            float spatialBlend = args["spatialBlend"]?.Value<float>() ?? 0f;

            audioSource.playOnAwake = playOnAwake;
            audioSource.loop = loop;
            audioSource.spatialBlend = spatialBlend;

            return AgentToolResult.Ok($"Added/Configured AudioSource on '{objectId}'.");
        }

        internal static AgentToolResult ListAudioClips(JObject args)
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip");
            var paths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).ToList();
            
            if (paths.Count == 0)
                return AgentToolResult.Ok("No audio clips found in project.");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Found {paths.Count} audio clips:");
            foreach (var p in paths)
                sb.AppendLine($"- {p}");

            return AgentToolResult.Ok(sb.ToString());
        }
    }
}
