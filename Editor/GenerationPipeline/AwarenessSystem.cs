namespace Antimatter
{
    public class AwarenessContext
    {
        public string Prompt;
        public SceneStateTracker State;
    }

    public class AwarenessSystem
    {
        public AwarenessContext Analyze(string userPrompt, SceneStateTracker sceneState)
        {
            return new AwarenessContext { Prompt = userPrompt, State = sceneState };
        }
    }
}
