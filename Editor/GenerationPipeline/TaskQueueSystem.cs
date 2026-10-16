using System.Collections.Generic;

namespace Antimatter
{
    public class TaskQueueSystem
    {
        private Queue<TaskNode> pendingTasks = new Queue<TaskNode>();

        public void Load(List<TaskNode> taskGraph)
        {
            pendingTasks.Clear();
            foreach(var t in taskGraph)
                pendingTasks.Enqueue(t);
        }

        public bool HasPendingTasks()
        {
            return pendingTasks.Count > 0;
        }

        public TaskNode GetNextPendingTask()
        {
            return pendingTasks.Dequeue();
        }

        public void MarkSkipped(string id) { }
        public void MarkCompleted(string id) { }
    }
}
