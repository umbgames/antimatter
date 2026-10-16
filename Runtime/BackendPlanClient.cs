using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    public class BackendPlanClient : IGamePlanClient
    {
        private static readonly HttpClient Http;
        private const string BackendBaseUrl = "https://api.umbtechnologies.com";

        static BackendPlanClient()
        {
            Http = new HttpClient();
            Http.Timeout = TimeSpan.FromSeconds(AiProviderSettings.HttpTimeoutSeconds);
        }

        private async Task EnsureValidTokenAsync(CancellationToken ct)
        {
#if UNITY_EDITOR
            if (!FirebaseApiClient.IsLoggedIn)
                throw new Exception("User is not logged into Antimatter. Please log in via the Marketplace window first.");

            await FirebaseApiClient.EnsureValidTokenAsync(ct);
#else
            await Task.CompletedTask;
            throw new Exception("BackendPlanClient requires Unity Editor for authentication.");
#endif
        }

        public async Task<string> GeneratePlanAsync(string userPrompt, CancellationToken ct)
        {
            await EnsureValidTokenAsync(ct);

            string url = $"{BackendBaseUrl}/v1/unity/generate-plan";

            var payload = new JObject
            {
                ["prompt"] = userPrompt,
                ["tier"] = AiProviderSettings.Provider.ToString().ToLowerInvariant()
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
#if UNITY_EDITOR
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MarketplaceSettings.CurrentToken);
#endif
            req.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

            using var resp = await Http.SendAsync(req, ct);
            string respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                string msg = respText;
                try
                {
                    var err = JToken.Parse(respText);
                    msg = err["error"]?.Value<string>() ?? respText;
                }
                catch { }
                throw new Exception($"Backend Error ({(int)resp.StatusCode}): {msg}");
            }

            var root = JToken.Parse(respText);
            
            // Check if it's the async queued response
            if (resp.StatusCode == System.Net.HttpStatusCode.Accepted || root["status"]?.Value<string>() == "queued")
            {
                string taskId = root["data"]?["taskId"]?.Value<string>() ?? root["taskId"]?.Value<string>();
                if (string.IsNullOrEmpty(taskId))
                    throw new Exception("Received queued status but no taskId.");

                return await PollForPlanAsync(taskId, ct);
            }

            return root["data"]?["plan"]?.Value<string>() ?? respText;
        }

        private async Task<string> PollForPlanAsync(string taskId, CancellationToken ct)
        {
            string url = $"{BackendBaseUrl}/v1/unity/task/{taskId}";

            while (true)
            {
                await Task.Delay(3000, ct); // Poll every 3 seconds

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
#if UNITY_EDITOR
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MarketplaceSettings.CurrentToken);
#endif
                using var resp = await Http.SendAsync(req, ct);
                string respText = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    continue;

                var root = JToken.Parse(respText);
                string status = root["status"]?.Value<string>();

                if (status == "completed")
                {
                    return root["plan"]?.Value<string>() ?? "{}";
                }
                else if (status == "failed")
                {
                    string err = root["error"]?.Value<string>() ?? "Unknown error occurred.";
                    throw new Exception($"Backend Task Failed: {err}");
                }
            }
        }

        public async Task<AgentResponse> GenerateAgentResponseAsync(List<AgentMessage> history, List<AgentTool> tools, CancellationToken ct)
        {
            await EnsureValidTokenAsync(ct);

            string url = $"{BackendBaseUrl}/v1/unity/agent";

            var messagesArray = new JArray();
            foreach (var msg in history)
            {
                var roleStr = msg.role.ToString().ToLowerInvariant();
                var msgObj = new JObject
                {
                    ["role"] = roleStr,
                    ["content"] = string.IsNullOrEmpty(msg.content) ? null : msg.content
                };

                if (msg.role == AgentRole.Assistant && !string.IsNullOrEmpty(msg.toolCallId))
                {
                    var functionCall = new JObject
                    {
                        ["name"] = msg.toolName,
                        ["arguments"] = msg.toolArgs
                    };
                    var toolCall = new JObject
                    {
                        ["id"] = msg.toolCallId,
                        ["type"] = "function",
                        ["function"] = functionCall
                    };
                    msgObj["tool_calls"] = new JArray { toolCall };
                }
                else if (msg.role == AgentRole.Tool)
                {
                    msgObj["tool_call_id"] = msg.toolCallId;
                    msgObj["name"] = msg.toolName;
                }

                messagesArray.Add(msgObj);
            }

            var toolsArray = new JArray();
            if (tools != null)
            {
                foreach (var t in tools)
                {
                    var functionObj = new JObject
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description
                    };
                    if (!string.IsNullOrEmpty(t.ParameterSchema))
                    {
                        functionObj["parameters"] = JObject.Parse(t.ParameterSchema);
                    }
                    
                    var toolObj = new JObject
                    {
                        ["type"] = "function",
                        ["function"] = functionObj
                    };
                    toolsArray.Add(toolObj);
                }
            }

            var payload = new JObject
            {
                ["messages"] = messagesArray,
                ["tier"] = AiProviderSettings.Provider.ToString().ToLowerInvariant()
            };

            if (toolsArray.Count > 0)
            {
                payload["tools"] = toolsArray;
            }

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
#if UNITY_EDITOR
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MarketplaceSettings.CurrentToken);
#endif
            req.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

            using var resp = await Http.SendAsync(req, ct);
            string respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                string msg = respText;
                try
                {
                    var err = JToken.Parse(respText);
                    msg = err["error"]?.Value<string>() ?? respText;
                }
                catch { }
                throw new Exception($"Backend Error ({(int)resp.StatusCode}): {msg}");
            }

            var root = JToken.Parse(respText);
            
            // Check if it's the async queued response
            if (resp.StatusCode == System.Net.HttpStatusCode.Accepted || root["status"]?.Value<string>() == "queued")
            {
                string taskId = root["taskId"]?.Value<string>() ?? root["data"]?["taskId"]?.Value<string>();
                if (string.IsNullOrEmpty(taskId))
                    throw new Exception("Received queued status but no taskId.");

                return await PollForAgentResponseAsync(taskId, ct);
            }

            return ParseAgentResponse(root);
        }

        private async Task<AgentResponse> PollForAgentResponseAsync(string taskId, CancellationToken ct)
        {
            string url = $"{BackendBaseUrl}/v1/unity/task/{taskId}";

            while (true)
            {
                await Task.Delay(3000, ct); // Poll every 3 seconds

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
#if UNITY_EDITOR
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MarketplaceSettings.CurrentToken);
#endif
                using var resp = await Http.SendAsync(req, ct);
                string respText = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    continue;

                var root = JToken.Parse(respText);
                string status = root["status"]?.Value<string>();

                if (status == "completed")
                {
                    var responseData = root["responseData"];
                    if (responseData == null) throw new Exception("Task completed but responseData missing.");
                    
                    // Add metadata into responseData so ParseAgentResponse can read it
                    responseData["antimatter_metadata"] = new JObject
                    {
                        ["creditsConsumed"] = root["creditsConsumed"],
                        ["unityCreditBalance"] = root["unityCreditBalance"]
                    };
                    
                    return ParseAgentResponse(responseData);
                }
                else if (status == "failed")
                {
                    string err = root["error"]?.Value<string>() ?? "Unknown error occurred.";
                    throw new Exception($"Backend Task Failed: {err}");
                }
            }
        }

        private AgentResponse ParseAgentResponse(JToken root)
        {
            var response = new AgentResponse();
            var choices = root["choices"] as JArray;
            if (choices != null && choices.Count > 0)
            {
                var msg = choices[0]["message"];
                if (msg != null)
                {
                    response.TextContent = msg["content"]?.Value<string>();
                    var toolCalls = msg["tool_calls"] as JArray;
                    if (toolCalls != null && toolCalls.Count > 0)
                    {
                        response.ToolCalls = new List<AgentToolCall>();
                        foreach (var call in toolCalls)
                        {
                            var argStr = call["function"]?["arguments"]?.Value<string>();
                            JObject argsObj = null;
                            try { if (!string.IsNullOrEmpty(argStr)) argsObj = JObject.Parse(argStr); } catch { }

                            response.ToolCalls.Add(new AgentToolCall
                            {
                                CallId = call["id"]?.Value<string>(),
                                ToolName = call["function"]?["name"]?.Value<string>(),
                                Arguments = argsObj ?? new JObject()
                            });
                        }
                    }
                }
            }

            var usage = root["usage"];
            if (usage != null)
            {
                response.PromptTokens = usage["prompt_tokens"]?.Value<int>() ?? 0;
                response.CompletionTokens = usage["completion_tokens"]?.Value<int>() ?? 0;
            }

            var antimatterMetadata = root["antimatter_metadata"];
            if (antimatterMetadata != null)
            {
                response.CreditsConsumed = antimatterMetadata["creditsConsumed"]?.Value<float>() ?? 0f;
                response.UnityCreditBalance = antimatterMetadata["unityCreditBalance"]?.Value<float>() ?? 0f;
            }

            return response;
        }
    }
}
