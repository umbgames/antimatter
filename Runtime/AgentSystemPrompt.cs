using System.Text;

namespace Antimatter
{
    /// <summary>
    /// Builds the dynamic system prompt injected at the start of every agentic request.
    /// Includes agent persona, available tools, current scene state, and constraints.
    /// </summary>
    public static class AgentSystemPrompt
    {
        public static string Build()
        {
            var sb = new StringBuilder();

            // Token-optimized system prompt: ~700 chars instead of ~1500
            sb.AppendLine(@"You are a Unity Editor AI Agent with direct tool control.

RULES:
1. Call inspectScene FIRST to see what exists.
2. Use tools (createObject, writeScript, etc.) to DO the work — NEVER give tutorials or code snippets.
3. Scripts: valid C# MonoBehaviours only. No UnityEditor namespace. Include SceneManagement using if needed.
4. Primitives: Cube, Sphere, Capsule, Plane, Cylinder, Quad. Use unique descriptive ids.
5. After writeScript, call readConsoleErrors. Fix errors if any.
6. For complex games use runFullPlan. For simple tasks use granular tools.
7. Say DONE with a brief summary when finished.
8. You are in EDITOR mode, not play mode. You BUILD scenes.");

            sb.AppendLine("Use inspectScene for hierarchy, listAssets for project files.");

#if UNITY_EDITOR
            sb.AppendLine($"Config: {AiProviderSettings.Provider}/{AiProviderSettings.CurrentModel}, max {AiProviderSettings.AgentMaxSteps} steps.");
#endif

            return sb.ToString();
        }
    }
}
