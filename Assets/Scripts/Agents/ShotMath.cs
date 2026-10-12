// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// The pure formulas behind <see cref="AgentShooter"/> (A4), kept free of components so they are directly
    /// testable (same pattern as MotionMath and RobotGaitMath). Source: docs/reference/agent-controller.md
    /// section 5.1.
    /// </summary>
    public static class ShotMath
    {
        /// <summary>
        /// The full prototype formula is
        /// <c>spread * (1 + dist/28) * (1 + playerHorizSpeed/4.5 * 0.9) * (engaged &lt; 1.2s ? 1.6 : 1) * (crouched ? 1.15 : 1)</c>,
        /// times 0.9 while heightened. AgentShooter has no access to the player's own speed or crouch state
        /// without coupling to the player (which the architecture deliberately avoids — the shooter only ever
        /// sees an AgentIntent), so those two multiplicative terms are omitted here; everything else is exact.
        /// Documented as a known simplification in docs/viva.
        /// </summary>
        public static float AimError(float spread, float distance, float engagedSeconds, bool heightened)
        {
            float error = spread * (1f + distance / 28f) * (engagedSeconds < 1.2f ? 1.6f : 1f);
            return heightened ? error * 0.9f : error;
        }

        /// <summary>
        /// A direction perturbed by a Rayleigh-style random angle: the perturbation angle is
        /// <c>errorRadians * sqrt(-2 * ln(u01)) * 0.7</c>, spread uniformly around <paramref name="forward"/>
        /// by <paramref name="angle01"/>. u01 and angle01 are passed in (not sampled internally) so tests are
        /// deterministic. right and up must be perpendicular to forward.
        /// </summary>
        public static Vector3 PerturbedDirection(Vector3 forward, Vector3 right, Vector3 up, float errorRadians, float u01, float angle01)
        {
            if (errorRadians <= 0f)
            {
                return forward;
            }

            float perturbRadians = errorRadians * Mathf.Sqrt(-2f * Mathf.Log(Mathf.Clamp(u01, 1e-6f, 1f))) * 0.7f;
            float discRadius = Mathf.Tan(perturbRadians);
            float angle = angle01 * 2f * Mathf.PI;
            Vector3 offset = right * (Mathf.Cos(angle) * discRadius) + up * (Mathf.Sin(angle) * discRadius);
            return (forward + offset).normalized;
        }

        /// <summary>
        /// Seconds until the next shot: <paramref name="interval"/> mid-burst, or a pause (0.8 to 1.2 times
        /// <paramref name="pause"/>, chosen by <paramref name="pauseRandom01"/>) once the burst is complete.
        /// </summary>
        public static float NextShotDelay(int shotsFiredInBurst, int burstCount, float interval, float pause, float pauseRandom01)
        {
            if (shotsFiredInBurst < burstCount)
            {
                return interval;
            }

            return pause * Mathf.Lerp(0.8f, 1.2f, Mathf.Clamp01(pauseRandom01));
        }
    }
}
