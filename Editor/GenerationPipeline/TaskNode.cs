using System.Collections.Generic;

namespace Antimatter
{
    [System.Serializable]
    public class TaskNode
    {
        public string id;
        public string tool;
        public Newtonsoft.Json.Linq.JObject args;
        public string description;
    }

    [System.Serializable]
    public class GameGeneration
    {
        public List<TaskNode> tasks;
    }
}
