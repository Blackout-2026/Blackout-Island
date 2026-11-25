using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// The sea surface mesh: a flat square grid at y 0 in its own space, with up-facing normals. The Blackout/Ocean
    /// shader moves the vertices on the GPU, so the mesh itself never changes and costs nothing per frame on the CPU.
    /// 128 x 128 cells over 520 m is a 4.06 m cell, so the shortest moving swell (18 m) still spans 4 cells.
    /// </summary>
    public static class OceanMesh
    {
        public const string MeshName = "OceanSurface";
        public const int Cells = 128;
        public const float Size = 520f;

        // The three swells in the shader add up to at most 0.15 m; the bounds carry a little more so culling stays right.
        public const float MaxWaveHeight = 0.2f;

        public static Mesh Create(int cells = Cells, float size = Size)
        {
            int row = cells + 1;
            var vertices = new Vector3[row * row];
            var normals = new Vector3[row * row];
            var uv = new Vector2[row * row];
            for (int j = 0; j < row; j++)
            {
                for (int i = 0; i < row; i++)
                {
                    float u = i / (float)cells;
                    float v = j / (float)cells;
                    int index = j * row + i;
                    vertices[index] = new Vector3((u - 0.5f) * size, 0f, (v - 0.5f) * size);
                    normals[index] = Vector3.up;
                    uv[index] = new Vector2(u, v);
                }
            }

            // Two triangles per cell, clockwise seen from above (Unity's front face), so the top of the water is drawn.
            var triangles = new int[cells * cells * 6];
            int t = 0;
            for (int j = 0; j < cells; j++)
            {
                for (int i = 0; i < cells; i++)
                {
                    int a = j * row + i; // x, z
                    int b = a + row; // x, z + 1
                    int c = a + 1; // x + 1, z
                    int d = b + 1; // x + 1, z + 1
                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = b;
                    triangles[t++] = d;
                    triangles[t++] = c;
                }
            }

            var mesh = new Mesh { name = MeshName };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, MaxWaveHeight * 2f, size));
            return mesh;
        }
    }
}
