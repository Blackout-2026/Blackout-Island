// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// The weapon rules as pure functions (falloff, spread, bloom, melee cone), so EditMode tests can check them
    /// without a scene. The weapon MonoBehaviours call these and only apply the result.
    /// Numbers are from docs/reference/systems-engineer.md section 7.
    /// </summary>
    public static class WeaponMath
    {
        /// <summary>Extra spread (degrees) while in the air: 0.02 rad.</summary>
        public const float AirborneSpread = 1.15f;
        /// <summary>Bloom recovery (degrees per second): 0.06 rad/s.</summary>
        public const float BloomRecoveryPerSecond = 3.44f;
        /// <summary>A melee target counts if its centre is this far beyond the reach (m).</summary>
        public const float MeleeReachTolerance = 0.4f;

        /// <summary>The prototype's smoothstep: 0 before edge0, 1 after edge1, an S-curve in between.</summary>
        public static float Smoothstep(float edge0, float edge1, float x)
        {
            if (edge1 <= edge0)
            {
                return x < edge0 ? 0f : 1f;
            }

            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Damage multiplier at a distance: 1 up to falloffStart, easing down to falloffMinMultiplier at falloffEnd.
        /// </summary>
        public static float FalloffMultiplier(float distance, float falloffStart, float falloffEnd, float minMultiplier)
        {
            return Mathf.Lerp(1f, minMultiplier, Smoothstep(falloffStart, falloffEnd, distance));
        }

        public static float FalloffMultiplier(float distance, WeaponData data)
        {
            return FalloffMultiplier(distance, data.falloffStart, data.falloffEnd, data.falloffMinMultiplier);
        }

        /// <summary>
        /// Current spread half-angle in degrees. moveFraction is speed / walk speed, clamped to 0..1.
        /// </summary>
        public static float Spread(WeaponData data, float moveFraction, float bloom, bool grounded)
        {
            float spread = data.spread + data.moveSpread * Mathf.Clamp01(moveFraction) + bloom;
            return grounded ? spread : spread + AirborneSpread;
        }

        /// <summary>Bloom after one more shot, capped at the weapon's maximum.</summary>
        public static float AddBloom(float bloom, WeaponData data)
        {
            return Mathf.Min(data.bloomMax, bloom + data.bloomPerShot);
        }

        public static float RecoverBloom(float bloom, float dt)
        {
            return Mathf.Max(0f, bloom - BloomRecoveryPerSecond * dt);
        }

        /// <summary>
        /// A random direction inside a cone around forward. angle01 and radius01 are random numbers in 0..1
        /// (passed in, so tests can choose them). √radius01 spreads the shots evenly over the disc instead of
        /// bunching them in the middle. right and up must be perpendicular to forward (the camera's axes).
        /// </summary>
        public static Vector3 SpreadDirection(Vector3 forward, Vector3 right, Vector3 up, float spreadDegrees,
            float angle01, float radius01)
        {
            if (spreadDegrees <= 0f)
            {
                return forward;
            }

            float radius = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad) * Mathf.Sqrt(Mathf.Clamp01(radius01));
            float angle = angle01 * 2f * Mathf.PI;
            Vector3 direction = forward + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius);
            return direction.normalized;
        }

        /// <summary>True if target is within maxDistance of origin and inside the cone around forward.</summary>
        public static bool InMeleeCone(Vector3 origin, Vector3 forward, Vector3 target, float maxDistance,
            float halfAngleDegrees)
        {
            Vector3 toTarget = target - origin;
            float sqrDistance = toTarget.sqrMagnitude;
            if (sqrDistance > maxDistance * maxDistance)
            {
                return false;
            }

            if (sqrDistance < 1e-6f)
            {
                return true; // inside the target: always a hit
            }

            return Vector3.Angle(forward, toTarget) <= halfAngleDegrees;
        }
    }
}
