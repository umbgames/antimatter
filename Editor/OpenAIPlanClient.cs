using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class OpenAIPlanClient : IGamePlanClient
{
    public Task<string> GeneratePlanAsync(string userPrompt, CancellationToken ct)
        => OpenAIJsonPlanClient.GeneratePlanAsync(userPrompt, ct);

    public Task<AgentResponse> GenerateAgentResponseAsync(
        List<AgentMessage> history,
        List<AgentTool> tools,
        CancellationToken ct)
        => OpenAIJsonPlanClient.GenerateAgentResponseAsync(history, tools, ct);
}
