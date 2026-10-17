// Owner: Core Developer (Tactical Guard AI).
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>Small pure helpers behind the guard's inputs and targets.</summary>
    public static class GuardMath
    {
        /// <summary>value / range clamped to 0..1; a non-positive range counts as 1 so nothing divides by zero.</summary>
        public static float Normalise(float value, float range)
        {
            return range <= 0f ? 1f : Mathf.Clamp01(value / range);
        }

        /// <summary>1 when age is 0, falling linearly to 0 at the window. Never seen (infinite age) is 0.</summary>
        public static float Freshness(float age, float window)
        {
            if (float.IsNaN(age) || float.IsPositiveInfinity(age))
            {
                return 0f;
            }
            return 1f - Normalise(Mathf.Max(0f, age), window);
        }

        /// <summary>Sum of the weighted threat sources, clamped to 0..1. recentDamage is a freshness (1 = just hit).</summary>
        public static float Threat(bool canSeePlayer, float recentDamage, bool aimedAt, bool alert, TacticalGuardConfig config)
        {
            float threat = (canSeePlayer ? config.threatVisible : 0f)
                         + Mathf.Clamp01(recentDamage) * config.threatRecentDamage
                         + (canSeePlayer && aimedAt ? config.threatAimedAt : 0f)
                         + (alert ? config.threatAlert : 0f);
            return Mathf.Clamp01(threat);
        }

        /// <summary>Straight-line distance ignoring height.</summary>
        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Where to stand when looking for the player: the last known position, pulled back along the line from the
        /// post so it is never further than the limit (prototype: post + (lastKnown - post) clamped to 15 m / 30 m).
        /// </summary>
        public static Vector3 RepositionTarget(Vector3 post, Vector3 lastKnown, float limit)
        {
            float dx = lastKnown.x - post.x;
            float dz = lastKnown.z - post.z;
            float length = Mathf.Sqrt(dx * dx + dz * dz);
            if (length > limit && length > 0.0001f)
            {
                float scale = limit / length;
                dx *= scale;
                dz *= scale;
            }
            return new Vector3(post.x + dx, post.y, post.z + dz);
        }

        /// <summary>The point the head looks at while holding the post: a circle around the post (prototype sin/cos of scanT * 0.5).</summary>
        public static Vector3 ScanPoint(Vector3 post, float time, TacticalGuardConfig config)
        {
            float angle = time * config.scanSpeed;
            return new Vector3(
                post.x + Mathf.Sin(angle) * config.scanRadius,
                post.y + config.scanHeight,
                post.z + Mathf.Cos(angle) * config.scanRadius);
        }
    }
}
