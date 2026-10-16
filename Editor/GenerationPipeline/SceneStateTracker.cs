using System.Collections.Generic;

namespace Antimatter
{
    public class SceneObject
    {
        public string Type;
        public string Identifier;
    }

    public class SceneStateTracker
    {

        public bool ObjectExists(string type, string identifier)
        {
            return false;
        }

        public List<SceneObject> GetAllObjects()
        {
            return new List<SceneObject>();
        }

        public void Refresh()
        {
        }
    }
}
