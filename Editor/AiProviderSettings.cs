using UnityEditor;
using System;

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
        get => (AiProvider)EditorPrefs.GetInt(AiPrefsKeys.Provider, (int)AiProvider.OpenAI);
        set => EditorPrefs.SetInt(AiPrefsKeys.Provider, (int)value);
    }

    // ── API Keys ──
    public static string OpenAIKey
    {
        get => EditorPrefs.GetString(AiPrefsKeys.OpenAIKey, "");
        set => EditorPrefs.SetString(AiPrefsKeys.OpenAIKey, value ?? "");
    }

    public static string GroqKey
    {
        get => EditorPrefs.GetString(AiPrefsKeys.GroqKey, "");
        set => EditorPrefs.SetString(AiPrefsKeys.GroqKey, value ?? "");
    }

    public static string GeminiKey
    {
        get => EditorPrefs.GetString(AiPrefsKeys.GeminiKey, "");
        set => EditorPrefs.SetString(AiPrefsKeys.GeminiKey, value ?? "");
    }

    public static string CustomUrl
    {
        get => EditorPrefs.GetString(AiPrefsKeys.CustomUrl, "");
        set => EditorPrefs.SetString(AiPrefsKeys.CustomUrl, value ?? "");
    }

    // ── Model Names (user-configurable per provider) ──
    public static string OpenAIModel
    {
        get => EditorPrefs.GetString(AiPrefsKeys.OpenAIModel, AiPrefsKeys.DefaultOpenAIModel);
        set => EditorPrefs.SetString(AiPrefsKeys.OpenAIModel, value ?? AiPrefsKeys.DefaultOpenAIModel);
    }

    public static string GroqModel
    {
        get => EditorPrefs.GetString(AiPrefsKeys.GroqModel, AiPrefsKeys.DefaultGroqModel);
        set => EditorPrefs.SetString(AiPrefsKeys.GroqModel, value ?? AiPrefsKeys.DefaultGroqModel);
    }

    public static string GeminiModel
    {
        get => EditorPrefs.GetString(AiPrefsKeys.GeminiModel, AiPrefsKeys.DefaultGeminiModel);
        set => EditorPrefs.SetString(AiPrefsKeys.GeminiModel, value ?? AiPrefsKeys.DefaultGeminiModel);
    }

    // ── Shared Settings ──
    public static int HttpTimeoutSeconds
    {
        get => EditorPrefs.GetInt(AiPrefsKeys.HttpTimeoutSec, AiPrefsKeys.DefaultHttpTimeout);
        set => EditorPrefs.SetInt(AiPrefsKeys.HttpTimeoutSec, Math.Clamp(value, 10, 300));
    }

    public static int MaxRetries
    {
        get => EditorPrefs.GetInt(AiPrefsKeys.MaxRetries, AiPrefsKeys.DefaultMaxRetries);
        set => EditorPrefs.SetInt(AiPrefsKeys.MaxRetries, Math.Clamp(value, 1, 10));
    }

    // ── Image Generation ──
    public static string ImageModel
    {
        get => EditorPrefs.GetString(AiPrefsKeys.ImageModel, AiPrefsKeys.DefaultImageModel);
        set => EditorPrefs.SetString(AiPrefsKeys.ImageModel, value ?? AiPrefsKeys.DefaultImageModel);
    }

    public static string ImageSize
    {
        get => EditorPrefs.GetString(AiPrefsKeys.ImageSize, AiPrefsKeys.DefaultImageSize);
        set => EditorPrefs.SetString(AiPrefsKeys.ImageSize, value ?? AiPrefsKeys.DefaultImageSize);
    }

    // ── Agent Settings ──
    public static int AgentMaxSteps
    {
        get => EditorPrefs.GetInt(AiPrefsKeys.AgentMaxSteps, AiPrefsKeys.DefaultAgentMaxSteps);
        set => EditorPrefs.SetInt(AiPrefsKeys.AgentMaxSteps, Math.Clamp(value, 1, 50));
    }

    public static bool AgentConfirmMode
    {
        get => EditorPrefs.GetBool(AiPrefsKeys.AgentConfirmMode, true);
        set => EditorPrefs.SetBool(AiPrefsKeys.AgentConfirmMode, value);
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
            _ => throw new System.Exception("Unknown AI provider.")
        };
    }
}
