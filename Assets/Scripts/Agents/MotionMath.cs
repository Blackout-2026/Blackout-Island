// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// The small formulas behind smooth movement, kept free of components so they can be tested directly.
    /// Velocities are flat: x is world X and y is world Z.
    /// </summary>
    public static class MotionMath
    {
        /// <summary>
        /// Moves the current velocity toward the desired one, at most <c>acceleration * dt</c> per step. Slowing down
        /// (desired speed below current speed) is <paramref name="decelerationMultiplier"/> times faster, so agents stop
        /// instead of sliding. Reaches the desired velocity exactly when it is within one step.
        /// </summary>
        public static Vector2 StepVelocity(Vector2 current, Vector2 desired, float acceleration, float decelerationMultiplier, float dt)
        {
            Vector2 delta = desired - current;
            float distance = delta.magnitude;
            float step = acceleration * dt * (desired.sqrMagnitude < current.sqrMagnitude ? decelerationMultiplier : 1f);
            if (distance <= step)
            {
                return desired;
            }
            return current + delta * (step / distance);
        }

        /// <summary>The yaw in degrees (0 = world +Z, 90 = world +X) of a flat direction.</summary>
        public static float YawOf(Vector2 direction)
        {
            return Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
        }

        /// <summary>Turns toward the target yaw by at most <c>turnRateDegrees * dt</c>, the short way round.</summary>
        public static float TurnToward(float currentYaw, float targetYaw, float turnRateDegrees, float dt)
        {
            return Mathf.MoveTowardsAngle(currentYaw, targetYaw, turnRateDegrees * dt);
        }
    }
}
