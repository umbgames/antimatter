// Assets/Editor/AiPrefsKeys.cs
// Single source of truth for every EditorPrefs / SessionState key used across the project.
// All files must reference these constants instead of hardcoding key strings.

public static class AiPrefsKeys
{
    // ── Provider Selection ──
    public const string Provider = "AI_GAMEGEN_PROVIDER";

    // ── API Keys ──
    public const string OpenAIKey   = "AI_GAMEGEN_OPENAI_KEY";
    public const string GroqKey     = "AI_GAMEGEN_GROQ_KEY";
    public const string GeminiKey   = "AI_GAMEGEN_GEMINI_KEY";
    public const string CustomUrl   = "AI_GAMEGEN_CUSTOM_URL";

    // ── Model Names (per provider, user-configurable) ──
    public const string OpenAIModel  = "AI_GAMEGEN_OPENAI_MODEL";
    public const string GroqModel    = "AI_GAMEGEN_GROQ_MODEL";
    public const string GeminiModel  = "AI_GAMEGEN_GEMINI_MODEL";

    // ── Generation Settings ──
    public const string MaxRetries       = "AI_GAMEGEN_MAX_RETRIES";
    public const string HttpTimeoutSec   = "AI_GAMEGEN_HTTP_TIMEOUT_SEC";
    public const string AiTextures       = "AI_GAMEGEN_AI_TERRAIN_TEXTURES";
    public const string ConstraintsJson  = "AI_GAMEGEN_CONSTRAINTS_JSON";

    // ── Image Generation ──
    public const string ImageModel = "AI_GAMEGEN_IMAGE_MODEL";
    public const string ImageSize  = "AI_GAMEGEN_IMAGE_SIZE";

    // ── Auto-Fix ──
    public const string AutoFixEnabled   = "AI_GAMEGEN_AUTOFIX_ENABLED";
    public const string AutoFixMaxPasses = "AI_GAMEGEN_AUTOFIX_MAXPASSES";

    // ── Session State Keys ──
    public const string SessionConstraints  = "AI_GAMEGEN_CONSTRAINTS_SESSION";
    public const string SessionUserPrompt   = "AI_GAMEGEN_LAST_USERPROMPT";
    public const string SessionLevelGen     = "AI_GAMEGEN_LEVELGEN_SESSION";
    public const string SessionJsonCreate   = "AI_GAMEGEN_JSON_CREATE";
    public const string SessionPendingCreate = "AI_GAMEGEN_PENDING_CREATE";
    public const string SessionJsonEdit     = "AI_GAMEGEN_JSON_EDIT";
    public const string SessionPendingEdit  = "AI_GAMEGEN_PENDING_EDIT";

    // ── Agent ──
    public const string AgentMaxSteps    = "AI_GAMEGEN_AGENT_MAX_STEPS";
    public const string AgentConfirmMode = "AI_GAMEGEN_AGENT_CONFIRM";
    public const string AgentHistory     = "AI_GAMEGEN_AGENT_HISTORY";

    // ── Default Values ──
    public const string DefaultOpenAIModel  = "gpt-4o-mini";
    public const string DefaultGroqModel    = "llama3-groq-70b-8192-tool-use-preview";
    public const string DefaultGeminiModel  = "gemini-2.0-flash";
    public const string DefaultImageModel   = "gpt-image-1";
    public const string DefaultImageSize    = "1024x1024";
    public const int    DefaultMaxRetries   = 3;
    public const int    DefaultHttpTimeout  = 60;
    public const int    DefaultAgentMaxSteps = 20;
}
