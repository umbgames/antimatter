using UnityEditor;

namespace Antimatter
{
    
    public static class LevelGenerationExecutor
    {
        public static void ExecuteCreate(string json)
        {
            SessionState.SetBool(AiPrefsKeys.SessionLevelGen, true);
            new GenerationOrchestrator().Execute(json);
        }
    
        public static void ExecuteEdit(string json)
        {
            SessionState.SetBool(AiPrefsKeys.SessionLevelGen, true);
            new GenerationOrchestrator().Execute(json);
        }
    
        // Helper used by GameGenerationExecutor
        public static bool IsLevelGenMode()
        {
            return SessionState.GetBool(AiPrefsKeys.SessionLevelGen, false);
        }
    }
}
