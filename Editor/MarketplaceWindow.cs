using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Threading;

namespace Antimatter
{
    public class MarketplaceWindow : EditorWindow
    {
        private enum Tab { LocalTools, Browse, Installed, Publish, Account }
        private Tab currentTab = Tab.LocalTools;
        
        // Publish State
        private string pubId = "custom_tool";
        private string pubName = "My Tool";
        private string pubVersion = "1.0.0";
        private string pubDesc = "A custom tool for the AI.";
        private string pubAiDesc = "Call this tool to do internal work.";
        private string pubScriptPath = "Assets/Editor/MyTool.cs";


        private string email = "";
        private string password = "";
        private string statusMessage = "";
        private MessageType statusType = MessageType.Info;

        private List<ToolManifest> availableTools = new List<ToolManifest>();
        private List<ToolManifest> installedTools = new List<ToolManifest>();

        private CancellationTokenSource cts;
        private bool isFetching = false;
        private bool isInstalling = false;

        private Vector2 scrollPos;

        [MenuItem("Antimatter/Marketplace")]
        public static void ShowWindow()
        {
            var window = GetWindow<MarketplaceWindow>("Antimatter Marketplace");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnEnable()
        {
            cts = new CancellationTokenSource();
            RefreshInstalledTools();
        }

        private void OnDisable()
        {
            cts?.Cancel();
            cts?.Dispose();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            DrawTabs();
            GUILayout.Space(10);

            if (currentTab == Tab.LocalTools)
            {
                DrawLocalToolsUI();
            }
            else if (!FirebaseApiClient.IsLoggedIn)
            {
                DrawLoginUI();
            }
            else
            {
                switch (currentTab)
                {
                    case Tab.Browse: DrawBrowseUI(); break;
                    case Tab.Installed: DrawInstalledUI(); break;
                    case Tab.Publish: DrawPublishUI(); break;
                    case Tab.Account: DrawAccountUI(); break;
                }
            }
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool isLocalTools = GUILayout.Toggle(currentTab == Tab.LocalTools, "Local Tools", "Button", GUILayout.Width(100), GUILayout.Height(30));
            bool isBrowse = GUILayout.Toggle(currentTab == Tab.Browse, "Browse Tools", "Button", GUILayout.Width(100), GUILayout.Height(30));
            bool isInstalled = GUILayout.Toggle(currentTab == Tab.Installed, "Installed", "Button", GUILayout.Width(100), GUILayout.Height(30));
            bool isPublish = GUILayout.Toggle(currentTab == Tab.Publish, "Publish Tool", "Button", GUILayout.Width(100), GUILayout.Height(30));
            bool isAccount = GUILayout.Toggle(currentTab == Tab.Account, "Account", "Button", GUILayout.Width(100), GUILayout.Height(30));

            if (isLocalTools) currentTab = Tab.LocalTools;
            if (isBrowse) currentTab = Tab.Browse;
            if (isInstalled) currentTab = Tab.Installed;
            if (isPublish) currentTab = Tab.Publish;
            if (isAccount) currentTab = Tab.Account;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawLoginUI()
        {
            GUILayout.Label("Authentication Required", EditorStyles.boldLabel);
            
            GUILayout.Space(10);
            email = EditorGUILayout.TextField("Email", email);
            password = EditorGUILayout.PasswordField("Password", password);

            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Marketplace functionality is coming soon!", MessageType.Info);
            GUILayout.Space(10);

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Log In", GUILayout.Height(30)))
            {
                DoLogin();
            }
            if (GUILayout.Button("Sign Up", GUILayout.Height(30)))
            {
                DoSignUp();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawPublishUI()
        {
            GUILayout.Label("Publish a Tool", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            
            pubId = EditorGUILayout.TextField("Tool ID (unique)", pubId);
            pubName = EditorGUILayout.TextField("Tool Name", pubName);
            pubVersion = EditorGUILayout.TextField("Version", pubVersion);
            
            GUILayout.Space(5);
            GUILayout.Label("Description (for humans)", EditorStyles.miniLabel);
            pubDesc = EditorGUILayout.TextArea(pubDesc, GUILayout.Height(40));

            GUILayout.Space(5);
            GUILayout.Label("AI Description (for the agent prompt)", EditorStyles.miniLabel);
            pubAiDesc = EditorGUILayout.TextArea(pubAiDesc, GUILayout.Height(40));
            
            GUILayout.Space(5);
            pubScriptPath = EditorGUILayout.TextField("Primary Script Path", pubScriptPath);
            EditorGUILayout.HelpBox("Path should be relative to project, e.g. Assets/Scripts/MyTool.cs", MessageType.Info);

            GUILayout.Space(15);
            
            GUI.enabled = !isFetching;
            if (GUILayout.Button("Publish to Marketplace", GUILayout.Height(30)))
            {
                DoPublish();
            }
            GUI.enabled = true;
            
            if (isFetching)
            {
                EditorGUILayout.HelpBox("Publishing tool...", MessageType.Info);
            }
            else if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
            
            GUILayout.EndScrollView();
        }

        private void DrawAccountUI()
        {
            GUILayout.Label("Account", EditorStyles.boldLabel);
            GUILayout.Space(10);
            GUILayout.Label($"Logged in User ID: {MarketplaceSettings.CurrentUserId}");

            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Marketplace functionality is coming soon!", MessageType.Info);
            GUILayout.Space(10);

            if (GUILayout.Button("Log Out", GUILayout.Width(100)))
            {
                FirebaseApiClient.Logout();
                statusMessage = "Logged out.";
                statusType = MessageType.Info;
            }
        }

        private void DrawLocalToolsUI()
        {
            GUILayout.Label("Default Agent Tools", EditorStyles.boldLabel);
            GUILayout.Space(10);

            var tools = EditorToolDispatcher.GetAllTools();
            
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            foreach (var tool in tools)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox); // Clearer border
                
                GUILayout.Space(5);
                GUILayout.Label(tool.Name, EditorStyles.boldLabel);
                
                GUILayout.Space(5);
                // Draw a simple horizontal line
                Rect rect = EditorGUILayout.GetControlRect(false, 1f);
                EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                GUILayout.Space(5);

                GUILayout.Label(tool.Description, EditorStyles.wordWrappedLabel);
                GUILayout.Space(5);
                EditorGUILayout.EndVertical();
                
                GUILayout.Space(10);
            }
            GUILayout.EndScrollView();
        }

        private void DrawBrowseUI()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Catalog", GUILayout.Width(150), GUILayout.Height(25)))
            {
                FetchCatalog();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (isFetching)
            {
                EditorGUILayout.HelpBox("Fetching tools from Marketplace...", MessageType.Info);
                return;
            }

            if (!string.IsNullOrEmpty(statusMessage) && statusType == MessageType.Error)
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }

            if (availableTools.Count == 0)
            {
                GUILayout.Label("No tools available currently. Ask an admin to invite you as a creator or publish a tool.");
                return;
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            foreach (var tool in availableTools)
            {
                DrawToolCard(tool, false);
            }
            GUILayout.EndScrollView();
        }

        private void DrawInstalledUI()
        {
            if (GUILayout.Button("Refresh Local Registry", GUILayout.Width(180)))
            {
                RefreshInstalledTools();
            }

            GUILayout.Space(10);

            if (installedTools.Count == 0)
            {
                GUILayout.Label("No tools installed locally.");
                return;
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            foreach (var tool in installedTools)
            {
                DrawToolCard(tool, true);
            }
            GUILayout.EndScrollView();
        }

        private void DrawToolCard(ToolManifest tool, bool isLocal)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.Label($"{tool.name} (v{tool.version})", EditorStyles.boldLabel);
            GUILayout.Label($"By: {tool.author}");
            GUILayout.Label(tool.description, EditorStyles.wordWrappedLabel);

            GUILayout.Space(5);

            if (isLocal)
            {
                if (GUILayout.Button("Uninstall", GUILayout.Width(100)))
                {
                    UninstallTool(tool.id);
                }
            }
            else
            {
                bool installed = ToolRegistry.IsToolInstalled(tool.id);
                if (installed)
                {
                    GUILayout.Label("Installed", EditorStyles.miniBoldLabel);
                }
                else
                {
                    GUI.enabled = !isInstalling;
                    if (GUILayout.Button("Install", GUILayout.Width(100)))
                    {
                        InstallTool(tool);
                    }
                    GUI.enabled = true;
                }
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private async void DoLogin()
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return;
            statusMessage = "Logging in...";
            statusType = MessageType.Info;
            Repaint();

            var (success, error) = await FirebaseApiClient.LoginAsync(email, password, cts.Token);
            if (success)
            {
                statusMessage = "Login successful!";
                statusType = MessageType.Info;
                FetchCatalog();
            }
            else
            {
                statusMessage = $"Login failed: {error}";
                statusType = MessageType.Error;
            }
            Repaint();
        }

        private async void DoSignUp()
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return;
            statusMessage = "Signing up...";
            statusType = MessageType.Info;
            Repaint();

            var (success, error) = await FirebaseApiClient.SignUpAsync(email, password, cts.Token);
            if (success)
            {
                statusMessage = "Sign up successful!";
                statusType = MessageType.Info;
                FetchCatalog();
            }
            else
            {
                statusMessage = $"Sign up failed: {error}";
                statusType = MessageType.Error;
            }
            Repaint();
        }

        private async void FetchCatalog()
        {
            isFetching = true;
            statusMessage = "";
            Repaint();

            try
            {
                // To avoid relying strictly on Firebase DB structure immediately, we catch exceptions.
                availableTools = await MarketplaceClient.FetchAvailableToolsAsync(cts.Token);
            }
            catch (Exception ex)
            {
                statusMessage = $"Error fetching catalog: {ex.Message}";
                statusType = MessageType.Error;
            }

            isFetching = false;
            RefreshInstalledTools();
            Repaint();
        }

        private void RefreshInstalledTools()
        {
            installedTools = ToolRegistry.GetInstalledTools();
        }

        private async void InstallTool(ToolManifest tool)
        {
            isInstalling = true;
            statusMessage = $"Downloading {tool.name}...";
            statusType = MessageType.Info;
            Repaint();

            try
            {
                byte[] data = await MarketplaceClient.DownloadToolPackageAsync(tool.id, tool.version, cts.Token);
                
                ToolPackageHandler.InstallToolPackage(tool.id, data);
                ToolRegistry.RegisterTool(tool);

                statusMessage = $"{tool.name} installed successfully!";
                statusType = MessageType.Info;
                RefreshInstalledTools();
            }
            catch (Exception ex)
            {
                statusMessage = $"Failed to install {tool.name}: {ex.Message}";
                statusType = MessageType.Error;
            }

            isInstalling = false;
            Repaint();
        }

        private void UninstallTool(string toolId)
        {
            ToolPackageHandler.UninstallTool(toolId);
            ToolRegistry.UnregisterTool(toolId);
            RefreshInstalledTools();
            Repaint();
        }

        private async void DoPublish()
        {
            isFetching = true;
            statusMessage = "Starting publish...";
            statusType = MessageType.Info;
            Repaint();

            try
            {
                var manifest = new ToolManifest
                {
                    id = pubId,
                    name = pubName,
                    version = pubVersion,
                    author = MarketplaceSettings.CurrentUserId,
                    description = pubDesc,
                    ai_description = pubAiDesc,
                    entry_point = pubScriptPath,
                    category = "misc",
                    dependencies = new List<string>()
                };

                await MarketplaceClient.PublishToolAsync(manifest, cts.Token);
                statusMessage = "Tool published successfully!";
                statusType = MessageType.Info;
            }
            catch (Exception ex)
            {
                statusMessage = $"Publish failed: {ex.Message}";
                statusType = MessageType.Error;
            }
            
            isFetching = false;
            Repaint();
        }
    }
}
