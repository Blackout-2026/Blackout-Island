using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.Editor
{
    /// <summary>
    /// Collects vertices (position, normal, uv) and triangles per submesh, then makes a Mesh. Used by
    /// <see cref="VegetationMeshes"/> to port the prototype's three.js geometry builders: vertex positions and triangle
    /// order are copied as they are, which mirrors the shape (three.js is right-handed, Unity left-handed) and keeps
    /// every face pointing outward.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int>[] triangles;

        public MeshBuilder(int submeshes)
        {
            triangles = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++)
            {
                triangles[i] = new List<int>();
            }
        }

        public int VertexCount => positions.Count;

        public int TriangleCount
        {
            get
            {
                int count = 0;
                foreach (List<int> list in triangles)
                {
                    count += list.Count / 3;
                }

                return count;
            }
        }

        public int Add(Vector3 position, Vector3 normal, Vector2 uv)
        {
            positions.Add(position);
            normals.Add(normal);
            uvs.Add(uv);
            return positions.Count - 1;
        }

        public void Triangle(int submesh, int a, int b, int c)
        {
            triangles[submesh].Add(a);
            triangles[submesh].Add(b);
            triangles[submesh].Add(c);
        }

        public Vector3 Position(int index)
        {
            return positions[index];
        }

        public Vector3 Normal(int index)
        {
            return normals[index];
        }

        public void Set(int index, Vector3 position, Vector3 normal)
        {
            positions[index] = position;
            normals[index] = normal;
        }

        /// <summary>
        /// Smooth normals for the vertices from <paramref name="first"/> on: the area-weighted sum of the faces that
        /// use each vertex (three.js computeVertexNormals on an indexed geometry).
        /// </summary>
        public void SmoothNormals(int first)
        {
            for (int i = first; i < normals.Count; i++)
            {
                normals[i] = Vector3.zero;
            }

            foreach (List<int> list in triangles)
            {
                for (int t = 0; t < list.Count; t += 3)
                {
                    int a = list[t];
                    int b = list[t + 1];
                    int c = list[t + 2];
                    if (a < first && b < first && c < first)
                    {
                        continue;
                    }

                    Vector3 face = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                    foreach (int v in new[] { a, b, c })
                    {
                        if (v >= first)
                        {
                            normals[v] += face;
                        }
                    }
                }
            }

            // A vertex used only by zero-area triangles (a frond's pointed tip) has no normal: it gets "up".
            for (int i = first; i < normals.Count; i++)
            {
                normals[i] = normals[i].sqrMagnitude > 1e-12f ? normals[i].normalized : Vector3.up;
            }
        }

        /// <summary>Moves and turns the vertices from <paramref name="first"/> on (normals turn too).</summary>
        public void Transform(int first, Quaternion rotation, Vector3 offset)
        {
            for (int i = first; i < positions.Count; i++)
            {
                positions[i] = rotation * positions[i] + offset;
                normals[i] = rotation * normals[i];
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++)
            {
                mesh.SetTriangles(triangles[i], i);
            }

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
