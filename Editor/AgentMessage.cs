// Assets/Editor/AgentMessage.cs
using System;

public enum AgentRole
{
    System,
    User,
    Assistant,
    Tool
}

[Serializable]
public class AgentMessage
{
    public AgentRole role;
    public string content;
    public string toolCallId;   // for tool result messages & assistant tool-call messages
    public string toolName;     // for tool call / tool result messages
    public string toolArgs;     // JSON string of arguments (for assistant tool-call messages)
    public long timestampTicks; // DateTime.UtcNow.Ticks for serialization

    public DateTime Timestamp
    {
        get => new DateTime(timestampTicks, DateTimeKind.Utc);
        set => timestampTicks = value.Ticks;
    }

    public AgentMessage() { }

    public static AgentMessage System(string content) => new AgentMessage
    {
        role = AgentRole.System,
        content = content,
        Timestamp = DateTime.UtcNow
    };

    public static AgentMessage User(string content) => new AgentMessage
    {
        role = AgentRole.User,
        content = content,
        Timestamp = DateTime.UtcNow
    };

    public static AgentMessage AssistantText(string content) => new AgentMessage
    {
        role = AgentRole.Assistant,
        content = content,
        Timestamp = DateTime.UtcNow
    };

    public static AgentMessage AssistantToolCall(string callId, string toolName, string argsJson) => new AgentMessage
    {
        role = AgentRole.Assistant,
        toolCallId = callId,
        toolName = toolName,
        toolArgs = argsJson,
        Timestamp = DateTime.UtcNow
    };

    public static AgentMessage ToolResult(string callId, string toolName, string resultContent) => new AgentMessage
    {
        role = AgentRole.Tool,
        toolCallId = callId,
        toolName = toolName,
        content = resultContent,
        Timestamp = DateTime.UtcNow
    };

    public bool IsToolCall => role == AgentRole.Assistant && !string.IsNullOrEmpty(toolName);
}
