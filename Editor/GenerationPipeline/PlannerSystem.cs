using System.Collections.Generic;

namespace Antimatter
{
    public class PlannerSystem
    {
        public List<TaskNode> CreateTasks(AwarenessContext context)
        {
            if (string.IsNullOrWhiteSpace(context.Prompt)) return new List<TaskNode>();

            try
            {
                var generation = Newtonsoft.Json.JsonConvert.DeserializeObject<GameGeneration>(context.Prompt);
                return generation?.tasks ?? new List<TaskNode>();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[PlannerSystem] Failed to parse JSON plan: {e.Message}");
                return new List<TaskNode>();
            }
        }
    }
}
