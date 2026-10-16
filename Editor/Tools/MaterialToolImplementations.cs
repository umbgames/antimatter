using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    internal static class MaterialToolImplementations
    {
        internal static AgentToolResult CreateMaterial(JObject args)
        {
            string name = args["name"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(name))
                return AgentToolResult.Fail("name is required.");

            string shaderName = args["shader"]?.Value<string>() ?? "Standard";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                if (shader == null)
                    return AgentToolResult.Fail("No shader could be found.");
            }

            var mat = new Material(shader);
            mat.name = name;

            float r = args["colorR"]?.Value<float>() ?? 1f;
            float g = args["colorG"]?.Value<float>() ?? 1f;
            float b = args["colorB"]?.Value<float>() ?? 1f;
            float a = args["colorA"]?.Value<float>() ?? 1f;
            mat.color = new Color(r, g, b, a);

            if (args["metallic"] != null)
                mat.SetFloat("_Metallic", args["metallic"].Value<float>());
            if (args["smoothness"] != null)
                mat.SetFloat("_Glossiness", args["smoothness"].Value<float>());

            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");

            string matPath = $"Assets/Generated/{name}.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            // Optionally assign to object
            string assignTo = args["assignTo"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(assignTo))
            {
                var go = EditorToolDispatcher.FindInScene(SceneManager.GetActiveScene(), assignTo);
                if (go != null)
                {
                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Undo.RecordObject(renderer, "Agent Assign Material");
                        renderer.sharedMaterial = mat;
                    }
                }
            }

            return AgentToolResult.Ok($"Created material '{name}' at {matPath}");
        }

        internal static AgentToolResult CreateParticleSystem(JObject args)
        {
            string id = args["id"]?.Value<string>();
            if (string.IsNullOrEmpty(id)) return AgentToolResult.Fail("id is required.");

            float x = args["x"]?.Value<float>() ?? 0f;
            float y = args["y"]?.Value<float>() ?? 0f;
            float z = args["z"]?.Value<float>() ?? 0f;
            string style = args["style"]?.Value<string>() ?? "Smoke";
            string colorHex = args["colorHex"]?.Value<string>() ?? "#FFFFFF";

            GameObject go = new GameObject(id);
            go.transform.position = new Vector3(x, y, z);
            var ps = go.AddComponent<ParticleSystem>();
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;

            if (ColorUtility.TryParseHtmlString(colorHex, out Color c))
            {
                main.startColor = c;
            }

            switch (style)
            {
                case "Fire":
                    main.startSpeed = 2f;
                    main.startSize = 1.5f;
                    main.startLifetime = 1.5f;
                    emission.rateOverTime = 20;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 15;
                    main.startColor = new Color(1f, 0.5f, 0f, 0.8f);
                    break;
                case "Smoke":
                    main.startSpeed = 1f;
                    main.startSize = new ParticleSystem.MinMaxCurve(1f, 3f);
                    main.startLifetime = 3f;
                    emission.rateOverTime = 10;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 25;
                    main.startColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    break;
                case "WaterSplash":
                    main.startSpeed = 5f;
                    main.startSize = 0.2f;
                    main.startLifetime = 0.8f;
                    main.gravityModifier = 1f;
                    emission.rateOverTime = 50;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 30;
                    main.startColor = new Color(0.7f, 0.9f, 1f, 0.8f);
                    break;
                case "Magic":
                    main.startSpeed = 0.5f;
                    main.startSize = 0.5f;
                    main.startLifetime = 2f;
                    emission.rateOverTime = 30;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 2f;
                    break;
                case "Sparks":
                    main.startSpeed = 10f;
                    main.startSize = 0.1f;
                    main.startLifetime = 0.5f;
                    main.gravityModifier = 1.5f;
                    emission.rateOverTime = 0;
                    emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });
                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    main.startColor = new Color(1f, 0.8f, 0.2f, 1f);
                    break;
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create ParticleSystem {id}");
            return AgentToolResult.Ok($"Created {style} particle system at {x}, {y}, {z}");
        }
    }
}
