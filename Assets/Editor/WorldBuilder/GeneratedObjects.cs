using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Lets World Builder tools re-run safely. A generated object is named after its container plus "_"
    /// (for example "IslandBoundary_042"), and a rebuild removes only those children. Anything a person adds to
    /// the container under another name is left alone.
    /// </summary>
    internal static class GeneratedObjects
    {
        /// <summary>Name for a generated child of this container.</summary>
        public static string Name(Transform container, string part)
        {
            return container.name + "_" + part;
        }

        /// <summary>Returns the direct child with this name, creating an empty one if it is missing.</summary>
        public static Transform FindOrCreateChild(Transform parent, string childName, string undoLabel)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, undoLabel);
            return go.transform;
        }

        /// <summary>Destroys the container's generated children and returns how many were removed.</summary>
        public static int RemoveGenerated(Transform container)
        {
            string prefix = container.name + "_";
            int removed = 0;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                GameObject child = container.GetChild(i).gameObject;
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    Undo.DestroyObjectImmediate(child);
                    removed++;
                }
            }

            return removed;
        }
    }
}
