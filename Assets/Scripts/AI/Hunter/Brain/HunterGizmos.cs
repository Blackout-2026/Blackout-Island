// Owner: Systems Engineer (Hunter AI).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// Where the Hunter's gizmos are drawn. In the Editor it is <see cref="UnityGizmoPainter"/> (Unity's Gizmos, which
    /// only work inside OnDrawGizmos*); tests pass a recording painter, so every line of the drawing code runs and what
    /// it would draw can be checked.
    /// </summary>
    public interface IGizmoPainter
    {
        Color Color { get; set; }
        void Line(Vector3 from, Vector3 to);
        void WireSphere(Vector3 centre, float radius);
        void WireCube(Vector3 centre, Vector3 size);
    }

    /// <summary>Draws with Unity's Gizmos. Use it only from OnDrawGizmos or OnDrawGizmosSelected.</summary>
    public sealed class UnityGizmoPainter : IGizmoPainter
    {
        public static readonly UnityGizmoPainter Instance = new UnityGizmoPainter();

        public Color Color
        {
            get => Gizmos.color;
            set => Gizmos.color = value;
        }

        public void Line(Vector3 from, Vector3 to) => Gizmos.DrawLine(from, to);
        public void WireSphere(Vector3 centre, float radius) => Gizmos.DrawWireSphere(centre, radius);
        public void WireCube(Vector3 centre, Vector3 size) => Gizmos.DrawWireCube(centre, size);
    }

    /// <summary>
    /// Colours and shapes for the Hunter's Scene-view gizmos (docs/reference/systems-engineer.md section 16). The
    /// colours are the prototype's debug overlay (ui/debug_overlay.js). Shapes are made of lines, so drawing
    /// allocates nothing.
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
        public static void Circle(IGizmoPainter painter, Vector3 centre, float radius)
        {
            Arc(painter, centre, Vector3.forward, 360f, radius);
        }

        /// <summary>A flat arc of <paramref name="degrees"/> centred on <paramref name="forward"/>.</summary>
        public static void Arc(IGizmoPainter painter, Vector3 centre, Vector3 forward, float degrees, float radius)
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
                painter.Line(previous, next);
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
