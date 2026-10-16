using System;

# if UNITY_EDITOR
using UnityEditor;
# endif

namespace Antimatter
{
    public enum AiProvider
    {
        Free = 0,
        Premium = 1,
        Alpha = 2
    }

    public static class AiProviderSettings
    {
        // ── Provider ──
        public static AiProvider Provider
        {
            get
            {
#if UNITY_EDITOR
                return (AiProvider)EditorPrefs.GetInt(AiPrefsKeys.Provider, (int)AiProvider.Free);
#else
                return AiProvider.Free;
#endif
            }
            set
            {
#if UNITY_EDITOR
                EditorPrefs.SetInt(AiPrefsKeys.Provider, (int)value);
#endif
            }
        }

        // ── Model properties removed, hardcoded in CurrentModel ──

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

        // ── Convenience: backend determines actual model from tier ──

        // ── Factory: create the unified backend client ──
        public static IGamePlanClient CreateClient()
        {
            return new BackendPlanClient();
        }
    }
}
