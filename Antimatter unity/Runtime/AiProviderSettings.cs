using System;

# if UNITY_EDITOR
using UnityEditor;
# endif

namespace Antimatter
{
    public enum AiProvider
    {
        OpenAI = 0,
        Groq = 1,
        CustomServer = 2, // Retained for backwards compatibility if needed, though unused by BackendPlanClient
        Gemini = 3,
        OpenRouter = 4
    }

    public static class AiProviderSettings
    {
        // ── Provider ──
        public static AiProvider Provider
        {
            get
            {
#if UNITY_EDITOR
                return (AiProvider)EditorPrefs.GetInt(AiPrefsKeys.Provider, (int)AiProvider.OpenAI);
#else
                return AiProvider.OpenAI;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetInt(AiPrefsKeys.Provider, (int)value);
#endif
            }
        }

        // ── Model Names (user-configurable per provider) ──
        public static string OpenAIModel
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(AiPrefsKeys.OpenAIModel, AiPrefsKeys.DefaultOpenAIModel);
#else
                return AiPrefsKeys.DefaultOpenAIModel;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetString(AiPrefsKeys.OpenAIModel, value ?? AiPrefsKeys.DefaultOpenAIModel);
#endif
            }
        }

        public static string GroqModel
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(AiPrefsKeys.GroqModel, AiPrefsKeys.DefaultGroqModel);
#else
                return AiPrefsKeys.DefaultGroqModel;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetString(AiPrefsKeys.GroqModel, value ?? AiPrefsKeys.DefaultGroqModel);
#endif
            }
        }

        public static string GeminiModel
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(AiPrefsKeys.GeminiModel, AiPrefsKeys.DefaultGeminiModel);
#else
                return AiPrefsKeys.DefaultGeminiModel;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetString(AiPrefsKeys.GeminiModel, value ?? AiPrefsKeys.DefaultGeminiModel);
#endif
            }
        }

        public static string OpenRouterModel
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(AiPrefsKeys.OpenRouterModel, AiPrefsKeys.DefaultOpenRouterModel);
#else
                return AiPrefsKeys.DefaultOpenRouterModel;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetString(AiPrefsKeys.OpenRouterModel, value ?? AiPrefsKeys.DefaultOpenRouterModel);
#endif
            }
        }

        // ── Shared Settings ──
        public static int HttpTimeoutSeconds
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetInt(AiPrefsKeys.HttpTimeoutSec, AiPrefsKeys.DefaultHttpTimeout);
#else
                return AiPrefsKeys.DefaultHttpTimeout;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetInt(AiPrefsKeys.HttpTimeoutSec, Math.Clamp(value, 10, 300));
#endif
            }
        }

        public static int MaxRetries
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetInt(AiPrefsKeys.MaxRetries, AiPrefsKeys.DefaultMaxRetries);
#else
                return AiPrefsKeys.DefaultMaxRetries;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetInt(AiPrefsKeys.MaxRetries, Math.Clamp(value, 1, 10));
#endif
            }
        }

        // ── Image Generation ──
        public static string ImageModel
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(AiPrefsKeys.ImageModel, AiPrefsKeys.DefaultImageModel);
#else
                return AiPrefsKeys.DefaultImageModel;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetString(AiPrefsKeys.ImageModel, value ?? AiPrefsKeys.DefaultImageModel);
#endif
            }
        }

        public static string ImageSize
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(AiPrefsKeys.ImageSize, AiPrefsKeys.DefaultImageSize);
#else
                return AiPrefsKeys.DefaultImageSize;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetString(AiPrefsKeys.ImageSize, value ?? AiPrefsKeys.DefaultImageSize);
#endif
            }
        }

        // ── Agent Settings ──
        public static int AgentMaxSteps
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetInt(AiPrefsKeys.AgentMaxSteps, AiPrefsKeys.DefaultAgentMaxSteps);
#else
                return AiPrefsKeys.DefaultAgentMaxSteps;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetInt(AiPrefsKeys.AgentMaxSteps, Math.Clamp(value, 0, 200));
#endif
            }
        }

        public static bool AgentConfirmMode
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetBool(AiPrefsKeys.AgentConfirmMode, true);
#else
                return true;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetBool(AiPrefsKeys.AgentConfirmMode, value);
#endif
            }
        }

        // ── Convenience: get model for current provider ──
        public static string CurrentModel
        {
            get
            {
                return Provider switch
                {
                    AiProvider.OpenAI => OpenAIModel,
                    AiProvider.Groq => GroqModel,
                    AiProvider.Gemini => GeminiModel,
                    AiProvider.OpenRouter => OpenRouterModel,
                    _ => ""
                };
            }
        }

        // ── Factory: create the unified backend client ──
        public static IGamePlanClient CreateClient()
        {
            return new BackendPlanClient();
        }
    }
}
