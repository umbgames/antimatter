// Assets/Editor/AgentHistory.cs
using UnityEditor;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages persistent conversation history stored in EditorPrefs.
/// Capped at a max message count to avoid EditorPrefs overflow.
/// </summary>
public static class AgentHistory
{
    private const int MaxStoredMessages = 60;

    [Serializable]
    private class MessageList
    {
        public List<AgentMessage> messages = new List<AgentMessage>();
    }

    public static List<AgentMessage> Load()
    {
        string json = EditorPrefs.GetString(AiPrefsKeys.AgentHistory, "");
        if (string.IsNullOrWhiteSpace(json))
            return new List<AgentMessage>();

        try
        {
            var list = UnityEngine.JsonUtility.FromJson<MessageList>(json);
            return list?.messages ?? new List<AgentMessage>();
        }
        catch
        {
            return new List<AgentMessage>();
        }
    }

    public static void Save(List<AgentMessage> messages)
    {
        if (messages == null) messages = new List<AgentMessage>();

        // Cap to avoid EditorPrefs overflow
        if (messages.Count > MaxStoredMessages)
            messages = messages.GetRange(messages.Count - MaxStoredMessages, MaxStoredMessages);

        var list = new MessageList { messages = messages };
        string json = UnityEngine.JsonUtility.ToJson(list);
        EditorPrefs.SetString(AiPrefsKeys.AgentHistory, json);
    }

    public static void Clear()
    {
        EditorPrefs.SetString(AiPrefsKeys.AgentHistory, "");
    }

    public static void Append(AgentMessage msg)
    {
        var messages = Load();
        messages.Add(msg);
        Save(messages);
    }
}
