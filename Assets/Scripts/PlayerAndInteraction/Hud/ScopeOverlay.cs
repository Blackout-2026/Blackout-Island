// Owner: Systems Engineer. SC1: mesh-based scope mask; no generated texture or extra camera.
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScopeOverlay : MaskableGraphic
    {
        private const int Segments = 96;

        public ScopeOverlay()
        {
            useLegacyMeshGeneration = false; // Graphic defaults to its obsolete Mesh overload; this class uses VertexHelper.
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            Vector2 centre = rect.center;
            float radius = Mathf.Min(rect.height * 0.34f, rect.width * 0.48f);
            // The outer polygon encloses every viewport corner; the screen clips its excess.
            float outside = rect.size.magnitude * 0.55f;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float b = (i + 1) * Mathf.PI * 2f / Segments;
                Vector2 first = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 second = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Quad(mesh, centre + first * radius, centre + first * outside,
                    centre + second * outside, centre + second * radius, Color.black);
            }
            // Two fine reticle lines and the prototype's red centre marker.
            Box(mesh, centre, new Vector2(radius * 2f, 2f), Color.black);
            Box(mesh, centre, new Vector2(2f, radius * 2f), Color.black);
            Box(mesh, centre, new Vector2(6f, 6f), new Color32(255, 42, 26, 255));
        }

        private static void Box(VertexHelper mesh, Vector2 centre, Vector2 size, Color32 color)
        {
            Vector2 half = size * 0.5f;
            Quad(mesh, centre + new Vector2(-half.x, -half.y), centre + new Vector2(-half.x, half.y),
                centre + half, centre + new Vector2(half.x, -half.y), color);
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddVert(d, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
