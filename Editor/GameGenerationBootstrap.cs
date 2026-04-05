using UnityEditor;

[InitializeOnLoad]
public static class GameGenerationBootstrap
{
    static GameGenerationBootstrap()
    {
        // After domain reload, wait a beat then try to resume.
        EditorApplication.delayCall += ResumeIfPending;
    }

    private static void ResumeIfPending()
    {
        // If Unity is still compiling/updating, keep waiting.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ResumeIfPending;
            return;
        }

        // Resume the SAME executor the window uses.
        GameGenerationExecutor.ResumeIfPending();
    }
}
