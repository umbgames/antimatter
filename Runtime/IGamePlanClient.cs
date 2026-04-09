using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Antimatter
{
    public interface IGamePlanClient
    {
        /// <summary>
        /// Single-shot text generation (legacy plan generation).
        /// </summary>
        Task<string> GeneratePlanAsync(string userPrompt, CancellationToken ct);

        /// <summary>
        /// Agentic function-calling round: sends conversation history + tool definitions,
        /// receives either a text reply or tool call requests.
        /// </summary>
        Task<AgentResponse> GenerateAgentResponseAsync(
            List<AgentMessage> history,
            List<AgentTool> tools,
            CancellationToken ct);
    }
}
