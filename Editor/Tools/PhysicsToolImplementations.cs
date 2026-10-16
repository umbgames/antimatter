using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Newtonsoft.Json.Linq;
using UnityEditor.AI;

namespace Antimatter
{
    internal static class PhysicsToolImplementations
    {
        internal static AgentToolResult BakeNavMesh(JObject args)
        {
            try
            {
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
                return AgentToolResult.Ok("Successfully baked NavMesh for the scene.");
            }
            catch (System.Exception ex)
            {
                return AgentToolResult.Fail($"Failed to bake NavMesh: {ex.Message}");
            }
        }

        internal static AgentToolResult Raycast(JObject args)
        {
            var originNode = args["origin"];
            var dirNode = args["direction"];
            float maxDistance = args["maxDistance"]?.Value<float>() ?? Mathf.Infinity;
            int layerMask = args["layerMask"]?.Value<int>() ?? Physics.DefaultRaycastLayers;

            if (originNode == null || dirNode == null)
                return AgentToolResult.Fail("origin and direction are required.");

            Vector3 origin = new Vector3(
                originNode["x"]?.Value<float>() ?? 0,
                originNode["y"]?.Value<float>() ?? 0,
                originNode["z"]?.Value<float>() ?? 0
            );

            Vector3 direction = new Vector3(
                dirNode["x"]?.Value<float>() ?? 0,
                dirNode["y"]?.Value<float>() ?? 0,
                dirNode["z"]?.Value<float>() ?? 0
            ).normalized;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, layerMask))
            {
                var result = new System.Text.StringBuilder();
                result.AppendLine("Raycast Hit:");
                result.AppendLine($"  Object: {(hit.collider != null ? hit.collider.gameObject.name : "Unknown")}");
                result.AppendLine($"  Point: {hit.point}");
                result.AppendLine($"  Normal: {hit.normal}");
                result.AppendLine($"  Distance: {hit.distance}");
                return AgentToolResult.Ok(result.ToString());
            }

            return AgentToolResult.Ok("Raycast did not hit anything.");
        }
    }
}
