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
    
    public static class OpenAIJsonPlanClient
    {
        private const string ChatEndpoint = "https://api.openai.com/v1/chat/completions";
    
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
    
        // ──────────────────── PLAN GENERATION (STRUCTURED OUTPUTS) ────────────────────
    
        public static async Task<string> GeneratePlanAsync(string prompt, CancellationToken cancellationToken)
        {
            string apiKey = AiProviderSettings.OpenAIKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("Missing OpenAI API Key in Settings.");
    
            string model = AiProviderSettings.OpenAIModel;
            cancellationToken.ThrowIfCancellationRequested();
    
            string system = OpenAIJsonPlanClientSchema.SystemPrompt;
            string body = BuildStructuredOutputRequestBody(model, system, prompt);
    
            using var request = new HttpRequestMessage(HttpMethod.Post, ChatEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
    
            using var resp = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            string respText = await resp.Content.ReadAsStringAsync();
    
            cancellationToken.ThrowIfCancellationRequested();
    
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"OpenAI error {(int)resp.StatusCode}: {respText}");
    
            string planJson = ExtractPlanJsonFromChatResponse(respText);
    
            if (string.IsNullOrWhiteSpace(planJson))
                throw new Exception("Model response did not contain a valid JSON plan.");
    
            return planJson.Trim();
        }
    
        // ──────────────────── AGENTIC FUNCTION CALLING ────────────────────
    
        public static async Task<AgentResponse> GenerateAgentResponseAsync(
            List<AgentMessage> history,
            List<AgentTool> tools,
            CancellationToken ct)
        {
            string apiKey = AiProviderSettings.OpenAIKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("Missing OpenAI API Key in Settings.");
    
            string model = AiProviderSettings.OpenAIModel;
            ct.ThrowIfCancellationRequested();
    
            // Build messages
            var messages = new JArray();
            foreach (var msg in history)
            {
                switch (msg.role)
                {
                    case AgentRole.System:
                        messages.Add(new JObject { ["role"] = "system", ["content"] = msg.content });
                        break;
                    case AgentRole.User:
                        messages.Add(new JObject { ["role"] = "user", ["content"] = msg.content });
                        break;
                    case AgentRole.Assistant:
                        if (!string.IsNullOrEmpty(msg.toolName))
                        {
                            messages.Add(new JObject
                            {
                                ["role"] = "assistant",
                                ["content"] = (string)null,
                                ["tool_calls"] = new JArray(new JObject
                                {
                                    ["id"] = msg.toolCallId ?? "call_0",
                                    ["type"] = "function",
                                    ["function"] = new JObject
                                    {
                                        ["name"] = msg.toolName,
                                        ["arguments"] = msg.toolArgs ?? "{}"
                                    }
                                })
                            });
                        }
                        else
                        {
                            messages.Add(new JObject { ["role"] = "assistant", ["content"] = msg.content ?? "" });
                        }
                        break;
                    case AgentRole.Tool:
                        messages.Add(new JObject
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = msg.toolCallId ?? "call_0",
                            ["content"] = msg.content ?? ""
                        });
                        break;
                }
            }
    
            var bodyObj = new JObject
            {
                ["model"] = model,
                ["messages"] = messages,
                ["temperature"] = 0
            };
    
            if (tools != null && tools.Count > 0)
            {
                var toolsArray = new JArray();
                foreach (var tool in tools)
                {
                    toolsArray.Add(new JObject
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
                bodyObj["tools"] = toolsArray;
                bodyObj["tool_choice"] = "auto";
            }
    
            using var req = new HttpRequestMessage(HttpMethod.Post, ChatEndpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = new StringContent(bodyObj.ToString(), Encoding.UTF8, "application/json");
    
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
            string respText = await resp.Content.ReadAsStringAsync();
    
            ct.ThrowIfCancellationRequested();
    
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"OpenAI error {(int)resp.StatusCode}: {respText}");
    
            // Reuse Groq's parser since Groq uses OpenAI-compatible format
            return GroqPlanClient.ParseOpenAICompatibleAgentResponse(respText);
        }
    
        // ──────────────────── HELPERS ────────────────────
    
        private static string ExtractPlanJsonFromChatResponse(string respText)
        {
            var root = JToken.Parse(respText);
            var content = root["choices"]?[0]?["message"]?["content"]?.Value<string>();
            return content;
        }
    
        private static string BuildStructuredOutputRequestBody(string model, string system, string userPrompt)
        {
            var body = new JObject
            {
                ["model"] = model,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = system },
                    new JObject { ["role"] = "user", ["content"] = userPrompt }
                },
                ["response_format"] = new JObject
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new JObject
                    {
                        ["name"] = "unity_game_plan",
                        ["strict"] = true,
                        ["schema"] = JToken.Parse(OpenAIJsonPlanClientSchema.SchemaJson)
                    }
                },
                ["temperature"] = 0
            };
    
            return body.ToString();
        }
    }
}
