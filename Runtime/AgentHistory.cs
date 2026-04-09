using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Antimatter
{
    /// <summary>
    /// Manages persistent conversation history stored in EditorPrefs.
    /// Capped at a max message count to avoid EditorPrefs overflow.
    /// </summary>
    public static class AgentHistory
    {
        private const int MaxStoredMessages = 200; // Increased limit since we are using disk

        public static string BaseSessionsPath => System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "AgentSessions");

        [Serializable]
        public class SessionManifest
        {
            public string id;
            public string name;
            public string lastUpdated;
        }

        [Serializable]
        private class MessageList
        {
            public string sessionName;
            public List<AgentMessage> messages = new List<AgentMessage>();
        }

        public static string CurrentSessionId
        {
            get => EditorPrefs.GetString(AiPrefsKeys.AgentHistory + "_currentSession", "default");
            set => EditorPrefs.SetString(AiPrefsKeys.AgentHistory + "_currentSession", value);
        }

        public static List<SessionManifest> GetAllSessions()
        {
            var list = new List<SessionManifest>();
            if (!System.IO.Directory.Exists(BaseSessionsPath)) return list;

            var files = System.IO.Directory.GetFiles(BaseSessionsPath, "*.json");
            foreach (var f in files)
            {
                string id = System.IO.Path.GetFileNameWithoutExtension(f);
                string name = id;
                try
                {
                    string json = System.IO.File.ReadAllText(f);
                    var data = UnityEngine.JsonUtility.FromJson<MessageList>(json);
                    if (!string.IsNullOrEmpty(data.sessionName)) name = data.sessionName;
                }
                catch { }

                list.Add(new SessionManifest { id = id, name = name, lastUpdated = System.IO.File.GetLastWriteTime(f).ToString("g") });
            }
            list.Sort((a, b) => DateTime.Parse(b.lastUpdated).CompareTo(DateTime.Parse(a.lastUpdated)));
            return list;
        }

        public static void CreateNewSession()
        {
            string id = "Session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            CurrentSessionId = id;
            SaveSession(id, new List<AgentMessage>(), id);
        }

        public static void DeleteSession(string id)
        {
            string path = System.IO.Path.Combine(BaseSessionsPath, $"{id}.json");
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
            if (CurrentSessionId == id)
                CreateNewSession();
        }

        public static List<AgentMessage> Load()
        {
            return LoadSession(CurrentSessionId);
        }

        public static List<AgentMessage> LoadSession(string id)
        {
#if UNITY_EDITOR
            if (!System.IO.Directory.Exists(BaseSessionsPath))
                System.IO.Directory.CreateDirectory(BaseSessionsPath);

            string path = System.IO.Path.Combine(BaseSessionsPath, $"{id}.json");
            if (!System.IO.File.Exists(path))
                return new List<AgentMessage>();

            try
            {
                string json = System.IO.File.ReadAllText(path);
                var list = UnityEngine.JsonUtility.FromJson<MessageList>(json);
                return list?.messages ?? new List<AgentMessage>();
            }
            catch
            {
                return new List<AgentMessage>();
            }
#else
            return new List<AgentMessage>();
#endif
        }

        public static void Save(List<AgentMessage> messages)
        {
            // Try to preserve existing name
            string name = CurrentSessionId;
            try
            {
                string path = System.IO.Path.Combine(BaseSessionsPath, $"{CurrentSessionId}.json");
                if (System.IO.File.Exists(path))
                {
                    var data = UnityEngine.JsonUtility.FromJson<MessageList>(System.IO.File.ReadAllText(path));
                    if (!string.IsNullOrEmpty(data.sessionName)) name = data.sessionName;
                }
            }
            catch { }

            SaveSession(CurrentSessionId, messages, name);
        }

        public static void SaveSession(string id, List<AgentMessage> messages, string sessionName)
        {
#if UNITY_EDITOR
            if (!System.IO.Directory.Exists(BaseSessionsPath))
                System.IO.Directory.CreateDirectory(BaseSessionsPath);

            if (messages == null) messages = new List<AgentMessage>();

            if (messages.Count > MaxStoredMessages)
                messages = messages.GetRange(messages.Count - MaxStoredMessages, MaxStoredMessages);

            var list = new MessageList { messages = messages, sessionName = sessionName };
            string json = UnityEngine.JsonUtility.ToJson(list);
            
            string path = System.IO.Path.Combine(BaseSessionsPath, $"{id}.json");
            System.IO.File.WriteAllText(path, json);
#endif
        }

        public static void RenameSession(string id, string newName)
        {
            var msgs = LoadSession(id);
            SaveSession(id, msgs, newName);
        }

        public static void Clear()
        {
            Save(new List<AgentMessage>());
        }

        public static void Append(AgentMessage msg)
        {
            var messages = Load();
            messages.Add(msg);
            Save(messages);
        }
    }
}
