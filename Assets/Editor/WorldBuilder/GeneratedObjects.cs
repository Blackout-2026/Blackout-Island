using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Lets World Builder tools re-run safely. A generated object is named after its container plus "_"
    /// (for example "IslandBoundary_042"). A rebuild reuses each generated object by name, so its object ID and
    /// the scene file stay the same when nothing changed, then removes generated children it no longer needs.
    /// Anything a person adds to the container under another name is left alone.
    /// </summary>
    public static class GeneratedObjects
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

        /// <summary>
        /// Returns the generated child "&lt;container&gt;_&lt;part&gt;", reusing it if it exists, and records its name
        /// in <paramref name="built"/>. A new child is an empty object, or the given primitive (with its collider).
        /// </summary>
        public static GameObject GetOrCreate(Transform container, string part, ISet<string> built, string undoLabel,
            PrimitiveType? primitive = null)
        {
            string childName = Name(container, part);
            built.Add(childName);
            Transform existing = container.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject go = primitive.HasValue ? GameObject.CreatePrimitive(primitive.Value) : new GameObject();
            go.name = childName;
            go.transform.SetParent(container, false);
            Undo.RegisterCreatedObjectUndo(go, undoLabel);
            return go;
        }

        /// <summary>Destroys generated children that this build did not produce, and returns how many.</summary>
        public static int RemoveStale(Transform container, ISet<string> built)
        {
            string prefix = container.name + "_";
            int removed = 0;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                GameObject child = container.GetChild(i).gameObject;
                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal) && !built.Contains(child.name))
                {
                    Undo.DestroyObjectImmediate(child);
                    removed++;
                }
            }

            return removed;
        }

        public static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
    }
}
