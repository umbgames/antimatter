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
        CustomServer = 2,
        Gemini = 3
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

        // ── API Keys ──
        public static string OpenAIKey
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(AiPrefsKeys.OpenAIKey, "");
#else
                return "";
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetString(AiPrefsKeys.OpenAIKey, value ?? "");
#endif
            }
        }

        public static string GroqKey
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(AiPrefsKeys.GroqKey, "");
#else
                return "";
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetString(AiPrefsKeys.GroqKey, value ?? "");
#endif
            }
        }

        public static string GeminiKey
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(AiPrefsKeys.GeminiKey, "");
#else
                return "";
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetString(AiPrefsKeys.GeminiKey, value ?? "");
#endif
            }
        }

        public static string CustomUrl
        {
            get
            {
#if UNITY_EDITOR
                return EditorPrefs.GetString(AiPrefsKeys.CustomUrl, "");
#else
                return "";
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetString(AiPrefsKeys.CustomUrl, value ?? "");
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
                    _ => ""
                };
            }
        }

        // ── Convenience: get API key for current provider ──
        public static string CurrentApiKey
        {
            get
            {
                return Provider switch
                {
                    AiProvider.OpenAI => OpenAIKey,
                    AiProvider.Groq => GroqKey,
                    AiProvider.Gemini => GeminiKey,
                    _ => ""
                };
            }
        }

        // ── Helper: push key into environment for clients that read env vars ──
        public static void PushCurrentKeyToEnvironment()
        {
            switch (Provider)
            {
                case AiProvider.OpenAI:
                    System.Environment.SetEnvironmentVariable("OPENAI_API_KEY", OpenAIKey);
                    break;
                case AiProvider.Groq:
                    System.Environment.SetEnvironmentVariable("GROQ_API_KEY", GroqKey);
                    break;
                case AiProvider.Gemini:
                    System.Environment.SetEnvironmentVariable("GEMINI_API_KEY", GeminiKey);
                    break;
            }
        }

        // ── Factory: create the right client for current provider ──
        public static IGamePlanClient CreateClient()
        {
            PushCurrentKeyToEnvironment();

            return Provider switch
            {
                AiProvider.OpenAI => new OpenAIPlanClient(),
                AiProvider.Groq => new GroqPlanClient(),
                AiProvider.Gemini => new GeminiPlanClient(),
                AiProvider.CustomServer => new CustomModelPlanClient(CustomUrl),
                _ => throw new System.Exception($"Unknown AI provider: {Provider}")
            };
        }
    }
}
