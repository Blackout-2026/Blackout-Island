using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds a <see cref="ModelDefinition"/> out of Unity primitives under a parent: the stand-in for an SM_*.fbx
    /// (the robot's placeholder works the same way). Parts get no colliders (a prop's go on the prefab root) and the
    /// sockets are not built here: <see cref="ModelPrefabBuilder"/> adds them next to the model so they survive a swap.
    /// </summary>
    public static class PlaceholderBuilder
    {
        /// <summary>Creates the pivots and parts of the definition below <paramref name="parent"/>.</summary>
        public static void Build(Transform parent, ModelDefinition definition, PartMaterials materials)
        {
            var pivots = new Dictionary<string, Transform>();
            foreach (PivotSpec pivot in definition.Pivots)
            {
                Transform t = new GameObject(pivot.Name).transform;
                t.SetParent(Resolve(parent, pivots, pivot.Parent, pivot.Name), false);
                t.localPosition = pivot.Position;
                t.localRotation = Quaternion.Euler(pivot.EulerDegrees);
                pivots[pivot.Name] = t;
            }

            foreach (PartSpec part in definition.Parts)
            {
                AddPart(Resolve(parent, pivots, part.Parent, part.Name), part, materials);
            }

        }

        private static Transform Resolve(Transform root, Dictionary<string, Transform> pivots, string parentName, string child)
        {
            if (string.IsNullOrEmpty(parentName))
            {
                return root;
            }

            if (!pivots.TryGetValue(parentName, out Transform pivot))
            {
                throw new KeyNotFoundException($"'{child}' hangs off pivot '{parentName}', which the definition does not declare.");
            }

            return pivot;
        }

        private static void AddPart(Transform parent, PartSpec part, PartMaterials materials)
        {
            GameObject go = GameObject.CreatePrimitive(part.Shape == PartShape.Box ? PrimitiveType.Cube
                : part.Shape == PartShape.Sphere ? PrimitiveType.Sphere
                : PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = part.Name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = part.Position;

            // A Unity cylinder runs along its own Y, 2 m long and 1 m wide: scale is (diameter, length / 2, diameter).
            // Turn it onto X or Z first, then apply the part's own tilt on top.
            Quaternion axis = part.Shape == PartShape.CylinderX ? Quaternion.Euler(0f, 0f, 90f)
                : part.Shape == PartShape.CylinderZ ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.identity;
            go.transform.localRotation = Quaternion.Euler(part.TiltDegrees) * axis;

            bool cylinder = part.Shape == PartShape.CylinderX || part.Shape == PartShape.CylinderY || part.Shape == PartShape.CylinderZ;
            go.transform.localScale = cylinder ? new Vector3(part.Size.x, part.Size.y / 2f, part.Size.z) : part.Size;

            Material material = materials.Get(part.Paint);
            if (material == null)
            {
                Debug.LogWarning($"[Placeholder] Part '{part.Name}' names the paint '{part.Paint}', which {materials.Prefix}* does not have.");
            }

            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>Adds the definition's colliders to the root. A root that already has any collider is left alone.</summary>
        public static void AddColliders(GameObject root, ModelDefinition definition)
        {
            if (root.GetComponent<Collider>() != null)
            {
                return;
            }

            foreach (ColliderSpec spec in definition.Colliders)
            {
                if (spec.Shape == ColliderShape.Box)
                {
                    var box = root.AddComponent<BoxCollider>();
                    box.center = spec.Center;
                    box.size = spec.Size;
                }
                else
                {
                    var capsule = root.AddComponent<CapsuleCollider>();
                    capsule.center = spec.Center;
                    capsule.radius = spec.Size.x;
                    capsule.height = spec.Size.y;
                    capsule.direction = 1; // Y
                }
            }
        }
    }
}
