using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    internal static class LightingToolImplementations
    {
        internal static AgentToolResult ConfigureLighting(JObject args)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Lighting Configuration:");

            bool? autoGenerate = args["autoGenerate"]?.Value<bool>();
            if (autoGenerate.HasValue)
            {
                Lightmapping.giWorkflowMode = autoGenerate.Value ? Lightmapping.GIWorkflowMode.Iterative : Lightmapping.GIWorkflowMode.OnDemand;
                sb.AppendLine($"- Auto Generate GI: {autoGenerate.Value}");
            }

            bool? bakeGI = args["bakeGI"]?.Value<bool>();
            if (bakeGI.HasValue && bakeGI.Value)
            {
                Lightmapping.BakeAsync();
                sb.AppendLine("- Started asynchronous GI bake.");
            }

            return AgentToolResult.Ok(sb.ToString());
        }

        internal static AgentToolResult CreateProceduralSkybox(JObject args)
        {
            string path = args["path"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(path))
                return AgentToolResult.Fail("path is required.");

            if (!path.EndsWith(".mat")) path += ".mat";
            if (!path.StartsWith("Assets/")) path = "Assets/" + path;

            var mat = new Material(Shader.Find("Skybox/Procedural"));

            string sunSize = args["sunSize"]?.Value<string>();
            if (sunSize != null) mat.SetFloat("_SunSize", float.Parse(sunSize));

            string skyTintHex = args["skyTintHex"]?.Value<string>();
            if (skyTintHex != null && ColorUtility.TryParseHtmlString(skyTintHex, out Color skyTint))
                mat.SetColor("_SkyTint", skyTint);

            string groundColorHex = args["groundColorHex"]?.Value<string>();
            if (groundColorHex != null && ColorUtility.TryParseHtmlString(groundColorHex, out Color groundColor))
                mat.SetColor("_GroundColor", groundColor);

            string exposure = args["exposure"]?.Value<string>();
            if (exposure != null) mat.SetFloat("_Exposure", float.Parse(exposure));

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();

            bool apply = args["applyToScene"]?.Value<bool>() ?? true;
            if (apply)
            {
                Undo.RecordObject(RenderSettings.sun, "Agent Set Skybox");
                RenderSettings.skybox = mat;
            }

            return AgentToolResult.Ok($"Created Procedural Skybox at {path}{(apply ? " and applied to scene." : ".")}");
        }
    }
}
