using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Antimatter
{
    internal static class AnimationToolImplementations
    {
        internal static AgentToolResult CreateAnimator(JObject args)
        {
            string path = args["path"]?.Value<string>();
            string objectId = args["objectId"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(path))
                return AgentToolResult.Fail("path is required.");

            if (!path.EndsWith(".controller")) path += ".controller";
            if (!path.StartsWith("Assets/")) path = "Assets/" + path;

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Created AnimatorController at {path}");

            if (!string.IsNullOrWhiteSpace(objectId))
            {
                var go = EditorToolDispatcher.FindInScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), objectId);
                if (go != null)
                {
                    var animator = go.GetComponent<Animator>();
                    if (animator == null)
                        animator = go.AddComponent<Animator>();
                    
                    animator.runtimeAnimatorController = controller;
                    sb.AppendLine($"Attached to object '{objectId}'");
                }
                else
                {
                    sb.AppendLine($"Warning: Could not find object '{objectId}' to attach to.");
                }
            }

            return AgentToolResult.Ok(sb.ToString());
        }

        internal static AgentToolResult CreateAnimationClip(JObject args)
        {
            string path = args["path"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(path))
                return AgentToolResult.Fail("path is required.");

            if (!path.EndsWith(".anim")) path += ".anim";
            if (!path.StartsWith("Assets/")) path = "Assets/" + path;

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();

            return AgentToolResult.Ok($"Created empty AnimationClip at {path}");
        }
    }
}
