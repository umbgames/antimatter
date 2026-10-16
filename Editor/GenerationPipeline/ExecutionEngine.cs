namespace Antimatter
{
    public class ExecutionEngine
    {
        public void Run(TaskQueueSystem queue, SceneStateTracker scene)
        {
            while (queue.HasPendingTasks())
            {
                var task = queue.GetNextPendingTask();

                ToolRouter.Execute(task);

                scene.Refresh();

                queue.MarkCompleted(task.id);
            }
        }
    }
}
