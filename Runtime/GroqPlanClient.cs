// Assets/Editor/GroqPlanClient.cs
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
    
    public sealed class GroqPlanClient : IGamePlanClient
    {
        private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
    
        private static HttpClient _http;
    
        private static HttpClient Http
        {
            get
            {
                if (_http == null)
                {
                    _http = new HttpClient();
                    _http.Timeout = TimeSpan.FromSeconds(AiProviderSettings.HttpTimeoutSeconds);
                    _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }
                return _http;
            }
        }
    
        public async Task<string> GeneratePlanAsync(string userPrompt, CancellationToken ct)
        {
            string apiKey = AiProviderSettings.GroqKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("Missing Groq API Key in Settings.");
    
            string model = AiProviderSettings.GroqModel;
            ct.ThrowIfCancellationRequested();
    
            // COMPACT PROMPT FOR GROQ (Reduces token usage and payload size)
            string compactSystem =
                "You are a Unity editor planner. Output ONLY a raw JSON object matching the standard schema.\n" +
                "REQUIREMENTS:\n" +
                "- Include: version, settings, assets (materials/terrainData), scenes (with objects), scripts, flow.\n" +
                "- Every object needs: id, kind, position, rotationEuler, scale, components.\n" +
                "- For FPS levels, include marker objects: LM_ArenaCenter, LM_ArenaSize, LM_PlayerSpawn, LM_MidHotspot, etc.\n" +
                "- Output NO text or markdown. Just the JSON.\n\n" +
                "SKELETON:\n" +
                "{\"version\":\"1.0\",\"planExplanation\":\"Explain what you did here.\",\"planSuggestions\":\"Any suggestions for the user?\",\"settings\":{\"ensureCameraAndLight\":true,\"worldGen\":{\"enabled\":false,\"terrainPreset\":\"Hills\",\"roadHubs\":[],\"settlements\":[]}}," +
                "\"assets\":{\"materials\":[],\"terrainData\":[]},\"scenes\":[{\"name\":\"MainScene\",\"objects\":[]}],\"scripts\":[],\"flow\":{\"restartKey\":\"R\",\"nextSceneOnTrigger\":false}}";
    
            var body = new JObject
            {
                ["model"] = model,
                ["temperature"] = 0,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = compactSystem },
                    new JObject { ["role"] = "user",   ["content"] = userPrompt }
                }
            };
    
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
    
            var (ok, respText) = await SendWithRetryAsync(req, ct);
            if (!ok) throw new Exception(respText);
    
            return ExtractJsonFromGroqResponse(respText);
        }
    
        // ── Shared 429 retry helper ──────────────────────────────────────
        private static async Task<(bool ok, string body)> SendWithRetryAsync(
            HttpRequestMessage req, CancellationToken ct)
        {
            const int MaxRetries = 5;
            string apiKey = req.Headers.Authorization?.Parameter ?? "";
    
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                
                HttpResponseMessage resp = null;
                string body = "";
                bool success = false;
                int statusCode = 0;
    
                try
                {
                    using var clone = CloneRequest(req, apiKey);
                    resp = await Http.SendAsync(clone, HttpCompletionOption.ResponseContentRead, ct);
                    body = await resp.Content.ReadAsStringAsync();
                    success = resp.IsSuccessStatusCode;
                    statusCode = (int)resp.StatusCode;
                }
                catch (HttpRequestException ex)
                {
                    if (attempt == MaxRetries - 1)
                        return (false, $"Network error after {MaxRetries} retries: {ex.Message} (Inner: {ex.InnerException?.Message})");
                    
                    UnityEngine.Debug.LogWarning($"[Groq] Network error: {ex.Message}. Retrying...");
                    await Task.Delay(2000, ct);
                    continue;
                }
                catch (TaskCanceledException)
                {
                    if (ct.IsCancellationRequested) throw;
                    if (attempt == MaxRetries - 1)
                        return (false, $"[Groq] Request timed out after {MaxRetries} retries.");
    
                    UnityEngine.Debug.LogWarning("[Groq] Request timed out. Retrying...");
                    await Task.Delay(2000, ct);
                    continue;
                }
                finally
                {
                    resp?.Dispose();
                }
    
                if (success)
                    return (true, body);
    
                if (statusCode == 429)
                {
                    double waitSec = ParseRetryAfterSeconds(body);
                    if (waitSec <= 0) waitSec = 20;
                    waitSec = System.Math.Min(waitSec + 1, 60);
    
                    UnityEngine.Debug.LogWarning(
                        $"Groq 429 rate limit. Waiting {waitSec:F1}s then retrying (attempt {attempt + 1}/{MaxRetries})...");
    
                    await Task.Delay(TimeSpan.FromSeconds(waitSec), ct);
                    continue;
                }
    
                // Non-retryable error
                return (false, $"Groq error {statusCode}: {body}");
            }
    
            return (false, "Groq: max retries exceeded.");
        }
    
        private static double ParseRetryAfterSeconds(string body)
        {
            // "Please try again in 15.626s"
            const string marker = "try again in ";
            int idx = body.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return 20;
            idx += marker.Length;
            int end = body.IndexOfAny(new[] { 's', ' ', '"', '.' }, idx + 1);
            if (end < 0) end = System.Math.Min(idx + 6, body.Length);
            string numStr = body.Substring(idx, end - idx).Trim();
            if (double.TryParse(numStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double secs))
                return secs;
            return 20;
        }
    
        private static HttpRequestMessage CloneRequest(HttpRequestMessage original, string apiKey)
        {
            var clone = new HttpRequestMessage(original.Method, original.RequestUri);
            clone.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            if (original.Content != null)
            {
                // Re-read original content bytes synchronously — safe because we already sent it
                var bytes = original.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                clone.Content = new ByteArrayContent(bytes);
                clone.Content.Headers.ContentType = original.Content.Headers.ContentType;
            }
            return clone;
        }
    
        public async Task<AgentResponse> GenerateAgentResponseAsync(
            List<AgentMessage> history,
            List<AgentTool> tools,
            CancellationToken ct)
        {
            string apiKey = AiProviderSettings.GroqKey;
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("Missing Groq API Key in Settings.");
    
            string model = AiProviderSettings.GroqModel;
            ct.ThrowIfCancellationRequested();
    
            // First attempt: try native tool calling
            var nativeMessages = BuildMessages(history);
            var bodyObj = new JObject
            {
                ["model"] = model,
                ["messages"] = nativeMessages,
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
    
            using var req1 = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req1.Content = new StringContent(bodyObj.ToString(), Encoding.UTF8, "application/json");
    
            var (ok1, respText1) = await SendWithRetryAsync(req1, ct);
    
            // If native tools succeeded, parse normally
            if (ok1)
                return ParseOpenAICompatibleAgentResponse(respText1);
    
            // If error is NOT tool-calling related (400), just throw
            bool isToolError = respText1.Contains("400") && 
                (respText1.Contains("tool calling") || respText1.Contains("tool_use") || respText1.Contains("tools"));
    
            if (!isToolError)
                throw new Exception(respText1);
    
            // ── Fallback: text-based tool call protocol ────────────────────────
            // ... (rest of the manual instructions)
            var toolNames = tools != null ? string.Join(", ", tools.ConvertAll(t => t.Name)) : "";
            var toolSchemas = new System.Text.StringBuilder();
            if (tools != null)
            {
                foreach (var t in tools)
                    toolSchemas.AppendLine($"- {t.Name}: {t.Description}");
            }
    
            string toolInstruction =
                "\n\nYou cannot use native function calling. Instead, to call a tool output ONLY a JSON object in this exact format:\n" +
                "{\"tool_call\":{\"name\":\"<tool_name>\",\"arguments\":{<args>}}}\n" +
                "To give a final text answer instead, output plain text.\n" +
                "Available tools:\n" + toolSchemas;
    
            // Rebuild messages, appending tool instruction to the system message
            var fallbackMessages = new JArray();
            bool systemPatched = false;
            foreach (var msg in history)
            {
                if (msg.role == AgentRole.System && !systemPatched)
                {
                    fallbackMessages.Add(new JObject { ["role"] = "system", ["content"] = msg.content + toolInstruction });
                    systemPatched = true;
                }
                else if (msg.role == AgentRole.User)
                    fallbackMessages.Add(new JObject { ["role"] = "user", ["content"] = msg.content });
                else if (msg.role == AgentRole.Assistant)
                    fallbackMessages.Add(new JObject { ["role"] = "assistant", ["content"] = msg.content ?? "" });
                else if (msg.role == AgentRole.Tool)
                    fallbackMessages.Add(new JObject { ["role"] = "user", ["content"] = $"[Tool result for {msg.toolName}]: {msg.content}" });
            }
    
            var fallbackBody = new JObject
            {
                ["model"] = model,
                ["messages"] = fallbackMessages,
                ["temperature"] = 0
            };
    
            using var req2 = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req2.Content = new StringContent(fallbackBody.ToString(), Encoding.UTF8, "application/json");
    
            var (ok2, respText2) = await SendWithRetryAsync(req2, ct);
            if (!ok2) throw new Exception(respText2);
    
            return ParseTextBasedToolResponse(respText2);
        }
    
        private static JArray BuildMessages(List<AgentMessage> history)
        {
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
                                        ["name"] = string.IsNullOrWhiteSpace(msg.toolName) ? "unknown_tool" : msg.toolName,
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
                            ["name"] = string.IsNullOrWhiteSpace(msg.toolName) ? "unknown_tool" : msg.toolName,
                            ["tool_call_id"] = msg.toolCallId ?? "call_0",
                            ["content"] = msg.content ?? ""
                        });
                        break;
                }
            }
            return messages;
        }
    
        private static AgentResponse ParseTextBasedToolResponse(string json)
        {
            var root = JToken.Parse(json);
            var response = new AgentResponse();
    
            var usage = root["usage"];
            if (usage != null)
            {
                response.PromptTokens = usage["prompt_tokens"]?.Value<int>() ?? 0;
                response.CompletionTokens = usage["completion_tokens"]?.Value<int>() ?? 0;
            }
    
            string content = root["choices"]?[0]?["message"]?["content"]?.Value<string>() ?? "";
    
            // Try to parse text-based tool call: {"tool_call":{"name":"...","arguments":{...}}}
            int braceIdx = content.IndexOf('{');
            if (braceIdx >= 0)
            {
                try
                {
                    string jsonPart = content.Substring(braceIdx);
                    var parsed = JObject.Parse(jsonPart);
                    var tc = parsed["tool_call"];
                    if (tc != null)
                    {
                        string toolName = tc["name"]?.Value<string>() ?? "";
                        var args = tc["arguments"] as JObject ?? new JObject();
                        response.ToolCalls = new List<AgentToolCall>
                        {
                            new AgentToolCall
                            {
                                CallId = "text_call_" + Guid.NewGuid().ToString("N").Substring(0, 6),
                                ToolName = toolName,
                                Arguments = args
                            }
                        };
                        return response;
                    }
                }
                catch { /* Not a valid tool call JSON — treat as plain text */ }
            }
    
            response.TextContent = content;
            return response;
        }
    
        internal static AgentResponse ParseOpenAICompatibleAgentResponse(string json)
        {
            var root = JToken.Parse(json);
            var response = new AgentResponse();
    
            var usage = root["usage"];
            if (usage != null)
            {
                response.PromptTokens = usage["prompt_tokens"]?.Value<int>() ?? 0;
                response.CompletionTokens = usage["completion_tokens"]?.Value<int>() ?? 0;
            }
    
            var choice = root["choices"]?[0];
            if (choice == null)
            {
                response.TextContent = "";
                return response;
            }
    
            var message = choice["message"];
            if (message == null)
            {
                response.TextContent = "";
                return response;
            }
    
            // Check for tool calls
            var toolCallsToken = message["tool_calls"] as JArray;
            if (toolCallsToken != null && toolCallsToken.Count > 0)
            {
                response.ToolCalls = new List<AgentToolCall>();
                foreach (var tc in toolCallsToken)
                {
                    response.ToolCalls.Add(new AgentToolCall
                    {
                        CallId = tc["id"]?.Value<string>() ?? Guid.NewGuid().ToString("N").Substring(0, 8),
                        ToolName = tc["function"]?["name"]?.Value<string>() ?? "",
                        Arguments = JObject.Parse(tc["function"]?["arguments"]?.Value<string>() ?? "{}")
                    });
                }
            }
            else
            {
                response.TextContent = message["content"]?.Value<string>() ?? "";
            }
    
            return response;
        }
    
        private static string ExtractJsonFromGroqResponse(string json)
        {
            var root = JToken.Parse(json);
            var content = root["choices"]?[0]?["message"]?["content"]?.Value<string>();
    
            if (string.IsNullOrWhiteSpace(content))
                throw new Exception("Groq response missing message content.");
    
            int start = content.IndexOf('{');
            if (start < 0)
                throw new Exception("Groq response does not contain JSON.");
    
            int depth = 0;
            bool inString = false;
            bool esc = false;
    
            for (int i = start; i < content.Length; i++)
            {
                char c = content[i];
    
                if (inString)
                {
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
    
                if (c == '"') { inString = true; continue; }
    
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return content.Substring(start, i - start + 1);
                }
            }
    
            throw new Exception("Failed to extract JSON object from Groq output.");
        }
    }
}
