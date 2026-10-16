namespace Antimatter
{
    public static class ToolRouter
    {
        public static void Execute(TaskNode task)
        {
            if (string.IsNullOrWhiteSpace(task.tool))
            {
                UnityEngine.Debug.LogWarning($"[ToolRouter] Task {task.id} has no tool defined.");
                return;
            }

            var result = EditorToolDispatcher.Dispatch(task.tool, task.args);
            
            if (result.Success)
            {
                UnityEngine.Debug.Log($"[ToolRouter] Success: {task.tool} - {result.Output}");
            }
            else
            {
                UnityEngine.Debug.LogError($"[ToolRouter] Failed: {task.tool} - {result.Error}");
            }
        }
    }
}
