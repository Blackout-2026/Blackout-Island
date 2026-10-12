// Owner: Agent Controller.
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// How an agent moves. Defaults are the prototype's AgentController values (docs/reference/agent-controller.md
    /// section 3): walk 1.5 m/s, run 4.0 m/s, acceleration 7 m/s^2, turn rate 5 rad/s. Each agent type overrides
    /// the speeds on its own prefab (for example the Hunter walks at 1.6 and runs at 4.5).
    /// </summary>
    [Serializable]
    public class MovementSettings
    {
        [Header("Speed")]
        [Min(0.1f)] public float walkSpeed = 1.5f;
        [Min(0.1f)] public float runSpeed = 4.0f;
        [Tooltip("Metres per second squared. Stops are this much times Deceleration Multiplier, so agents do not slide.")]
        [Min(0.1f)] public float acceleration = 7f;
        [Min(1f)] public float decelerationMultiplier = 1.6f;

        [Header("Turning")]
        [Tooltip("Degrees per second. The prototype uses 5 rad/s = 286.5 degrees/s.")]
        [Min(1f)] public float turnRateDegrees = 286.5f;
        [Tooltip("Faster turning while aiming, so the agent can follow the player.")]
        [Min(1f)] public float aimTurnMultiplier = 1.4f;
        [Tooltip("Below this speed (m/s) the agent keeps its facing instead of turning toward its velocity.")]
        [Min(0f)] public float minTurnSpeed = 0.25f;

        [Header("Following a path")]
        [Tooltip("A corner counts as reached within this distance (metres).")]
        [Min(0.05f)] public float cornerReachDistance = 0.55f;
        [Tooltip("The destination counts as reached within this distance (metres).")]
        [Min(0.05f)] public float arriveDistance = 0.4f;
        [Tooltip("Within this distance of the destination the agent slows down.")]
        [Min(0.1f)] public float arrivalSlowDistance = 1.4f;
        [Tooltip("Slowest fraction of the speed while arriving (0.15 keeps it creeping the last metre).")]
        [Range(0.01f, 1f)] public float arrivalMinSpeedFactor = 0.15f;

        [Header("Replanning")]
        [Tooltip("A new destination further than this from the planned one forces a new path while walking.")]
        [Min(0.05f)] public float replanDistanceWalk = 0.9f;
        [Tooltip("The same while running, which tolerates more drift.")]
        [Min(0.05f)] public float replanDistanceRun = 1.6f;
        [Tooltip("Seconds between routine re-plans (2.5 s plus up to 1 s, random per agent), which also catches a moved barricade.")]
        [Min(0.1f)] public float repathInterval = 2.5f;
        [Min(0f)] public float repathJitter = 1f;
        [Tooltip("Seconds between checks that the way to the next corner is still open.")]
        [Min(0.05f)] public float blockedCheckInterval = 0.25f;
        [Tooltip("Never plan twice within this many seconds, so a moving target does not cost a path every frame.")]
        [Min(0f)] public float minReplanInterval = 0.2f;

        [Header("Ground")]
        [Tooltip("Smoothing of the height following the NavMesh (stairs, ramp). The prototype uses 18.")]
        [Min(0.1f)] public float groundDamping = 18f;
        [Tooltip("How far the agent may be from the NavMesh before it counts as off it (metres).")]
        [Min(0.1f)] public float snapDistance = 1f;

        [Header("Stuck handling (A4)")]
        [Tooltip("Below this desired speed (m/s) the agent is not 'trying to move', so it can never be stuck. The prototype uses 0.4.")]
        [Min(0f)] public float stuckTryingSpeedThreshold = 0.4f;
        [Tooltip("Seconds between stuck checks. The prototype uses 1.4.")]
        [Min(0.1f)] public float stuckCheckInterval = 1.4f;
        [Tooltip("Below this much progress (metres) since the last check, the agent counts as stuck. The prototype uses 0.35.")]
        [Min(0f)] public float stuckMinProgress = 0.35f;
        [Tooltip("The random nudge applied on each axis when stuck (m/s). The prototype uses 1.25.")]
        [Min(0f)] public float stuckNudgeSpeed = 1.25f;

        /// <summary>The top speed for a speed class. Stop is zero.</summary>
        public float SpeedFor(MoveSpeed speed)
        {
            switch (speed)
            {
                case MoveSpeed.Walk:
                    return walkSpeed;
                case MoveSpeed.Run:
                    return runSpeed;
                default:
                    return 0f;
            }
        }

        /// <summary>How far a new destination may drift before the path is rebuilt.</summary>
        public float ReplanDistanceFor(MoveSpeed speed)
        {
            return speed == MoveSpeed.Run ? replanDistanceRun : replanDistanceWalk;
        }
    }
}
