using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    public class SceneDiffWindow : EditorWindow
    {
        private string _toolName;
        private JObject _toolArgs;
        private Action<bool> _onComplete;
        private Vector2 _scrollPos;

        private GUIStyle _diffAddStyle;
        private GUIStyle _diffLabelStyle;

        public static void OpenDiff(string toolName, JObject toolArgs, Action<bool> onComplete)
        {
            var window = GetWindow<SceneDiffWindow>(true, "Agent Action Review", true);
            window.minSize = new Vector2(400, 300);
            window.Setup(toolName, toolArgs, onComplete);
            window.ShowUtility();
        }

        private void Setup(string toolName, JObject toolArgs, Action<bool> onComplete)
        {
            _toolName = toolName;
            _toolArgs = toolArgs;
            _onComplete = onComplete;
        }

        private void InitStyles()
        {
            if (_diffAddStyle == null)
            {
                _diffAddStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    normal = { background = EditorGUIUtility.whiteTexture },
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(5, 5, 5, 5)
                };
            }
            if (_diffLabelStyle == null)
            {
                _diffLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = true
                };
            }
        }

        private void OnGUI()
        {
            InitStyles();

            EditorGUILayout.LabelField("Agent Confirm Mode", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("The agent wants to execute the following tool call. Proceed?", MessageType.Warning);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Tool: {_toolName}", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));
            
            // Basic Diff Visualization
            var defaultColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.1f, 0.5f, 0.1f, 0.3f); // Light green for add
            EditorGUILayout.BeginVertical(_diffAddStyle);
            
            string formattedArgs = _toolArgs != null ? _toolArgs.ToString(Newtonsoft.Json.Formatting.Indented) : "{}";
            EditorGUILayout.LabelField($"<color=#BBDDFF>{formattedArgs}</color>", _diffLabelStyle);
            
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = defaultColor;
            
            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reject", GUILayout.Height(30)))
            {
                _onComplete?.Invoke(false);
                Close();
            }
            if (GUILayout.Button("Approve", GUILayout.Height(30)))
            {
                _onComplete?.Invoke(true);
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void OnDestroy()
        {
            // If window is closed without clicking a button, count as reject
            if (_onComplete != null)
            {
                _onComplete.Invoke(false);
                _onComplete = null;
            }
        }
    }
}
