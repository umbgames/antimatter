using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    public static class UIBuilderTool
    {
        public static AgentToolResult ToolGenerateCanvasUI(JObject args)
        {
            string canvasName = args["canvasName"]?.Value<string>() ?? "GeneratedCanvas";
            var elements = args["elements"] as JArray;

            if (elements == null || elements.Count == 0)
                return AgentToolResult.Fail("No UI elements provided.");

            // Create Canvas
            GameObject canvasGO = new GameObject(canvasName);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");

            // Ensure EventSystem exists
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }

            foreach (var el in elements)
            {
                CreateUIElement(el as JObject, canvasGO.transform);
            }

            return AgentToolResult.Ok($"Successfully generated Canvas '{canvasName}' with {elements.Count} elements.");
        }

        private static void CreateUIElement(JObject def, Transform parent)
        {
            if (def == null) return;

            string name = def["name"]?.Value<string>() ?? "UIElement";
            string type = def["type"]?.Value<string>()?.ToLower() ?? "panel";
            
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();

            // Basic positioning
            var posInfo = def["rect"];
            if (posInfo != null)
            {
                float x = posInfo["x"]?.Value<float>() ?? 0;
                float y = posInfo["y"]?.Value<float>() ?? 0;
                float w = posInfo["width"]?.Value<float>() ?? 100;
                float h = posInfo["height"]?.Value<float>() ?? 100;
                rect.anchoredPosition = new Vector2(x, y);
                rect.sizeDelta = new Vector2(w, h);
            }
            else if (type == "panel")
            {
                // Full stretch for panel
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
            }

            // Apply type-specific components
            switch (type)
            {
                case "panel":
                    var img = go.AddComponent<Image>();
                    img.color = new Color(0, 0, 0, 0.5f);
                    break;
                case "text":
                    var txt = go.AddComponent<Text>();
                    txt.text = def["text"]?.Value<string>() ?? "Text";
                    txt.color = Color.white;
                    txt.alignment = TextAnchor.MiddleCenter;
                    // Note: modern Unity uses TextMeshPro, but standard Text is simpler for AI to generate without dependencies
                    break;
                case "button":
                    var btnImg = go.AddComponent<Image>();
                    btnImg.color = Color.white;
                    var btn = go.AddComponent<Button>();
                    
                    // Add text child
                    GameObject textGo = new GameObject("Text");
                    textGo.transform.SetParent(go.transform, false);
                    var textRect = textGo.AddComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.sizeDelta = Vector2.zero;
                    
                    var btnTxt = textGo.AddComponent<Text>();
                    btnTxt.text = def["text"]?.Value<string>() ?? "Button";
                    btnTxt.color = Color.black;
                    btnTxt.alignment = TextAnchor.MiddleCenter;
                    break;
            }

            // Children
            var children = def["children"] as JArray;
            if (children != null)
            {
                foreach (var childDef in children)
                {
                    CreateUIElement(childDef as JObject, go.transform);
                }
            }
        }
    }
}
