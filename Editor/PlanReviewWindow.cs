using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    public class PlanReviewWindow : EditorWindow
    {
        public static bool UserProceeded = false;
        public static string FinalPlanText = "";

        private string _planText;
        private Vector2 _scrollPos;

        public static void ShowModal(string planText)
        {
            UserProceeded = false;
            FinalPlanText = planText;
            var window = ScriptableObject.CreateInstance<PlanReviewWindow>();
            window.titleContent = new GUIContent("Agent Reasoning & Game Plan");
            window._planText = planText;
            window.position = new Rect(Screen.width / 2 - 300, Screen.height / 2 - 350, 600, 700);
            window.ShowModalUtility();
        }

        private void OnGUI()
        {
            GUILayout.Label("Agent Reasoning & Game Plan", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            EditorGUILayout.HelpBox("The agent has proposed the following game design, scripts, and logic breakdown. You can edit this text to modify the plan. Once approved, the agent will execute it.", MessageType.Info);
            GUILayout.Space(10);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            
            var textAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
                fontSize = 12
            };

            // Allow the user to edit the plan
            _planText = EditorGUILayout.TextArea(_planText, textAreaStyle, GUILayout.ExpandHeight(true));
            
            GUILayout.EndScrollView();
            
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                UserProceeded = false;
                this.Close();
            }
            
            if (GUILayout.Button("Approve & Execute", GUILayout.Height(30)))
            {
                UserProceeded = true;
                FinalPlanText = _planText;
                this.Close();
            }
            
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }
    }
}
