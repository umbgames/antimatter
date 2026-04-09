using Newtonsoft.Json.Linq;

namespace Antimatter
{
    /// <summary>
    /// Interface that must be implemented by any tool downloaded from the Antimatter Marketplace.
    /// Implementing classes will automatically be discovered and exposed to the AI Agent.
    /// </summary>
    public interface IMarketplaceTool
    {
        string Name { get; }
        string Description { get; }
        string ParameterSchema { get; }
        AgentToolResult Execute(JObject args);
    }
}
