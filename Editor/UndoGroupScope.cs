using System;
using UnityEditor;

namespace Antimatter
{
    public sealed class UndoGroupScope : IDisposable
    {
        private readonly int group;

        public UndoGroupScope(string name)
        {
            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
        }

        public void Dispose()
        {
            Undo.CollapseUndoOperations(group);
        }
    }
}
