using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Finds the island's coastline as a closed polygon (x, z), for the invisible boundary walls.
    /// From the island centre it casts a ray every 360 / rays degrees and walks each one inward from far out at
    /// sea until the ground rises to <see cref="ShoreHeight"/>, or the ray enters the dock and boat area, which
    /// must stay inside. Walking inward rather than outward means a low inland hollow never stops a ray early.
    /// Pure and deterministic, so EditMode tests can check it.
    /// </summary>
    public static class IslandBoundary
    {
        /// <summary>The wall stands where the water is 0.6 m deep: the player can wade, not swim away.</summary>
        public const float ShoreHeight = -0.6f;

        /// <summary>Where each ray starts. Stays inside the 400 m terrain and is well past the coast (about 160 m).</summary>
        public const float MaxRadius = 190f;

        public const float StepLength = 0.5f;

        /// <summary>Each wall point is this far seaward of the first ground found, so the wall is in open water.</summary>
        public const float Offset = 1f;

        /// <summary>Dock, boat and boat exit zone (x -10 .. 12, z -132 .. -92): the boundary goes round it.</summary>
        public static readonly Rect KeepInside = Rect.MinMaxRect(-10f, -132f, 12f, -92f);

        public static Vector2 Centre => new Vector2(0f, IslandLayout.IslandCentreZ);

        public static Vector2[] TraceCoast(int rays)
        {
            var points = new Vector2[rays];
            for (int i = 0; i < rays; i++)
            {
                // Angle 0 points north (+Z) and the angle grows clockwise, toward east (+X).
                float angle = i * 2f * Mathf.PI / rays;
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));

                float r = MaxRadius;
                while (r > 0f)
                {
                    Vector2 p = Centre + direction * r;
                    if (KeepInside.Contains(p) || IslandHeightField.Sample(p.x, p.y) >= ShoreHeight)
                    {
                        break;
                    }

                    r -= StepLength;
                }

                points[i] = Centre + direction * (r + Offset);
            }

            return points;
        }
    }
}
