using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.Editor
{
    /// <summary>
    /// Reads counts from a mesh without needing Read/Write enabled: index counts and vertex attributes are
    /// metadata, while Mesh.triangles would log an error on a mesh that is not readable.
    /// </summary>
    public static class MeshStats
    {
        public static int CountTriangles(Mesh mesh)
        {
            int triangles = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                int indices = (int)mesh.GetIndexCount(i);
                switch (mesh.GetTopology(i))
                {
                    case MeshTopology.Triangles:
                        triangles += indices / 3;
                        break;
                    case MeshTopology.Quads:
                        triangles += indices / 4 * 2; // a quad is drawn as two triangles
                        break;
                    // Lines and points have no area, so they add no triangles.
                }
            }

            return triangles;
        }

        public static bool HasUv0(Mesh mesh)
        {
            return mesh.HasVertexAttribute(VertexAttribute.TexCoord0);
        }
    }
}
