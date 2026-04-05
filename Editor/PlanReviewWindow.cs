using System;
using UnityEditor;
using UnityEngine;

public class PlanReviewWindow : EditorWindow
{
    private string _json;
    private GameGeneration _plan;
    private Action<bool> _onCallback;
    private Vector2 _scrollPos;

    private GUIStyle _headerStyle;
    private GUIStyle _textAreaStyle;
    private GUIStyle _aiThoughtStyle;

    public static void Open(string json, GameGeneration plan, Action<bool> onCallback)
    {
        var window = GetWindow<PlanReviewWindow>("Implementation Plan");
        window.minSize = new Vector2(500, 600);
        window._json = json;
        window._plan = plan;
        window._onCallback = onCallback;
        window.ShowModalUtility();
    }

    private void InitStyles()
    {
        if (_headerStyle != null) return;
        
        _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        _textAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        _aiThoughtStyle = new GUIStyle(EditorStyles.helpBox)
        {
            richText = true,
            wordWrap = true,
            padding = new RectOffset(10, 10, 10, 10),
            fontSize = 12
        };
    }

    private void OnGUI()
    {
        InitStyles();

        EditorGUILayout.Space();
        GUILayout.Label("📝 Review AI Implementation Plan", _headerStyle);
        EditorGUILayout.Space();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // AI Explanations
        if (_plan != null && (!string.IsNullOrEmpty(_plan.planExplanation) || !string.IsNullOrEmpty(_plan.planSuggestions)))
        {
            EditorGUILayout.LabelField("AI Thoughts & Suggestions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(_aiThoughtStyle);
            if (!string.IsNullOrEmpty(_plan.planExplanation))
            {
                GUILayout.Label($"<b>Explanation:</b>\n{_plan.planExplanation}");
                EditorGUILayout.Space();
            }
            if (!string.IsNullOrEmpty(_plan.planSuggestions))
            {
                GUILayout.Label($"<b>Suggestions:</b>\n{_plan.planSuggestions}");
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        // Technical Overview
        EditorGUILayout.LabelField("Implementation Steps", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (_plan != null)
        {
            // Scripts
            if (_plan.scripts != null && _plan.scripts.Count > 0)
            {
                foreach (var script in _plan.scripts)
                    GUILayout.Label($" • Create Script: <b>{script.name}</b>", _aiThoughtStyle);
            }

            // Assets (Materials, Terrain)
            if (_plan.assets != null)
            {
                if (_plan.assets.materials != null)
                {
                    foreach (var mat in _plan.assets.materials)
                        GUILayout.Label($" • Create Material: <b>{mat.id}</b> ({mat.shader})", _aiThoughtStyle);
                }
                if (_plan.assets.terrainData != null)
                {
                    foreach (var td in _plan.assets.terrainData)
                        GUILayout.Label($" • Setup Terrain: <b>{td.id}</b> ({td.preset})", _aiThoughtStyle);
                }
            }

            // Scenes & Objects
            if (_plan.scenes != null)
            {
                foreach (var scene in _plan.scenes)
                {
                    GUILayout.Label($" • Scene: <b>{scene.name}</b>", EditorStyles.boldLabel);
                    if (scene.objects != null)
                    {
                        foreach (var obj in scene.objects)
                        {
                            string type = !string.IsNullOrEmpty(obj.primitiveType) ? obj.primitiveType : obj.builtinType;
                            GUILayout.Label($"   - Create {obj.kind} <b>'{obj.id}'</b> ({type})", _aiThoughtStyle);
                            if (obj.components != null)
                            {
                                foreach (var comp in obj.components)
                                {
                                    string cName = !string.IsNullOrEmpty(comp.script) ? comp.script : comp.builtinComponent;
                                    GUILayout.Label($"     + Add component: <b>{cName}</b>", _aiThoughtStyle);
                                }
                            }
                        }
                    }
                }
            }

            // Global World Gen
            if (_plan.settings?.worldGen != null && _plan.settings.worldGen.enabled)
            {
                GUILayout.Label($" • <b>Generate World Layout:</b> {_plan.settings.worldGen.terrainPreset}", _aiThoughtStyle);
                int roadCount = _plan.settings.worldGen.roadHubs?.Count ?? 0;
                int cityCount = _plan.settings.worldGen.settlements?.Count ?? 0;
                if (roadCount > 0) GUILayout.Label($"   - Layout road system with {roadCount} hubs", _aiThoughtStyle);
                if (cityCount > 0) GUILayout.Label($"   - Populate world with {cityCount} settlements", _aiThoughtStyle);
            }
        }
        else
        {
            GUILayout.Label("No implementation plan available to display.");
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Raw JSON Plan", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(_json, _textAreaStyle, GUILayout.ExpandHeight(true));

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("Reject Plan", GUILayout.Height(40)))
        {
            CloseWindow(false);
        }

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("Approve & Execute", GUILayout.Height(40)))
        {
            CloseWindow(true);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    private void CloseWindow(bool approved)
    {
        _onCallback?.Invoke(approved);
        Close();
    }

    private void OnDestroy()
    {
        // If the window is closed via the 'X' button without clicking approve/reject, treat as reject.
        if (_onCallback != null)
        {
            _onCallback.Invoke(false);
            _onCallback = null;
        }
    }
}
