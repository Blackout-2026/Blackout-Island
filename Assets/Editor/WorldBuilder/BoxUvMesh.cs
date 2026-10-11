using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// A unit box mesh (like the cube primitive, so the part's scale still gives its size) whose UVs are world
    /// positions in metres. Each face is projected along its dominant world axis, as the prototype's applyWorldUV does
    /// (geometry.js): top and bottom use (x, z), faces along X use (z, y), faces along Z use (x, y). A material's
    /// tiling then means repeats per metre on every part, whatever its size, and neighbouring parts line up.
    /// The cube primitive's 0–1 UVs would stretch one texture over a 41 m wall and squash it on a 0.4 m sill.
    /// UV2 (lightmaps) is unwrapped at the part's real size, so lightmap texels are spread evenly over its faces.
    /// </summary>
    public static class BoxUvMesh
    {
        public const string NameSuffix = "_BoxUV";

        private static readonly Vector3[] FaceNormals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        /// <summary>World-space box mapping: the UV of a point on a face with this world normal, in metres.</summary>
        public static Vector2 WorldUv(Vector3 worldPoint, Vector3 worldNormal)
        {
            float nx = Mathf.Abs(worldNormal.x);
            float ny = Mathf.Abs(worldNormal.y);
            float nz = Mathf.Abs(worldNormal.z);
            if (ny >= nx && ny >= nz)
            {
                return new Vector2(worldPoint.x, worldPoint.z);
            }

            return nx >= nz ? new Vector2(worldPoint.z, worldPoint.y) : new Vector2(worldPoint.x, worldPoint.y);
        }

        /// <summary>Fills <paramref name="mesh"/> (cleared first) with a unit box mapped for the part's current transform.</summary>
        public static void Build(Mesh mesh, Transform part)
        {
            var vertices = new Vector3[24];
            var normals = new Vector3[24];
            var uvs = new Vector2[24];
            var triangles = new int[36];
            for (int face = 0; face < FaceNormals.Length; face++)
            {
                Vector3 n = FaceNormals[face];
                Vector3 a = new Vector3(n.y, n.z, n.x); // two axes across the face
                Vector3 b = Vector3.Cross(n, a);
                Vector3 worldNormal = part.TransformDirection(n);
                int first = face * 4;
                for (int corner = 0; corner < 4; corner++)
                {
                    float sa = corner < 2 ? -0.5f : 0.5f;
                    float sb = corner == 1 || corner == 2 ? 0.5f : -0.5f;
                    Vector3 local = n * 0.5f + a * sa + b * sb;
                    vertices[first + corner] = local;
                    normals[first + corner] = n;
                    uvs[first + corner] = WorldUv(part.TransformPoint(local), worldNormal);
                }

                // Unity draws clockwise triangles; pick the order whose face normal points outward.
                bool outward = Vector3.Dot(Vector3.Cross(vertices[first + 1] - vertices[first], vertices[first + 2] - vertices[first]), n) > 0f;
                int[] order = outward ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
                for (int i = 0; i < 6; i++)
                {
                    triangles[face * 6 + i] = first + order[i];
                }
            }

            mesh.Clear();
            mesh.name = part.name + NameSuffix;
            Vector3 size = part.localScale;
            mesh.vertices = System.Array.ConvertAll(vertices, v => Vector3.Scale(v, size)); // real size for the unwrap
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            Unwrapping.GenerateSecondaryUVSet(mesh);

            // Back to a unit box, as the part's scale is its size.
            var inverse = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
            mesh.vertices = System.Array.ConvertAll(mesh.vertices, v => Vector3.Scale(v, inverse));
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }
    }
}
