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
        private float _currentInputHeight = 40f;
        
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
            var w = GetWindow<AgentWindow>("ɅNTIMɅTTΞR");
            w.minSize = new Vector2(420, 500);
        }
    
        private void OnEnable()
        {
            _stylesInitialized = false;
            _orchestrator = new AgentOrchestrator();
            _orchestrator.OnMessage += HandleMessage;
            _orchestrator.OnStepProgress += HandleStepProgress;
            _orchestrator.OnTokenUsage += HandleTokenUsage;
            _orchestrator.OnDone += HandleDone;
            _orchestrator.OnError += HandleError;
    
            EditorSceneManager.sceneOpened += OnSceneOpened;
            VoiceRecorder.OnTranscriptionUpdated += HandleTranscriptionUpdated;
            VoiceRecorder.OnTranscriptionFinished += HandleTranscriptionFinished;
    
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
            VoiceRecorder.OnTranscriptionUpdated -= HandleTranscriptionUpdated;
            VoiceRecorder.OnTranscriptionFinished -= HandleTranscriptionFinished;
    
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
    
        private Texture2D _userBg;
        private Texture2D _toolBg;

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.hideFlags = HideFlags.HideAndDontSave;
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private Texture2D MakeRoundedTex(int radius, Color col)
        {
            int size = radius * 2 + 2;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = x < radius ? radius : (x > size - 1 - radius ? size - 1 - radius : x);
                    int cy = y < radius ? radius : (y > size - 1 - radius ? size - 1 - radius : y);

                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    Color c = col;
                    c.a *= alpha;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            
            _userBg = MakeRoundedTex(10, new Color(0.18f, 0.18f, 0.18f, 1f));
            _toolBg = MakeRoundedTex(8, new Color(0.12f, 0.14f, 0.16f, 1f));
            Texture2D inputBg = MakeRoundedTex(12, new Color(0.12f, 0.12f, 0.12f, 1f));
            Texture2D assistantBg = MakeRoundedTex(10, new Color(0.15f, 0.15f, 0.15f, 0.8f));

            _userBubbleStyle = new GUIStyle()
            {
                richText = true,
                wordWrap = true,
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(40, 8, 4, 4),
                border = new RectOffset(10, 10, 10, 10),
                fontSize = 12
            };
            _userBubbleStyle.normal.background = _userBg;
            _userBubbleStyle.normal.textColor = Color.white;
    
            _assistantBubbleStyle = new GUIStyle()
            {
                richText = true,
                wordWrap = true,
                padding = new RectOffset(12, 12, 10, 10),
                margin = new RectOffset(8, 40, 4, 4),
                border = new RectOffset(10, 10, 10, 10),
                fontSize = 12
            };
            _assistantBubbleStyle.normal.background = assistantBg;
            _assistantBubbleStyle.normal.textColor = Color.white;
    
            _toolBubbleStyle = new GUIStyle()
            {
                richText = true,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(20, 20, 4, 4),
                border = new RectOffset(8, 8, 8, 8),
                fontSize = 11
            };
            _toolBubbleStyle.normal.background = _toolBg;
            _toolBubbleStyle.normal.textColor = Color.white;
    
            _systemBubbleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(20, 20, 2, 2)
            };
    
            _inputStyle = new GUIStyle()
            {
                wordWrap = true,
                fontSize = 12,
                padding = new RectOffset(12, 12, 12, 12),
                border = new RectOffset(12, 12, 12, 12)
            };
            _inputStyle.normal.background = inputBg;
            _inputStyle.normal.textColor = Color.white;
            _inputStyle.focused.background = inputBg;
            _inputStyle.focused.textColor = Color.white;
            _inputStyle.active.background = inputBg;
            _inputStyle.active.textColor = Color.white;
            _inputStyle.hover.background = inputBg;
            _inputStyle.hover.textColor = Color.white;
    
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
    
            if (!FirebaseApiClient.IsLoggedIn)
            {
                DrawLoginScreen();
                return;
            }
    
            // ── Settings Panel ──
            if (_showSettings)
            {
                DrawSettingsPanel();
                return;
            }
    
            // ── Fixed layout: input+status anchored to bottom, chat fills the rest ──
            float inputAreaPadding = 55f; // padding + footer row
            if (_orchestrator != null && _orchestrator.IsRunning) inputAreaPadding += 35f;
            float dynamicInputAreaHeight = _currentInputHeight + inputAreaPadding;
            const float statusBarHeight = 20f;
            const float bottomPadding = 40f;
            float bottomReserved = dynamicInputAreaHeight + statusBarHeight + bottomPadding;
    
            // Chat takes everything between header and bottom-anchored input
            DrawChatArea(bottomReserved);
    
            // ── Input Area (fixed at bottom) ──
            DrawInputArea();

            GUILayout.Space(40f);
    
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
                GUILayout.Label("ɅNTIMɅTTΞR", _headerStyle, GUILayout.Width(100));
    
                GUILayout.FlexibleSpace();
    
                // Provider indicator
                GUILayout.Label($"Tier: {AiProviderSettings.Provider}",
                    EditorStyles.miniLabel);
    
                GUILayout.Space(8);
    
                // Credit counter
                if (_orchestrator != null)
                {
                    if (_orchestrator.TotalCreditsConsumed > 0)
                    {
                        string cText = $"Cost: {_orchestrator.TotalCreditsConsumed:F2} CRD";
                        if (_orchestrator.AvailableCreditBalance > 0)
                            cText += $" | Bal: {_orchestrator.AvailableCreditBalance:F2} CRD";
                        GUILayout.Label(cText, EditorStyles.miniLabel);
                    }
                }
    
                GUILayout.Space(8);
    
                if (GUILayout.Button(EditorGUIUtility.IconContent("Toolbar Plus"), EditorStyles.toolbarButton, GUILayout.Width(24)))
                {
                    AgentHistory.CreateNewSession();
                    SwitchSession(AgentHistory.CurrentSessionId);
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("SettingsIcon"), EditorStyles.toolbarButton, GUILayout.Width(24)))
                    _showSettings = !_showSettings;
    
                if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), EditorStyles.toolbarButton, GUILayout.Width(24)))
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

                if (FirebaseApiClient.IsLoggedIn)
                {
                    if (GUILayout.Button("Sign Out", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        if (EditorUtility.DisplayDialog("Sign Out", "Are you sure you want to sign out?", "Yes", "No"))
                        {
                            FirebaseApiClient.Logout();
                        }
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

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Delete All Threads", EditorStyles.toolbarButton, GUILayout.Width(110)))
                {
                    if (EditorUtility.DisplayDialog("Delete All Threads", "Are you sure you want to completely delete ALL saved conversation threads from disk? This cannot be undone.", "Delete All", "Cancel"))
                    {
                        AgentHistory.DeleteAllSessions();
                        _orchestrator?.ClearHistory();
                        _displayMessages.Clear();
                        _statusText = "All threads deleted.";
                        SwitchSession(AgentHistory.CurrentSessionId);
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
                case AiProvider.Free:
                    EditorGUILayout.HelpBox("Free tier ready. Powered by Antimatter.", MessageType.Info);
                    break;
                case AiProvider.Premium:
                    EditorGUILayout.HelpBox("Premium tier ready. Powered by Antimatter.", MessageType.Info);
                    break;
                case AiProvider.Alpha:
                    EditorGUILayout.HelpBox("Alpha tier ready. Powered by Antimatter.", MessageType.Info);
                    break;
            }
    
            EditorGUILayout.Space(10);

            // Add Credits button linking to local or production top-up URL
            if (GUILayout.Button("Add Unity Credits", GUILayout.Height(30)))
            {
                Application.OpenURL("http://antimatter.ng/#/unity-credits");
            }

            EditorGUILayout.Space(10);

            var logoutStyle = new GUIStyle(GUI.skin.button) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };
            if (GUILayout.Button("Sign Out", logoutStyle))
            {
                FirebaseApiClient.Logout();
                _showSettings = false;
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("← Back to Chat"))
                _showSettings = false;
    
            EditorGUILayout.EndScrollView();
        }
    
        // ──────────────────── LOGIN SCREEN ────────────────────
    
        private void DrawLoginScreen()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            EditorGUILayout.BeginVertical(new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(20, 20, 20, 20) }, GUILayout.Width(300));
            
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("Welcome to Antimatter", titleStyle);
            GUILayout.Space(10);
            
            var descStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("Please sign in to connect the agent to your project.", descStyle);
            
            GUILayout.Space(20);
            
            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, padding = new RectOffset(10, 10, 10, 10) };
            if (GUILayout.Button("Sign in with Google", btnStyle, GUILayout.Height(40)))
            {
                HandleGoogleSignIn();
            }
            
            EditorGUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private async void HandleGoogleSignIn()
        {
            var (success, errorMsg) = await FirebaseApiClient.SignInWithGoogleBrowserAsync();
            if (!success)
            {
                Debug.LogError($"Sign in failed: {errorMsg}");
            }
            Repaint();
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
                GUILayout.Space(chatHeight * 0.25f);

                EditorGUILayout.BeginVertical();
                
                var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 24, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };
                var subtitleStyle = new GUIStyle(EditorStyles.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
                var exampleStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
                
                GUILayout.Label("ɅNTIMɅTTΞR", titleStyle);
                GUILayout.Space(8);
                GUILayout.Label("What would you like to build today?", subtitleStyle);
                GUILayout.Space(35);

                GUILayout.Label("Try saying:", new GUIStyle(subtitleStyle) { fontSize = 12, fontStyle = FontStyle.Normal });
                GUILayout.Space(10);
                
                GUILayout.Label("\"Create a small fantasy village with a central square\"", exampleStyle);
                GUILayout.Space(6);
                GUILayout.Label("\"Generate a complete world with a medieval mountain kingdom\"", exampleStyle);
                GUILayout.Space(6);
                GUILayout.Label("\"Spawn a water splash particle system right here\"", exampleStyle);

                EditorGUILayout.EndVertical();
            }
    
            int startIndex = Mathf.Max(0, _displayMessages.Count - 50);
            if (startIndex > 0)
            {
                EditorGUILayout.LabelField($"... {_displayMessages.Count - 50} older messages hidden ...", EditorStyles.centeredGreyMiniLabel);
            }
            for (int i = startIndex; i < _displayMessages.Count; i++)
            {
                DrawMessage(_displayMessages[i]);
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
            var messageStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };

            switch (msg.Role)
            {
                case AgentRole.User:
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.BeginVertical(_userBubbleStyle, GUILayout.MaxWidth(position.width * 0.75f));
                    
                    GUILayout.Label(msg.Content, messageStyle);
                    
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
                                var codeStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, normal = { textColor = new Color(0.6f, 0.7f, 0.8f) } };
                                GUILayout.Label(pretty, codeStyle);
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
                        GUILayout.Label(msg.Content, messageStyle);
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
            // Give it some side margin so it's not hugging the walls completely
            EditorGUILayout.BeginVertical(new GUIStyle() { padding = new RectOffset(10, 10, 0, 0) });
            bool isRunning = _orchestrator != null && _orchestrator.IsRunning;

            // The main input bar using the flat dark grey _inputStyle
            EditorGUILayout.BeginVertical(_inputStyle);

            var borderlessTextArea = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                fontSize = 13,
                padding = new RectOffset(8, 8, 10, 10)
            };
            borderlessTextArea.normal.background = _inputStyle.normal.background;
            borderlessTextArea.focused.background = _inputStyle.normal.background;
            borderlessTextArea.hover.background = _inputStyle.normal.background;
            borderlessTextArea.active.background = _inputStyle.normal.background;
            borderlessTextArea.normal.textColor = Color.white;
            borderlessTextArea.focused.textColor = Color.white;

            // Calculate auto-resizing height
            float textWidth = position.width - 20f - 20f; // window padding - inputStyle padding
            float contentHeight = borderlessTextArea.CalcHeight(new GUIContent(_inputText), textWidth);
            _currentInputHeight = Mathf.Clamp(contentHeight, 80f, 140f); // 80px min height

            // Shift+Enter / Enter handling
            Event e = Event.current;
            bool submit = false;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
            {
                if (!e.shift && !isRunning && !string.IsNullOrWhiteSpace(_inputText))
                {
                    submit = true;
                    e.Use();
                }
            }

            _inputText = EditorGUILayout.TextArea(_inputText, borderlessTextArea, GUILayout.Height(_currentInputHeight));

            if (submit)
            {
                SendMessage();
            }

            // Footer Row
            EditorGUILayout.BeginHorizontal();
            
            var helperStyle = new GUIStyle(EditorStyles.miniLabel);
            helperStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            GUILayout.Label("Ask anything... (Enter to send, Shift+Enter for newline)", helperStyle);
            
            GUILayout.FlexibleSpace();

            // Completely flat buttons without Unity's button borders
            var btnStyle = new GUIStyle(EditorStyles.label)
            {
                fixedWidth = 28,
                fixedHeight = 28,
                alignment = TextAnchor.MiddleCenter
            };

            var defaultColor = GUI.color;
            if (_isRecordingVoice) GUI.color = Color.red;
            
            GUIContent micIcon = _isRecordingVoice ? EditorGUIUtility.IconContent("Record On") : EditorGUIUtility.IconContent("Record Off");
            micIcon.tooltip = _isRecordingVoice ? "Stop Recording" : "Record Voice";

            if (GUILayout.Button(micIcon, btnStyle))
            {
                ToggleVoiceRecord();
            }
            GUI.color = defaultColor;

            using (new EditorGUI.DisabledScope(isRunning || string.IsNullOrWhiteSpace(_inputText)))
            {
                GUIContent sendIcon = EditorGUIUtility.IconContent("PlayButton");
                sendIcon.tooltip = "Send Message";

                if (GUILayout.Button(sendIcon, btnStyle))
                {
                    SendMessage();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (isRunning)
            {
                GUILayout.Space(5f); // 5px below the input box
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                // Wrap the controls in a sleek dark pill using the input style
                EditorGUILayout.BeginHorizontal(new GUIStyle(_inputStyle) { padding = new RectOffset(6, 6, 2, 2) });

                var ctrlBtnStyle = new GUIStyle(EditorStyles.label)
                {
                    fixedWidth = 24,
                    fixedHeight = 24,
                    alignment = TextAnchor.MiddleCenter
                };

                GUIContent resumeIcon = EditorGUIUtility.IconContent("PlayButton");
                resumeIcon.tooltip = "Resume";
                GUIContent pauseIcon = EditorGUIUtility.IconContent("PauseButton");
                pauseIcon.tooltip = "Pause";
                GUIContent cancelIcon = EditorGUIUtility.IconContent("Toolbar Minus");
                cancelIcon.tooltip = "Cancel / Stop Generation";

                if (_orchestrator.IsPaused)
                {
                    if (GUILayout.Button(resumeIcon, ctrlBtnStyle))
                        _orchestrator.Resume();
                }
                else
                {
                    if (GUILayout.Button(pauseIcon, ctrlBtnStyle))
                        _orchestrator.Pause();
                }

                GUILayout.Space(6); // Slight gap between pause and cancel

                if (GUILayout.Button(cancelIcon, ctrlBtnStyle))
                {
                    _orchestrator.Cancel();
                    _statusText = "Canceled";
                }

                EditorGUILayout.EndHorizontal();

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

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

        private string _preRecordText = "";

        private void ToggleVoiceRecord()
        {
            if (_isRecordingVoice)
            {
                _isRecordingVoice = false;
                _statusText = "Finalizing transcription...";
                VoiceRecorder.StopRecording();
                Repaint();
            }
            else
            {
                _preRecordText = _inputText;
                _isRecordingVoice = true;
                _statusText = "Recording... (Streaming)";
                VoiceRecorder.StartRecording();
                Repaint();
            }
        }

        private void HandleTranscriptionUpdated(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                _inputText = (string.IsNullOrWhiteSpace(_preRecordText) ? text : _preRecordText + " " + text).Trim();
                Repaint();
            }
        }

        private void HandleTranscriptionFinished(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                _inputText = (string.IsNullOrWhiteSpace(_preRecordText) ? text : _preRecordText + " " + text).Trim();
            }
            _statusText = "Idle";
            _isRecordingVoice = false;
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
