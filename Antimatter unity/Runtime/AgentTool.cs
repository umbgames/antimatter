using System;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    public class AgentTool
    {
        public string Name;
        public string Description;
        public string ParameterSchema; // JSON Schema string for parameters
        public Func<JObject, AgentToolResult> Execute;
    }

    public class AgentToolResult
    {
        public bool Success;
        public string Output;   // text returned to the AI
        public string Error;     // error message if failed

        public static AgentToolResult Ok(string output) => new AgentToolResult
        {
            Success = true,
            Output = output
        };

        public static AgentToolResult Fail(string error) => new AgentToolResult
        {
            Success = false,
            Error = error,
            Output = "ERROR: " + error
        };

        public override string ToString() => Success ? Output : $"ERROR: {Error}";
    }

    public class AgentToolCall
    {
        public string CallId;
        public string ToolName;
        public JObject Arguments;
    }

    public class AgentResponse
    {
        public string TextContent;              // non-null if agent replied with text
        public System.Collections.Generic.List<AgentToolCall> ToolCalls; // non-null if agent wants to call tools
        public int PromptTokens;
        public int CompletionTokens;
        
        public float CreditsConsumed;
        public float UnityCreditBalance;

        public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;
        public bool HasText => !string.IsNullOrEmpty(TextContent);
    }
}
