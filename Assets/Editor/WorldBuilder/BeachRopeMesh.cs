using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>Small six-sided tubes for fixed Editor-generated rope. No runtime mesh work.</summary>
    internal sealed class BeachRopeMesh
    {
        private const int Sides = 6;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        public void AddTube(IReadOnlyList<Vector3> points, float radius)
        {
            int first = vertices.Count;
            float distance = 0f;
            bool closed = (points[0] - points[points.Count - 1]).sqrMagnitude < 0.000001f;
            for (int ring = 0; ring < points.Count; ring++)
            {
                Vector3 before = points[ring == 0 && closed ? points.Count - 2 : Mathf.Max(0, ring - 1)];
                Vector3 after = points[ring == points.Count - 1 && closed ? 1 : Mathf.Min(ring + 1, points.Count - 1)];
                Vector3 tangent = (after - before).normalized;
                Vector3 across = Vector3.Cross(tangent, Vector3.up).normalized;
                Vector3 other = Vector3.Cross(tangent, across);
                if (ring > 0) distance += Vector3.Distance(points[ring - 1], points[ring]);
                for (int side = 0; side <= Sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / Sides;
                    Vector3 normal = across * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                    vertices.Add(points[ring] + normal * radius);
                    normals.Add(normal);
                    uv.Add(new Vector2((float)side / Sides, distance * 2f));
                    if (ring == points.Count - 1 || side == Sides) continue;
                    int a = first + ring * (Sides + 1) + side, b = a + Sides + 1;
                    triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                    triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
                }
            }
        }

        public Mesh Save(string path, string name)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create = mesh == null;
            if (create) mesh = new Mesh { name = name };
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            return mesh;
        }

        public static Mesh Coil(string path)
        {
            // Sixteen spans with a repeated endpoint; AddTube matches the end tangents at the seam.
            var points = new Vector3[17];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / 16;
                points[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.28f;
            }
            var rope = new BeachRopeMesh();
            rope.AddTube(points, 0.05f);
            return rope.Save(path, "BeachRopeCoil");
        }
    }
}
