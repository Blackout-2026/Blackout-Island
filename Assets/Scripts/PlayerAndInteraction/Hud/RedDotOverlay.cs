// GN1: the view through the SMG's red-dot sight. Mesh only (no texture, no extra camera), like ScopeOverlay.
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>
    /// A black surround with a round clear window (soft at its edge, 36% of the viewport height in radius, clamped to fit narrow views), a faint red
    /// tint on the glass, a thin red coating ring at the window's edge, and the red dot with its glow at the centre. No crosshair lines: a reflex sight
    /// has none.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RedDotOverlay : MaskableGraphic
    {
        private const int Segments = 96;
        public const float RadiusOfHeight = 0.36f;
        public const float DotRadius = 5.5f;
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);
        private static readonly Color32 DotRed = new Color32(255, 36, 24, 255);

        public RedDotOverlay()
        {
            useLegacyMeshGeneration = false;
        }

        /// <summary>The window's radius in canvas units for a viewport of this size.</summary>
        public static float WindowRadius(float width, float height)
        {
            return Mathf.Min(height * RadiusOfHeight, width * 0.46f);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;
            Vector2 centre = rect.center;
            float radius = WindowRadius(rect.width, rect.height);
            float outside = rect.size.magnitude * 0.55f;   // encloses every viewport corner; the screen clips the excess
            float feather = radius * 0.06f;

            Ring(mesh, centre, radius - feather, radius, Clear, Black);                 // soft inner edge of the surround
            Ring(mesh, centre, radius, outside, Black, Black);                          // the surround
            Ring(mesh, centre, radius - 3f, radius, new Color32(150, 14, 14, 0), new Color32(150, 14, 14, 140)); // red coating at the rim
            Ring(mesh, centre, 0f, radius - feather, new Color32(255, 30, 20, 9), new Color32(255, 30, 20, 9)); // faint red glass
            Ring(mesh, centre, 0f, 15f, new Color32(255, 36, 24, 80), new Color32(255, 36, 24, 0));              // the dot's glow
            Ring(mesh, centre, 0f, DotRadius, DotRed, DotRed);                                                    // the dot
        }

        /// <summary>A ring (or a disc when the inner radius is 0) with one colour at its inner edge and one at its outer edge.</summary>
        private static void Ring(VertexHelper mesh, Vector2 centre, float inner, float outer, Color32 innerColor, Color32 outerColor)
        {
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float b = (i + 1) * Mathf.PI * 2f / Segments;
                Vector2 first = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 second = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int start = mesh.currentVertCount;
                mesh.AddVert(centre + first * inner, innerColor, Vector2.zero);
                mesh.AddVert(centre + first * outer, outerColor, Vector2.zero);
                mesh.AddVert(centre + second * outer, outerColor, Vector2.zero);
                mesh.AddVert(centre + second * inner, innerColor, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
