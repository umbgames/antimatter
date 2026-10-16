namespace Antimatter
{
    public class GenerationOrchestrator
    {
        private AwarenessSystem awareness;
        private PlannerSystem planner;
        private TaskQueueSystem taskQueue;
        private ExecutionEngine executor;
        private SceneStateTracker sceneState;

        public GenerationOrchestrator()
        {
            awareness = new AwarenessSystem();
            planner = new PlannerSystem();
            taskQueue = new TaskQueueSystem();
            executor = new ExecutionEngine();
            sceneState = new SceneStateTracker();
        }

        public void Execute(string userPrompt)
        {
            var context = awareness.Analyze(userPrompt, sceneState);

            var taskGraph = planner.CreateTasks(context);

            taskQueue.Load(taskGraph);

            executor.Run(taskQueue, sceneState);
        }
    }
}
