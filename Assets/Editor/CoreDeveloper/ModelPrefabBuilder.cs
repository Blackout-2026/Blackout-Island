using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds one prop, pickup or weapon prefab from its <see cref="ModelDefinition"/>: a "Model" child (the FBX when it
    /// exists, otherwise the primitive placeholder), the colliders on the root, and the sockets. Same rules as the robot
    /// prefab (docs/art-pipeline.md section 13): re-runnable, only the Model child is replaced, a collider or socket that
    /// already exists is kept, and no gameplay script is added (the Systems Engineer nests these).
    /// </summary>
    public static class ModelPrefabBuilder
    {
        public const string ModelName = "Model";

        /// <summary>Creates or updates the prefab at <c>entry.PrefabPath</c>.</summary>
        /// <param name="withColliders">Props have colliders; visual-only prefabs (pickups, weapons) do not.</param>
        /// <param name="extras">Extra children to ensure on the root (a muzzle flash). Called every run, so it must be idempotent.</param>
        /// <param name="rebuildPlaceholder">Replace an existing placeholder Model with a fresh one (after the definition changed). Off by default so a re-run leaves the prefab file untouched.</param>
        public static void Build(PropEntry entry, ModelDefinition definition, PartMaterials materials, bool withColliders,
            Action<GameObject> extras = null, bool rebuildPlaceholder = false)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(entry.FbxPath);
            ModelMaterials.EnsureFolder(entry.Folder);
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(entry.PrefabPath) : new GameObject(entry.Name);
            try
            {
                Populate(root, entry, definition, fbx, materials, withColliders, rebuildPlaceholder);
                extras?.Invoke(root);
                PrefabUtility.SaveAsPrefabAsset(root, entry.PrefabPath);
            }
            finally
            {
                if (exists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        /// <summary>Fills a root with the model, colliders and sockets. Safe to call repeatedly.</summary>
        public static void Populate(GameObject root, PropEntry entry, ModelDefinition definition, GameObject fbx, PartMaterials materials,
            bool withColliders, bool rebuildPlaceholder = false)
        {
            ReplaceModel(root.transform, definition, fbx, materials, rebuildPlaceholder);
            if (withColliders)
            {
                PlaceholderBuilder.AddColliders(root, definition);
            }

            foreach (SocketSpec socket in definition.Sockets)
            {
                EnsureSocket(root.transform, socket);
            }

            if (fbx != null)
            {
                ReportProblems(root, entry);
            }
        }

        /// <summary>
        /// Replaces every imported material slot with the asset of the same name (a Blender material called M_Prop_Steel
        /// becomes Assets/Materials/Models/M_Prop_Steel.mat). A name that matches none stays as imported, with a warning.
        /// </summary>
        public static void AssignMaterials(GameObject instance, PartMaterials materials)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string slotName = slots[i] != null ? slots[i].name : string.Empty;
                    Material replacement = materials.Find(slotName);
                    if (replacement != null)
                    {
                        slots[i] = replacement;
                    }
                    else
                    {
                        Debug.LogWarning($"[ModelPrefab] {renderer.name} slot {i} uses '{slotName}', which matches no {materials.Prefix}* material.", renderer);
                    }
                }

                renderer.sharedMaterials = slots;
            }
        }

        private static void ReplaceModel(Transform root, ModelDefinition definition, GameObject fbx, PartMaterials materials, bool rebuildPlaceholder)
        {
            Transform old = root.Find(ModelName);
            if (old != null && IsCurrentModel(old.gameObject, fbx) && !(fbx == null && rebuildPlaceholder))
            {
                if (fbx != null)
                {
                    AssignMaterials(old.gameObject, materials);
                }

                return;
            }

            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            if (fbx != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root);
                instance.name = ModelName;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                AssignMaterials(instance, materials);
                return;
            }

            var placeholder = new GameObject(ModelName).transform;
            placeholder.SetParent(root, false);
            PlaceholderBuilder.Build(placeholder, definition, materials);
        }

        // True when the Model child is already what this run would build: the placeholder while there is no FBX, or an
        // instance of this very FBX once there is one.
        private static bool IsCurrentModel(GameObject model, GameObject fbx)
        {
            if (fbx == null)
            {
                return !PrefabUtility.IsPartOfPrefabInstance(model);
            }

            return PrefabUtility.GetCorrespondingObjectFromSource(model) == fbx;
        }

        // Does nothing if the socket exists anywhere in the prefab (moved by hand, or exported from Blender as an Empty).
        private static void EnsureSocket(Transform root, SocketSpec spec)
        {
            if (RobotRig.FindDeep(root, spec.Name) != null)
            {
                return;
            }

            Transform parent = string.IsNullOrEmpty(spec.Parent) ? root : RobotRig.FindDeep(root, spec.Parent) ?? root;
            Transform socket = new GameObject(spec.Name).transform;
            socket.SetParent(parent, false);
            socket.localPosition = spec.Position;
            socket.localRotation = Quaternion.Euler(spec.EulerDegrees);
        }

        // An FBX replaced the placeholder: say so when its size or pivot differ from the catalog.
        private static void ReportProblems(GameObject root, PropEntry entry)
        {
            Bounds bounds = PropStats.LocalBounds(root);
            string pivot = PivotRules.CheckPivot(bounds, entry.Pivot);
            string size = entry.Pivot == PropPivot.Grip ? null : PivotRules.CheckSize(bounds, entry.Size);
            if (pivot != null)
            {
                Debug.LogWarning($"[ModelPrefab] {entry.Name}: pivot is wrong, {pivot} (docs/art-pipeline.md section 5).");
            }

            if (size != null)
            {
                Debug.LogWarning($"[ModelPrefab] {entry.Name}: {size}.");
            }
        }
    }
}
