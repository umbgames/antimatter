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
1. Use tools (createObject, writeScript, etc.) to DO the work — NEVER give tutorials or code snippets.
2. Scripts: valid C# MonoBehaviours only. No UnityEditor namespace. Include SceneManagement using if needed.
3. Primitives: Cube, Sphere, Capsule, Plane, Cylinder, Quad. Use unique descriptive ids.
4. Check readConsoleErrors ONLY at the very end right before saving the scene. Fix errors if any.
5. For complex games use runFullPlan. For simple tasks use granular tools.
6. Say DONE with a brief summary when finished.
7. You are in EDITOR mode, not play mode. You BUILD scenes.
8. ONLY use inspectScene or listAssets if you truly need to know the state. Do not call them mindlessly.
9. Shaders/Materials: ALWAYS use `searchAssets` first to check if a suitable shader or material exists (like a Water shader) before using `writeShader` to generate one from scratch.");

#if UNITY_EDITOR
            sb.AppendLine($"Config: {AiProviderSettings.Provider}/{AiProviderSettings.CurrentModel}, max {AiProviderSettings.AgentMaxSteps} steps.");
#endif

            return sb.ToString();
        }
    }
}
