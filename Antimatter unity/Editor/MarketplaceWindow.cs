using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Threading;

namespace Antimatter
{
    public class MarketplaceWindow : EditorWindow
    {
        private enum Tab { Browse, Installed }
        private Tab currentTab = Tab.Browse;
        
        private List<MarketplaceItem> availableItems = new List<MarketplaceItem>();
        
        // CancellationTokenSource for fetching
        private CancellationTokenSource cts;
        private bool isFetching = false;
        private bool isInstalling = false;
        private float installProgress = 0f;

        private Vector2 scrollPos;
        private string activeTypeFilter = "all";

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
            FetchCatalog();
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

            switch (currentTab)
            {
                case Tab.Browse: DrawBrowseUI(); break;
                case Tab.Installed: DrawInstalledUI(); break;
            }
        }

        private void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            bool isBrowse = GUILayout.Toggle(currentTab == Tab.Browse, "Browse", "Button", GUILayout.Width(100), GUILayout.Height(30));
            bool isInstalled = GUILayout.Toggle(currentTab == Tab.Installed, "Installed", "Button", GUILayout.Width(100), GUILayout.Height(30));

            if (isBrowse) currentTab = Tab.Browse;
            if (isInstalled) currentTab = Tab.Installed;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawBrowseUI()
        {
            // Filters
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All", GetFilterStyle("all"))) activeTypeFilter = "all";
            if (GUILayout.Button("Tools", GetFilterStyle("tool"))) activeTypeFilter = "tool";
            if (GUILayout.Button("Models", GetFilterStyle("model"))) activeTypeFilter = "model";
            if (GUILayout.Button("Textures", GetFilterStyle("texture"))) activeTypeFilter = "texture";
            
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", GUILayout.Width(80)))
            {
                FetchCatalog();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (isFetching)
            {
                EditorGUILayout.HelpBox("Fetching marketplace items...", MessageType.Info);
                return;
            }

            if (availableItems.Count == 0)
            {
                GUILayout.Label("No items found. Try a different filter or check back later.");
                return;
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            foreach (var item in availableItems)
            {
                if (activeTypeFilter != "all" && item.itemType != activeTypeFilter) continue;
                DrawItemCard(item);
            }
            GUILayout.EndScrollView();

            if (isInstalling)
            {
                Rect r = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(r, installProgress, "Downloading...");
            }
        }

        private GUIStyle GetFilterStyle(string type)
        {
            if (activeTypeFilter == type) return new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, normal = { textColor = Color.cyan } };
            return GUI.skin.button;
        }

        private void DrawInstalledUI()
        {
            GUILayout.Label("Assets downloaded via the Marketplace are placed in:", EditorStyles.boldLabel);
            GUILayout.Label("- Tools: Assets/Antimatter unity/Editor/Tools/Marketplace");
            GUILayout.Label("- Models/Textures: Assets/Antimatter unity/Assets/Marketplace");
            GUILayout.Space(10);
            
            GUILayout.Label("Manage your imported packages from the Project view.");
        }

        private void DrawItemCard(MarketplaceItem item)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label(item.title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"[{item.itemType.ToUpper()}]", EditorStyles.miniBoldLabel);
            GUILayout.EndHorizontal();

            GUILayout.Label($"By: {item.authorName}  |  Size: {(item.sizeBytes / 1024f / 1024f):F2} MB");
            
            GUILayout.Space(5);
            GUIStyle wrapped = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(item.description, wrapped);

            GUILayout.Space(5);

            GUI.enabled = !isInstalling;
            if (GUILayout.Button("Download & Install", GUILayout.Width(150)))
            {
                InstallItem(item);
            }
            GUI.enabled = true;

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        private async void FetchCatalog()
        {
            isFetching = true;
            Repaint();

            try
            {
                availableItems = await MarketplaceClient.FetchItemsAsync("all", cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error fetching catalog: {ex.Message}");
            }

            isFetching = false;
            Repaint();
        }

        private async void InstallItem(MarketplaceItem item)
        {
            isInstalling = true;
            installProgress = 0f;
            Repaint();

            try
            {
                await MarketplaceClient.DownloadAndInstallAsync(item, p => {
                    installProgress = p;
                    Repaint();
                }, cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to install {item.title}: {ex.Message}");
            }

            isInstalling = false;
            Repaint();
        }
    }
}
