using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public sealed class CustomModelPlanClient : IGamePlanClient
{
    private readonly string endpointUrl;

    private static readonly HttpClient Http = CreateHttpClient();

    public CustomModelPlanClient(string endpointUrl)
    {
        this.endpointUrl = endpointUrl;
    }

    public async Task<string> GeneratePlanAsync(string userPrompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl))
            throw new Exception("Custom server URL is empty.");

        if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var uri))
            throw new Exception("Custom server URL is not a valid absolute URL.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new Exception("Custom server URL must start with http:// or https://");

        ct.ThrowIfCancellationRequested();

        var payload = new JObject
        {
            ["schemaName"] = "unity_game_plan",
            ["schema"] = JToken.Parse(OpenAIJsonPlanClientSchema.SchemaJson),
            ["system"] = OpenAIJsonPlanClientSchema.SystemPrompt,
            ["prompt"] = userPrompt
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, uri);
        req.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        string respText = await resp.Content.ReadAsStringAsync();

        ct.ThrowIfCancellationRequested();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Custom server error {(int)resp.StatusCode}: {respText}");

        var root = JToken.Parse(respText);

        if (root.Type == JTokenType.Object && root["version"] != null && root["scenes"] != null)
            return root.ToString();

        var plan = root["plan"];
        if (plan != null && plan.Type == JTokenType.Object)
            return plan.ToString();

        throw new Exception("Custom server returned JSON but not a valid plan (missing version/scenes).");
    }

    public async Task<AgentResponse> GenerateAgentResponseAsync(
        List<AgentMessage> history,
        List<AgentTool> tools,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(endpointUrl))
            throw new Exception("Custom server URL is empty.");

        ct.ThrowIfCancellationRequested();

        // Build a generic agentic request for the custom server
        var messagesArray = new JArray();
        foreach (var msg in history)
        {
            var m = new JObject
            {
                ["role"] = msg.role.ToString().ToLowerInvariant(),
                ["content"] = msg.content ?? ""
            };
            if (!string.IsNullOrEmpty(msg.toolCallId))
                m["tool_call_id"] = msg.toolCallId;
            if (!string.IsNullOrEmpty(msg.toolName))
                m["tool_name"] = msg.toolName;
            if (!string.IsNullOrEmpty(msg.toolArgs))
                m["tool_args"] = msg.toolArgs;
            messagesArray.Add(m);
        }

        var toolDefs = new JArray();
        if (tools != null)
        {
            foreach (var tool in tools)
            {
                toolDefs.Add(new JObject
                {
                    ["type"] = "function",
                    ["function"] = new JObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = JToken.Parse(tool.ParameterSchema ?? "{\"type\":\"object\",\"properties\":{}}")
                    }
                });
            }
        }

        var payload = new JObject
        {
            ["mode"] = "agent",
            ["messages"] = messagesArray,
            ["tools"] = toolDefs
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
        req.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        string respText = await resp.Content.ReadAsStringAsync();

        ct.ThrowIfCancellationRequested();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Custom server error {(int)resp.StatusCode}: {respText}");

        // Try parsing as OpenAI-compatible format first
        try
        {
            return GroqPlanClient.ParseOpenAICompatibleAgentResponse(respText);
        }
        catch
        {
            // Fallback: treat entire response as plain text
            return new AgentResponse { TextContent = respText };
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(AiProviderSettings.HttpTimeoutSeconds);
        return http;
    }
}
