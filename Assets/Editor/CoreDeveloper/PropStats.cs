using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// What the Props palette and the builder tests read from a prefab: triangles, materials, colliders and the size of
    /// its visible meshes. Works on a loaded prefab asset or an instance, and never needs Read/Write on a mesh.
    /// </summary>
    public static class PropStats
    {
        /// <summary>Triangles of every mesh below the root, inactive children included (a hidden flash still costs when shown).</summary>
        public static int Triangles(GameObject root)
        {
            int triangles = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    triangles += MeshStats.CountTriangles(filter.sharedMesh);
                }
            }

            return triangles;
        }

        /// <summary>The number of different material assets used by the renderers below the root.</summary>
        public static int Materials(GameObject root)
        {
            var distinct = new HashSet<Material>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        distinct.Add(material);
                    }
                }
            }

            return distinct.Count;
        }

        public static int Colliders(GameObject root)
        {
            return root.GetComponentsInChildren<Collider>(true).Length;
        }

        /// <summary>
        /// The box round the active meshes, in the root's own axes (so it is the model's size in metres whatever the
        /// root's position). Inactive children such as the muzzle flash are left out. Zero when there is no mesh.
        /// </summary>
        public static Bounds LocalBounds(GameObject root)
        {
            var bounds = new Bounds();
            bool first = true;
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                Bounds mesh = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                        (corner & 4) == 0 ? mesh.min.z : mesh.max.z);
                    Vector3 local = matrix.MultiplyPoint3x4(point);
                    if (first)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }

            return bounds;
        }
    }
}
