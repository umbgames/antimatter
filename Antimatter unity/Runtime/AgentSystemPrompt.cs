namespace Antimatter
{
    /// <summary>
    /// Dynamic system prompt is now managed exclusively by the Antimatter backend.
    /// This file is retained for backward compatibility, but returns an empty string.
    /// </summary>
    public static class AgentSystemPrompt
    {
        public static string Build()
        {
            return "";
        }
    }
}
