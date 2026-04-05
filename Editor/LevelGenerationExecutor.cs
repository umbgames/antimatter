using UnityEditor;

public static class LevelGenerationExecutor
{
    public static void ExecuteCreate(string json)
    {
        SessionState.SetBool(AiPrefsKeys.SessionLevelGen, true);
        GameGenerationExecutor.ExecuteCreate(json);
    }

    public static void ExecuteEdit(string json)
    {
        SessionState.SetBool(AiPrefsKeys.SessionLevelGen, true);
        GameGenerationExecutor.ExecuteEdit(json);
    }

    // Helper used by GameGenerationExecutor
    public static bool IsLevelGenMode()
    {
        return SessionState.GetBool(AiPrefsKeys.SessionLevelGen, false);
    }
}
