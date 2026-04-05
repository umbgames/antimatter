using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public static class OpenAIJsonPlanClient
{
    private const string PlanEndpoint = "https://api.openai.com/v1/responses";
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

    // ──────────────────── LEGACY PLAN GENERATION ────────────────────

    public static async Task<string> GeneratePlanAsync(string prompt, CancellationToken cancellationToken)
    {
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception("Missing OPENAI_API_KEY environment variable.");

        string model = AiProviderSettings.OpenAIModel;

        cancellationToken.ThrowIfCancellationRequested();

        string system = BuildSystemPrompt();
        string body = BuildRequestBody(model, system, prompt);

        using var request = new HttpRequestMessage(HttpMethod.Post, PlanEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        string respText = await resp.Content.ReadAsStringAsync();

        cancellationToken.ThrowIfCancellationRequested();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"OpenAI error {(int)resp.StatusCode}: {respText}");

        string planJson = ExtractPlanJsonFromResponsesPayload(respText);

        if (string.IsNullOrWhiteSpace(planJson))
            throw new Exception("Model response did not contain a JSON plan.");

        return planJson.Trim();
    }

    // ──────────────────── AGENTIC FUNCTION CALLING ────────────────────

    public static async Task<AgentResponse> GenerateAgentResponseAsync(
        List<AgentMessage> history,
        List<AgentTool> tools,
        CancellationToken ct)
    {
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception("Missing OPENAI_API_KEY environment variable.");

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

    private static string ExtractPlanJsonFromResponsesPayload(string responsesPayload)
    {
        var root = JToken.Parse(responsesPayload);

        var outputText = root["output_text"]?.Value<string>();
        if (!string.IsNullOrWhiteSpace(outputText))
            return outputText;

        var output = root["output"] as JArray;
        if (output == null) return null;

        var sb = new StringBuilder();
        foreach (var item in output)
        {
            var content = item["content"] as JArray;
            if (content == null) continue;

            foreach (var c in content)
            {
                var text = c["text"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(text))
                    sb.Append(text);
            }
        }

        var combined = sb.ToString();
        return string.IsNullOrWhiteSpace(combined) ? null : combined;
    }

    private static string BuildSystemPrompt()
    {
        return
@"You are a Unity game-generation planner.
Return ONLY valid JSON that matches the provided JSON Schema. No markdown. No comments. No extra keys.

Constraints:
- AI must not control Unity. Only produce a deterministic JSON plan.
- Use only primitive placeholder objects: Cube, Sphere, Capsule.
- Every object must have a unique id.
- Scripts must be valid C# MonoBehaviour code with NO UnityEditor usage.
- Prefer simple, stable runtime logic (SceneManager ok).
- Do not assume tags/layers exist unless you also include creation steps (avoid requiring tags).
- Properties must be typed using entries: { name, type, floatValue/intValue/boolValue/stringValue }.
- When editing, reuse existing object ids. Only introduce new ids for new objects.
";
    }

    private static string BuildRequestBody(string model, string system, string userPrompt)
    {
        string sysEsc = JsonEscape(system);
        string userEsc = JsonEscape(userPrompt);

        return $@"
{{
  ""model"": ""{model}"",
  ""input"": [
    {{
      ""role"": ""system"",
      ""content"": [
        {{ ""type"": ""input_text"", ""text"": ""{sysEsc}"" }}
      ]
    }},
    {{
      ""role"": ""user"",
      ""content"": [
        {{ ""type"": ""input_text"", ""text"": ""{userEsc}"" }}
      ]
    }}
  ],
  ""text"": {{
    ""format"": {{
      ""type"": ""json_schema"",
      ""name"": ""unity_game_plan"",
      ""strict"": true,
      ""schema"": {GamePlanJsonSchema()}
    }}
  }}
}}".Trim();
    }

    private static string GamePlanJsonSchema()
    {
        return @"
{
  ""type"": ""object"",
  ""additionalProperties"": false,
  ""required"": [""version"", ""planExplanation"", ""planSuggestions"", ""scenes"", ""scripts"", ""flow""],
  ""properties"": {
    ""version"": { ""type"": ""string"" },
    ""planExplanation"": { ""type"": ""string"" },
    ""planSuggestions"": { ""type"": ""string"" },
    ""scenes"": {
      ""type"": ""array"",
      ""minItems"": 1,
      ""items"": { ""$ref"": ""#/definitions/SceneDefinition"" }
    },
    ""scripts"": {
      ""type"": ""array"",
      ""items"": { ""$ref"": ""#/definitions/ScriptDefinition"" }
    },
    ""flow"": { ""$ref"": ""#/definitions/FlowDefinition"" }
  },
  ""definitions"": {
    ""Vector3"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""x"", ""y"", ""z""],
      ""properties"": {
        ""x"": { ""type"": ""number"" },
        ""y"": { ""type"": ""number"" },
        ""z"": { ""type"": ""number"" }
      }
    },
    ""PropertyValue"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""name"", ""type"", ""floatValue"", ""intValue"", ""boolValue"", ""stringValue""],
      ""properties"": {
        ""name"": { ""type"": ""string"" },
        ""type"": { ""type"": ""string"", ""enum"": [""float"", ""int"", ""bool"", ""string""] },
        ""floatValue"": { ""type"": ""number"" },
        ""intValue"": { ""type"": ""integer"" },
        ""boolValue"": { ""type"": ""boolean"" },
        ""stringValue"": { ""type"": ""string"" }
      }
    },
    ""ComponentDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""script"", ""properties""],
      ""properties"": {
        ""script"": { ""type"": ""string"" },
        ""properties"": {
          ""type"": ""array"",
          ""items"": { ""$ref"": ""#/definitions/PropertyValue"" }
        }
      }
    },
    ""ObjectDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""id"", ""type"", ""position"", ""scale"", ""components""],
      ""properties"": {
        ""id"": { ""type"": ""string"" },
        ""type"": { ""type"": ""string"", ""enum"": [""Cube"", ""Sphere"", ""Capsule""] },
        ""position"": { ""$ref"": ""#/definitions/Vector3"" },
        ""scale"": { ""$ref"": ""#/definitions/Vector3"" },
        ""components"": {
          ""type"": ""array"",
          ""items"": { ""$ref"": ""#/definitions/ComponentDefinition"" }
        }
      }
    },
    ""SceneDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""name"", ""objects""],
      ""properties"": {
        ""name"": { ""type"": ""string"" },
        ""objects"": {
          ""type"": ""array"",
          ""items"": { ""$ref"": ""#/definitions/ObjectDefinition"" }
        }
      }
    },
    ""ScriptDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""name"", ""path"", ""code""],
      ""properties"": {
        ""name"": { ""type"": ""string"" },
        ""path"": { ""type"": ""string"" },
        ""code"": { ""type"": ""string"" }
      }
    },
    ""FlowDefinition"": {
      ""type"": ""object"",
      ""additionalProperties"": false,
      ""required"": [""restartKey"", ""nextSceneOnTrigger""],
      ""properties"": {
        ""restartKey"": { ""type"": ""string"" },
        ""nextSceneOnTrigger"": { ""type"": ""boolean"" }
      }
    }
  }
}
".Trim();
    }

    private static string JsonEscape(string s)
    {
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }
}
