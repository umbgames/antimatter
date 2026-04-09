using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    
    public sealed class GeminiPlanClient : IGamePlanClient
    {
        private static HttpClient _http;
    
        private static HttpClient Http
        {
            get
            {
                if (_http == null)
                {
                    _http = new HttpClient();
                    _http.Timeout = TimeSpan.FromSeconds(AiProviderSettings.HttpTimeoutSeconds);
                    _http.DefaultRequestHeaders.Accept.Clear();
                    _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }
                return _http;
            }
        }
    
        public async Task<string> GeneratePlanAsync(string userPrompt, CancellationToken ct)
        {
            string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("Missing GEMINI_API_KEY environment variable.");
    
            string model = AiProviderSettings.GeminiModel;
    
            ct.ThrowIfCancellationRequested();
    
            string system = OpenAIJsonPlanClientSchema.SystemPrompt;
    
            var body = new JObject
            {
                ["systemInstruction"] = new JObject
                {
                    ["parts"] = new JArray(
                        new JObject { ["text"] = system }
                    )
                },
                ["contents"] = new JArray(
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray(
                            new JObject { ["text"] = userPrompt }
                        )
                    }
                ),
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = "application/json"
                }
            }.ToString();
    
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
    
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            req.Headers.Add("x-goog-api-key", apiKey);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
    
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
            string respText = await resp.Content.ReadAsStringAsync();
    
            ct.ThrowIfCancellationRequested();
    
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Gemini error {(int)resp.StatusCode}: {respText}");
    
            string planJson = ExtractCandidateText(respText);
    
            if (string.IsNullOrWhiteSpace(planJson))
                throw new Exception("Gemini response did not contain plan JSON.");
    
            return planJson.Trim();
        }
    
        public async Task<AgentResponse> GenerateAgentResponseAsync(
            List<AgentMessage> history,
            List<AgentTool> tools,
            CancellationToken ct)
        {
            string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("Missing GEMINI_API_KEY environment variable.");
    
            string model = AiProviderSettings.GeminiModel;
            ct.ThrowIfCancellationRequested();
    
            // Build contents from history
            var contents = new JArray();
            string systemText = null;
    
            foreach (var msg in history)
            {
                switch (msg.role)
                {
                    case AgentRole.System:
                        systemText = msg.content;
                        break;
                    case AgentRole.User:
                        contents.Add(new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray(new JObject { ["text"] = msg.content })
                        });
                        break;
                    case AgentRole.Assistant:
                        if (!string.IsNullOrEmpty(msg.content))
                        {
                            contents.Add(new JObject
                            {
                                    ["role"] = "model",
                                ["parts"] = new JArray(new JObject { ["text"] = msg.content })
                            });
                        }
                        else if (!string.IsNullOrEmpty(msg.toolName))
                        {
                            contents.Add(new JObject
                            {
                                ["role"] = "model",
                                ["parts"] = new JArray(new JObject
                                {
                                    ["functionCall"] = new JObject
                                    {
                                        ["name"] = msg.toolName,
                                        ["args"] = JToken.Parse(msg.toolArgs ?? "{}")
                                    }
                                })
                            });
                        }
                        break;
                    case AgentRole.Tool:
                        contents.Add(new JObject
                        {
                            ["role"] = "function",
                            ["parts"] = new JArray(new JObject
                            {
                                ["functionResponse"] = new JObject
                                {
                                    ["name"] = msg.toolName ?? "unknown",
                                    ["response"] = new JObject
                                    {
                                        ["content"] = msg.content ?? ""
                                    }
                                }
                            })
                        });
                        break;
                }
            }
    
            // Build tool declarations
            var toolDeclarations = new JArray();
            if (tools != null)
            {
                foreach (var tool in tools)
                {
                    toolDeclarations.Add(new JObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = JToken.Parse(tool.ParameterSchema ?? "{\"type\":\"object\",\"properties\":{}}")
                    });
                }
            }
    
            var bodyObj = new JObject
            {
                ["contents"] = contents,
                ["generationConfig"] = new JObject
                {
                    ["responseMimeType"] = "text/plain"
                }
            };
    
            if (!string.IsNullOrEmpty(systemText))
            {
                bodyObj["systemInstruction"] = new JObject
                {
                    ["parts"] = new JArray(new JObject { ["text"] = systemText })
                };
            }
    
            if (toolDeclarations.Count > 0)
            {
                bodyObj["tools"] = new JArray(new JObject
                {
                    ["functionDeclarations"] = toolDeclarations
                });
            }
    
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
    
            using var httpReq = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpReq.Headers.Add("x-goog-api-key", apiKey);
            httpReq.Content = new StringContent(bodyObj.ToString(), Encoding.UTF8, "application/json");
    
            using var resp = await Http.SendAsync(httpReq, HttpCompletionOption.ResponseContentRead, ct);
            string respText = await resp.Content.ReadAsStringAsync();
    
            ct.ThrowIfCancellationRequested();
    
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Gemini error {(int)resp.StatusCode}: {respText}");
    
            return ParseAgentResponse(respText);
        }
    
        private static AgentResponse ParseAgentResponse(string payload)
        {
            var root = JToken.Parse(payload);
            var response = new AgentResponse();
    
            var usageMeta = root["usageMetadata"];
            if (usageMeta != null)
            {
                response.PromptTokens = usageMeta["promptTokenCount"]?.Value<int>() ?? 0;
                response.CompletionTokens = usageMeta["candidatesTokenCount"]?.Value<int>() ?? 0;
            }
    
            var parts = root["candidates"]?[0]?["content"]?["parts"] as JArray;
            if (parts == null || parts.Count == 0)
            {
                response.TextContent = "";
                return response;
            }
    
            var toolCalls = new List<AgentToolCall>();
            var textParts = new StringBuilder();
    
            foreach (var part in parts)
            {
                var functionCall = part["functionCall"];
                if (functionCall != null)
                {
                    toolCalls.Add(new AgentToolCall
                    {
                        CallId = Guid.NewGuid().ToString("N").Substring(0, 8),
                        ToolName = functionCall["name"]?.Value<string>() ?? "",
                        Arguments = functionCall["args"] as JObject ?? new JObject()
                    });
                }
                else
                {
                    var text = part["text"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(text))
                        textParts.Append(text);
                }
            }
    
            if (toolCalls.Count > 0)
                response.ToolCalls = toolCalls;
            else
                response.TextContent = textParts.ToString();
    
            return response;
        }
    
        private static string ExtractCandidateText(string payload)
        {
            var root = JToken.Parse(payload);
            var parts = root["candidates"]?[0]?["content"]?["parts"] as JArray;
            if (parts == null || parts.Count == 0) return null;
    
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                var t = p["text"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(t))
                    sb.Append(t);
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }
    }
}
