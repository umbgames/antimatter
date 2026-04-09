// Assets/Editor/AgentWindow.cs
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Antimatter
{
    
    /// <summary>
    /// Full chat-style Unity EditorWindow for the AI Agent.
    /// Provides a conversation UI with tool call visualization, settings, and controls.
    /// </summary>
    public class AgentWindow : EditorWindow
    {
        // ── Orchestrator ──
        private AgentOrchestrator _orchestrator;
    
        // ── Settings (JSON Fallback) ──
        private bool _useJsonFallbackMode = false;
        private bool _editCurrentScene = false;
        private bool _levelGen = false;
        private GenerationConstraints _constraints = new GenerationConstraints();
    
        // ── UI State ──
        private string _inputText = "";
        private Vector2 _chatScroll;
        private Vector2 _settingsScroll;
        private bool _showSettings;
        private int _currentStep;
        private int _maxSteps;
        private string _statusText = "Idle";
        private bool _autoScroll = true;
        
        // ── Context Memory ──
        private List<UnityEngine.Object> _contextAssets = new List<UnityEngine.Object>();
        private bool _isRecordingVoice = false;
    
        // ── Display Messages ──
        private List<DisplayMessage> _displayMessages = new List<DisplayMessage>();
    
        // ── Styles (lazy init) ──
        private GUIStyle _userBubbleStyle;
        private GUIStyle _assistantBubbleStyle;
        private GUIStyle _toolBubbleStyle;
        private GUIStyle _systemBubbleStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _headerStyle;
        private bool _stylesInitialized;
    
        private class DisplayMessage
        {
            public AgentRole Role;
            public string Content;
            public string ToolName;
            public string ToolArgs;
            public bool IsToolCall;
            public bool IsExpanded;
            public DateTime Timestamp;
        }
    
        [MenuItem("Tools/Antimatter")]
        public static void Open()
        {
            var w = GetWindow<AgentWindow>("Antimatter");
            w.minSize = new Vector2(420, 500);
        }
    
        private void OnEnable()
        {
            _orchestrator = new AgentOrchestrator();
            _orchestrator.OnMessage += HandleMessage;
            _orchestrator.OnStepProgress += HandleStepProgress;
            _orchestrator.OnTokenUsage += HandleTokenUsage;
            _orchestrator.OnDone += HandleDone;
            _orchestrator.OnError += HandleError;
    
            EditorSceneManager.sceneOpened += OnSceneOpened;
    
            // Load persistent history
            var saved = AgentHistory.Load();
            if (saved.Count > 0)
            {
                _orchestrator.LoadHistory(saved);
                foreach (var msg in saved)
                    AddDisplayMessage(msg);
            }
        }

        private void SwitchSession(string sessionId)
        {
            if (_orchestrator != null && _orchestrator.IsRunning) return;
            AgentHistory.CurrentSessionId = sessionId;
            _orchestrator?.ClearHistory();
            _displayMessages.Clear();
            _statusText = "Session loaded.";
            
            var saved = AgentHistory.Load();
            if (saved.Count > 0)
            {
                _orchestrator?.LoadHistory(saved);
                foreach (var msg in saved)
                    AddDisplayMessage(msg);
            }
        }

        private void OnDisable()
        {
            _orchestrator?.Cancel();
            EditorSceneManager.sceneOpened -= OnSceneOpened;
    
            // Save history
            if (_orchestrator != null)
                AgentHistory.Save(_orchestrator.GetHistory());
        }
    
        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (_orchestrator != null)
            {
                var snap = SceneObserver.GetSnapshot();
                _orchestrator.InjectSystemMessage($"[System Event] Opened scene: {scene.name}\n" + SceneObserver.GetMinimalSummary(snap));
                _statusText = "Scene context sent to agent.";
                Repaint();
            }
        }
    
        private void InitStyles()
        {
            if (_stylesInitialized) return;
    
            _userBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(40, 8, 4, 4),
                fontSize = 12
            };
    
            _assistantBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(8, 40, 4, 4),
                fontSize = 12
            };
    
            _toolBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                wordWrap = true,
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(20, 20, 2, 2),
                fontSize = 11
            };
    
            _systemBubbleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(20, 20, 2, 2)
            };
    
            _inputStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                fontSize = 12,
                padding = new RectOffset(8, 8, 6, 6)
            };
    
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };
    
            _stylesInitialized = true;
        }
    
        private void OnGUI()
        {
            InitStyles();
    
            // ── Header ──
            DrawHeader();
    
            // ── Settings Panel ──
            if (_showSettings)
            {
                DrawSettingsPanel();
                return;
            }
    
            // ── Fixed layout: input+status anchored to bottom, chat fills the rest ──
            // Reserve fixed space at the bottom for input area (110px) + status bar (20px)
            const float inputAreaHeight = 110f;
            const float statusBarHeight = 20f;
            const float bottomReserved = inputAreaHeight + statusBarHeight;
    
            // Chat takes everything between header and bottom-anchored input
            DrawChatArea(bottomReserved);
    
            // ── Input Area (fixed at bottom) ──
            DrawInputArea();
    
            // ── Status Bar ──
            DrawStatusBar();
    
            // Force repaint while running
            if (_orchestrator != null && _orchestrator.IsRunning)
                Repaint();
        }
    
        // ──────────────────── HEADER ────────────────────
    
        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("🚀 Antimatter", _headerStyle, GUILayout.Width(100));
    
                GUILayout.FlexibleSpace();
    
                // Provider indicator
                GUILayout.Label($"{AiProviderSettings.Provider} | {AiProviderSettings.CurrentModel}",
                    EditorStyles.miniLabel);
    
                GUILayout.Space(8);
    
                // Token counter
                if (_orchestrator != null)
                {
                    int total = _orchestrator.TotalPromptTokens + _orchestrator.TotalCompletionTokens;
                    if (total > 0)
                        GUILayout.Label($"Tokens: {total:N0}", EditorStyles.miniLabel);
                }
    
                GUILayout.Space(8);
    
                if (GUILayout.Button("＋", EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    AgentHistory.CreateNewSession();
                    SwitchSession(AgentHistory.CurrentSessionId);
                }

                if (GUILayout.Button("⚙", EditorStyles.toolbarButton, GUILayout.Width(24)))
                    _showSettings = !_showSettings;
    
                if (GUILayout.Button("🗑", EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    if (EditorUtility.DisplayDialog("Clear Chat", "Clear all conversation history?", "Yes", "No"))
                    {
                        _orchestrator?.Cancel();
                        _orchestrator?.ClearHistory();
                        _displayMessages.Clear();
                        AgentHistory.Clear();
                        _statusText = "Idle";
                    }
                }
            }

            // Session Selector
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Thread:", EditorStyles.miniLabel, GUILayout.Width(50));
                var sessions = AgentHistory.GetAllSessions();
                int currentIndex = sessions.FindIndex(s => s.id == AgentHistory.CurrentSessionId);
                if (currentIndex < 0) currentIndex = 0;
                
                if (sessions.Count > 0)
                {
                    string[] options = sessions.ConvertAll(s => s.name).ToArray();
                    int newIndex = EditorGUILayout.Popup(currentIndex, options, EditorStyles.toolbarDropDown);
                    if (newIndex != currentIndex)
                    {
                        SwitchSession(sessions[newIndex].id);
                    }
                }
            }
    
            // Subtitle directly beneath header
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Antimatter by UMB GAMES AND TECHNOLOGY LTD", new GUIStyle(EditorStyles.miniLabel) { normal = new GUIStyleState { textColor = Color.gray } });
                GUILayout.FlexibleSpace();
            }
        }
    
        // ──────────────────── SETTINGS PANEL ────────────────────
    
        private void DrawSettingsPanel()
        {
            _settingsScroll = EditorGUILayout.BeginScrollView(_settingsScroll);
    
            EditorGUILayout.LabelField("Backend Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
    
            AiProviderSettings.Provider = (AiProvider)EditorGUILayout.EnumPopup("Provider", AiProviderSettings.Provider);
    
            EditorGUILayout.Space(4);
            AiProviderSettings.OpenAIKey = EditorGUILayout.PasswordField("OpenAI API Key", AiProviderSettings.OpenAIKey);
            AiProviderSettings.OpenAIModel = EditorGUILayout.TextField("OpenAI Model", AiProviderSettings.OpenAIModel);
    
            EditorGUILayout.Space(4);
            AiProviderSettings.GroqKey = EditorGUILayout.PasswordField("Groq API Key", AiProviderSettings.GroqKey);
            AiProviderSettings.GroqModel = EditorGUILayout.TextField("Groq Model", AiProviderSettings.GroqModel);
    
            EditorGUILayout.Space(4);
            AiProviderSettings.GeminiKey = EditorGUILayout.PasswordField("Gemini API Key", AiProviderSettings.GeminiKey);
            AiProviderSettings.GeminiModel = EditorGUILayout.TextField("Gemini Model", AiProviderSettings.GeminiModel);
    
            EditorGUILayout.Space(4);
            AiProviderSettings.CustomUrl = EditorGUILayout.TextField("Custom Server URL", AiProviderSettings.CustomUrl);
    
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Agent Settings", EditorStyles.boldLabel);
    
            AiProviderSettings.HttpTimeoutSeconds = EditorGUILayout.IntSlider(
                "HTTP Timeout (sec)", AiProviderSettings.HttpTimeoutSeconds, 10, 300);
    
            AiProviderSettings.MaxRetries = EditorGUILayout.IntSlider(
                "Max Retries (legacy)", AiProviderSettings.MaxRetries, 1, 10);
    
            AiProviderSettings.AgentMaxSteps = EditorGUILayout.IntSlider(
                "Max Agent Steps", AiProviderSettings.AgentMaxSteps, 0, 200);
            if (AiProviderSettings.AgentMaxSteps == 0)
                EditorGUILayout.HelpBox("0 = Unlimited steps (agent runs until the task is complete).", MessageType.Info);
    
            AiProviderSettings.AgentConfirmMode = EditorGUILayout.ToggleLeft(
                "Confirm each tool call before execution", AiProviderSettings.AgentConfirmMode);
    
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("JSON Mode / Fallback Settings", EditorStyles.boldLabel);
            _useJsonFallbackMode = EditorGUILayout.ToggleLeft("Use Monolithic JSON Fallback Mode", _useJsonFallbackMode);
            _editCurrentScene = EditorGUILayout.ToggleLeft("Edit Current Scene (Don't create new)", _editCurrentScene);
            _levelGen = EditorGUILayout.ToggleLeft("Level Gen Mode (FPS/Small level)", _levelGen);
    
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Constraints (JSON Mode only)", EditorStyles.miniBoldLabel);
            _constraints.maxObjectsPerScene = EditorGUILayout.IntSlider("Max Objects", _constraints.maxObjectsPerScene, 10, 500);
            _constraints.maxScenes = EditorGUILayout.IntSlider("Max Scenes", _constraints.maxScenes, 1, 5);
            _constraints.forbidScripts = EditorGUILayout.ToggleLeft("Forbid Scripts", _constraints.forbidScripts);
            _constraints.forbidTerrain = EditorGUILayout.ToggleLeft("Forbid Terrain", _constraints.forbidTerrain);
            _constraints.forbidMaterials = EditorGUILayout.ToggleLeft("Forbid Materials", _constraints.forbidMaterials);
            _constraints.enableAutoArrange = EditorGUILayout.ToggleLeft("Auto Arrange (Roads/Settlements)", _constraints.enableAutoArrange);
    
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Image Generation", EditorStyles.boldLabel);
            AiProviderSettings.ImageModel = EditorGUILayout.TextField("Image Model", AiProviderSettings.ImageModel);
            AiProviderSettings.ImageSize = EditorGUILayout.TextField("Image Size", AiProviderSettings.ImageSize);
    
            EditorGUILayout.Space(10);
    
            // Show key status
            switch (AiProviderSettings.Provider)
            {
                case AiProvider.OpenAI:
                    if (string.IsNullOrWhiteSpace(AiProviderSettings.OpenAIKey))
                        EditorGUILayout.HelpBox("OpenAI API key is empty.", MessageType.Error);
                    else
                        EditorGUILayout.HelpBox("OpenAI ready. Best tool-calling reliability.", MessageType.Info);
                    break;
                case AiProvider.Groq:
                    if (string.IsNullOrWhiteSpace(AiProviderSettings.GroqKey))
                        EditorGUILayout.HelpBox("Groq API key is empty.", MessageType.Error);
                    else
                    {
                        bool groqModelSupportsTools = AiProviderSettings.GroqModel.Contains("tool-use") ||
                                                      AiProviderSettings.GroqModel.Contains("groq-70b") ||
                                                      AiProviderSettings.GroqModel.Contains("groq-8b");
                        if (groqModelSupportsTools)
                            EditorGUILayout.HelpBox(
                                "Groq ready. Model supports native tool calling.",
                                MessageType.Info);
                        else
                            EditorGUILayout.HelpBox(
                                "⚠ This Groq model may not support tool calling. Recommended models:\n" +
                                "• llama3-groq-70b-8192-tool-use-preview\n" +
                                "• llama3-groq-8b-8192-tool-use-preview\n" +
                                "The agent will fall back to text-based tool calls automatically.",
                                MessageType.Warning);
                    }
                    break;
                case AiProvider.Gemini:
                    if (string.IsNullOrWhiteSpace(AiProviderSettings.GeminiKey))
                        EditorGUILayout.HelpBox("Gemini API key is empty.", MessageType.Error);
                    else
                        EditorGUILayout.HelpBox("Gemini ready. Function calling supported.", MessageType.Info);
                    break;
                case AiProvider.CustomServer:
                    if (string.IsNullOrWhiteSpace(AiProviderSettings.CustomUrl))
                        EditorGUILayout.HelpBox("Custom server URL is empty.", MessageType.Error);
                    else
                        EditorGUILayout.HelpBox("Custom server. Must support OpenAI-compatible tool calling.", MessageType.Info);
                    break;
            }
    
            EditorGUILayout.Space(10);
            if (GUILayout.Button("← Back to Chat"))
                _showSettings = false;
    
            EditorGUILayout.EndScrollView();
        }
    
        // ──────────────────── CHAT AREA ────────────────────
    
        private void DrawChatArea(float bottomReserved)
        {
            // Chat area fills all vertical space between header and the fixed bottom area
            float chatHeight = position.height - GUILayoutUtility.GetLastRect().yMax - bottomReserved;
            if (chatHeight < 100) chatHeight = 100;
    
            _chatScroll = EditorGUILayout.BeginScrollView(_chatScroll, GUILayout.Height(chatHeight));
    
            if (_displayMessages.Count == 0)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.LabelField("Welcome to Antimatter!", _headerStyle);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Type a goal below and the agent will build it step by step.",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true, fontSize = 12 });
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Examples:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("  • \"Create a simple platformer with a player capsule, ground plane, and 5 platforms\"");
                EditorGUILayout.LabelField("  • \"Add a red bouncing ball with a Rigidbody to the current scene\"");
                EditorGUILayout.LabelField("  • \"Write a PlayerController script with WASD movement and attach it to Player\"");
            }
    
            foreach (var msg in _displayMessages)
            {
                DrawMessage(msg);
            }
    
            // Auto-scroll to bottom
            if (_autoScroll && Event.current.type == EventType.Repaint)
            {
                _chatScroll.y = float.MaxValue;
            }
    
            EditorGUILayout.EndScrollView();
        }
    
        private void DrawMessage(DisplayMessage msg)
        {
            switch (msg.Role)
            {
                case AgentRole.User:
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.BeginVertical(_userBubbleStyle, GUILayout.MaxWidth(position.width * 0.75f));
                    GUILayout.Label($"<b>You</b>  <color=#888>{msg.Timestamp:HH:mm}</color>", _userBubbleStyle);
                    GUILayout.Label(msg.Content, new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true });
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    break;
    
                case AgentRole.Assistant:
                    if (msg.IsToolCall)
                    {
                        // Tool call card
                        EditorGUILayout.BeginVertical(_toolBubbleStyle);
                        string icon = "🔧";
                        string header = $"{icon} <b>{EditorToolDispatcher.GetFriendlyToolName(msg.ToolName)}</b>";
    
                        EditorGUILayout.BeginHorizontal();
                        msg.IsExpanded = EditorGUILayout.Foldout(msg.IsExpanded, "");
                        GUILayout.Label(header, new GUIStyle(EditorStyles.label) { richText = true });
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
    
                        if (msg.IsExpanded && !string.IsNullOrEmpty(msg.ToolArgs))
                        {
                            EditorGUILayout.Space(2);
                            // Pretty-print args
                            try
                            {
                                string pretty = Newtonsoft.Json.Linq.JToken.Parse(msg.ToolArgs).ToString(Newtonsoft.Json.Formatting.Indented);
                                EditorGUILayout.TextArea(pretty, EditorStyles.helpBox, GUILayout.MaxHeight(120));
                            }
                            catch
                            {
                                EditorGUILayout.LabelField(msg.ToolArgs, EditorStyles.miniLabel);
                            }
                        }
    
                        EditorGUILayout.EndVertical();
                    }
                    else
                    {
                        // Normal assistant message
                        EditorGUILayout.BeginVertical(_assistantBubbleStyle, GUILayout.MaxWidth(position.width * 0.85f));
                        GUILayout.Label($"<b>Agent</b>  <color=#888>{msg.Timestamp:HH:mm}</color>",
                            new GUIStyle(EditorStyles.label) { richText = true });
                        GUILayout.Label(msg.Content, new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true });
    
                        // Copy button
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(45)))
                            GUIUtility.systemCopyBuffer = msg.Content;
                        EditorGUILayout.EndHorizontal();
    
                        EditorGUILayout.EndVertical();
                    }
                    break;
    
                case AgentRole.Tool:
                    EditorGUILayout.BeginVertical(_toolBubbleStyle);
                    bool isSuccess = msg.Content != null && !msg.Content.StartsWith("ERROR:");
                    string statusIcon = isSuccess ? "✅" : "❌";
                    string toolHeader = $"  {statusIcon} Result: <b>{EditorToolDispatcher.GetFriendlyToolName(msg.ToolName)}</b>";
    
                    EditorGUILayout.BeginHorizontal();
                    msg.IsExpanded = EditorGUILayout.Foldout(msg.IsExpanded, "");
                    GUILayout.Label(toolHeader, new GUIStyle(EditorStyles.label) { richText = true });
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
    
                    if (msg.IsExpanded)
                    {
                        string display = msg.Content ?? "(empty)";
                        if (display.Length > 500) display = display.Substring(0, 500) + "\n...";
                        EditorGUILayout.TextArea(display, EditorStyles.helpBox, GUILayout.MaxHeight(150));
                    }
    
                    EditorGUILayout.EndVertical();
                    break;
    
                case AgentRole.System:
                    // Don't show system messages
                    break;
            }
        }
    
        // ──────────────────── INPUT AREA ────────────────────
    
        private void DrawInputArea()
        {
            EditorGUILayout.BeginVertical("box");
    
            // Fixed-height input — does NOT grow with content
            const float fixedInputHeight = 40f;
            _inputText = EditorGUILayout.TextArea(_inputText, _inputStyle, GUILayout.Height(fixedInputHeight));
    
            EditorGUILayout.BeginHorizontal();
    
            bool isRunning = _orchestrator != null && _orchestrator.IsRunning;
    
            // Send button
            using (new EditorGUI.DisabledScope(isRunning || string.IsNullOrWhiteSpace(_inputText)))
            {
                if (GUILayout.Button("Send", GUILayout.Height(26), GUILayout.Width(60)))
                {
                    SendMessage();
                }
            }

            // Mic button
            var defaultColor = GUI.backgroundColor;
            if (_isRecordingVoice) GUI.backgroundColor = Color.red;
            if (GUILayout.Button(_isRecordingVoice ? "⬛" : "🎤", GUILayout.Height(26), GUILayout.Width(35)))
            {
                ToggleVoiceRecord();
            }
            GUI.backgroundColor = defaultColor;

            // Check for Enter key (without shift)
            if (Event.current.type == EventType.KeyDown &&
                Event.current.keyCode == KeyCode.Return &&
                !Event.current.shift &&
                !isRunning &&
                !string.IsNullOrWhiteSpace(_inputText))
            {
                SendMessage();
                Event.current.Use();
            }
    
            GUILayout.FlexibleSpace();
    
            // Agent controls
            if (isRunning)
            {
                if (_orchestrator.IsPaused)
                {
                    if (GUILayout.Button("▶ Resume", GUILayout.Width(75)))
                        _orchestrator.Resume();
                }
                else
                {
                    if (GUILayout.Button("⏸ Pause", GUILayout.Width(75)))
                        _orchestrator.Pause();
                }
    
                if (GUILayout.Button("⏹ Cancel", GUILayout.Width(75)))
                {
                    _orchestrator.Cancel();
                    _statusText = "Canceled";
                }
            }
    
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    
        // ──────────────────── STATUS BAR ────────────────────
    
        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bool isRunning = _orchestrator != null && _orchestrator.IsRunning;
    
                // Spinner for active state
                if (isRunning)
                {
                    string[] spinChars = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
                    int idx = (int)(EditorApplication.timeSinceStartup * 8) % spinChars.Length;
                    GUILayout.Label(spinChars[idx], GUILayout.Width(16));
                }
    
                GUILayout.Label(_statusText, EditorStyles.miniLabel);
    
                GUILayout.FlexibleSpace();
    
                if (_currentStep > 0 && _maxSteps > 0)
                    GUILayout.Label($"Step {_currentStep}/{_maxSteps}", EditorStyles.miniLabel);
                else if (_currentStep > 0 && _maxSteps == 0)
                    GUILayout.Label($"Step {_currentStep}/∞", EditorStyles.miniLabel);
            }
        }
    
        // ──────────────────── ACTIONS ────────────────────
    
        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(_inputText)) return;
    
            string goal = _inputText.Trim();
            _inputText = "";
    
            _statusText = "Running...";
            _autoScroll = true;

            // Append Context Assets
            if (_contextAssets.Count > 0)
            {
                var contextText = new StringBuilder("CONTEXTUAL MEMORY Pinned by User:\n");
                foreach (var asset in _contextAssets)
                {
                    if (asset == null) continue;
                    string path = AssetDatabase.GetAssetPath(asset);
                    if (asset is TextAsset textAsset)
                    {
                        contextText.AppendLine($"--- {asset.name} ---");
                        contextText.AppendLine(textAsset.text);
                    }
                    else
                    {
                        contextText.AppendLine($"Asset: {asset.name} (Type: {asset.GetType().Name}, Path: {path})");
                    }
                }
                
                // Inject system message invisibly to history without rendering, or as a new system message
                _orchestrator.InjectSystemMessage(contextText.ToString());
            }
    
            if (_useJsonFallbackMode)
                _ = _orchestrator.RunJsonFallbackAsync(goal, _constraints, _editCurrentScene, _levelGen);
            else
                _ = _orchestrator.RunAsync(goal);
        }
    
        // ──────────────────── EVENT HANDLERS ────────────────────
    
        private void HandleMessage(AgentMessage msg)
        {
            AddDisplayMessage(msg);
    
            // Save history after each message
            AgentHistory.Save(_orchestrator.GetHistory());
    
            Repaint();
        }
    
        private void HandleStepProgress(int current, int max)
        {
            _currentStep = current;
            _maxSteps = max;
            _statusText = $"Step {current}/{max}...";
            Repaint();
        }
    
        private void HandleTokenUsage(int prompt, int completion)
        {
            Repaint();
        }
    
        private void HandleDone()
        {
            _statusText = "Done ✓";
            _currentStep = 0;
            Repaint();
        }
    
        private void HandleError(Exception ex)
        {
            _statusText = $"Error: {ex.Message}";
            Debug.LogError("AI Agent: " + ex);
    
            var errorMsg = new DisplayMessage
            {
                Role = AgentRole.Assistant,
                Content = $"❌ Error: {ex.Message}",
                Timestamp = DateTime.Now
            };
            _displayMessages.Add(errorMsg);
            Repaint();
        }

        private void DrawContextDropzone()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Context Memory (Drag Assets Here)", EditorStyles.miniBoldLabel);
            
            Event evt = Event.current;
            Rect dropRect = GUILayoutUtility.GetRect(0.0f, 30.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Drop TextAssets, Scripts, or Prefabs to pin to AI's context.", EditorStyles.centeredGreyMiniLabel);
            
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropRect.Contains(evt.mousePosition))
                        break;
                    
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                        {
                            if (!_contextAssets.Contains(draggedObject))
                                _contextAssets.Add(draggedObject);
                        }
                    }
                    Event.current.Use();
                    break;
            }

            if (_contextAssets.Count > 0)
            {
                GUILayout.Space(2);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < _contextAssets.Count; i++)
                {
                    if (GUILayout.Button(_contextAssets[i].name + " ✕", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        _contextAssets.RemoveAt(i);
                        i--;
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private async void ToggleVoiceRecord()
        {
            if (_isRecordingVoice)
            {
                _isRecordingVoice = false;
                _statusText = "Transcribing voice...";
                Repaint();
                try
                {
                    string transcribed = await VoiceRecorder.StopAndTranscribeAsync();
                    if (!string.IsNullOrEmpty(transcribed))
                    {
                        _inputText += string.IsNullOrEmpty(_inputText) ? transcribed : " " + transcribed;
                    }
                    _statusText = "Transcription complete.";
                }
                catch (Exception ex)
                {
                    _statusText = "Voice Error: " + ex.Message;
                }
            }
            else
            {
                _isRecordingVoice = true;
                _statusText = "Recording voice... Click Mic again to stop.";
                VoiceRecorder.StartRecording();
            }
            Repaint();
        }
    
        private void AddDisplayMessage(AgentMessage msg)
        {
            _displayMessages.Add(new DisplayMessage
            {
                Role = msg.role,
                Content = msg.content,
                ToolName = msg.toolName,
                ToolArgs = msg.toolArgs,
                IsToolCall = msg.IsToolCall,
                IsExpanded = false,
                Timestamp = msg.Timestamp
            });
        }
    }
}
