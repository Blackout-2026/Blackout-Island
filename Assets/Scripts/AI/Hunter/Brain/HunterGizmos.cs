// Owner: Systems Engineer (Hunter AI).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// Colours and shapes for the Hunter's Scene-view gizmos (docs/reference/systems-engineer.md section 16). The
    /// colours are the prototype's debug overlay (ui/debug_overlay.js). The shapes are drawn with Gizmos.DrawLine, so
    /// they allocate nothing, and only from OnDrawGizmosSelected.
    /// </summary>
    public static class HunterGizmos
    {
        public static readonly Color Calm = Hex(0x40FF80);
        public static readonly Color Suspicious = Hex(0xFFB030);
        public static readonly Color Combat = Hex(0xFF3020);
        public static readonly Color Gunfire = Hex(0xFF5030);
        public static readonly Color Machine = Hex(0xFF30FF);
        public static readonly Color Footsteps = Hex(0xFFD040);
        public static readonly Color Walking = new Color(0.2f, 0.35f, 1f);
        public static readonly Color Running = new Color(0.35f, 0.85f, 1f);
        public static readonly Color Route = Color.cyan;
        public static readonly Color Heard = Color.green;
        public static readonly Color NotHeard = new Color(0.5f, 0.5f, 0.5f, 0.6f);

        private const int CircleSegments = 48;

        /// <summary>The prototype's state colours: combat red, suspicious orange, otherwise calm green.</summary>
        public static Color ForBranch(string branch)
        {
            switch (branch)
            {
                case "Attack":
                case "Pursue":
                    return Combat;
                case "Investigate":
                case "CautiousApproach":
                case "Search":
                case "FollowOrders":
                    return Suspicious;
                default:
                    return Calm;
            }
        }

        /// <summary>The prototype's noise ring colours: gunfire, the generator and alarm, everything else.</summary>
        public static Color ForNoise(NoiseType type)
        {
            switch (type)
            {
                case NoiseType.Gunshot:
                    return Gunfire;
                case NoiseType.Generator:
                case NoiseType.Alarm:
                    return Machine;
                default:
                    return Footsteps;
            }
        }

        /// <summary>A flat circle (around +y) of line segments.</summary>
        public static void Circle(Vector3 centre, float radius)
        {
            Arc(centre, Vector3.forward, 360f, radius);
        }

        /// <summary>A flat arc of <paramref name="degrees"/> centred on <paramref name="forward"/>.</summary>
        public static void Arc(Vector3 centre, Vector3 forward, float degrees, float radius)
        {
            if (radius <= 0f)
            {
                return;
            }

            var flat = new Vector3(forward.x, 0f, forward.z);
            float start = (flat.sqrMagnitude > 1e-8f ? Mathf.Atan2(flat.x, flat.z) : 0f) - degrees * 0.5f * Mathf.Deg2Rad;
            int segments = Mathf.Max(4, Mathf.CeilToInt(CircleSegments * degrees / 360f));
            float step = degrees * Mathf.Deg2Rad / segments;
            Vector3 previous = centre + Direction(start) * radius;
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = centre + Direction(start + step * i) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        /// <summary>The flat unit vector at <paramref name="radians"/> clockwise from +z.</summary>
        public static Vector3 Direction(float radians)
        {
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        public static Color Faded(Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            return color;
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }
    }
}
