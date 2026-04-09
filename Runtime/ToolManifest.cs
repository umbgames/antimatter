using System;
using System.Collections.Generic;

namespace Antimatter
{
    [Serializable]
    public class ToolManifest
    {
        public string id;
        public string name;
        public string version;
        public string author;
        public string description;
        public string ai_description;
        public string category;
        public List<string> dependencies;
        public string entry_point;
    }
}
