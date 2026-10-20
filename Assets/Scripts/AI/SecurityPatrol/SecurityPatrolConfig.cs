// Owner: World Builder (Security Patrol AI).
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>
    /// Patrol tuning from docs/reference/agent-controller.md, sections 3, 5 and 6.1.
    /// Distances are metres, durations seconds and speeds metres per second.
    /// The brain requests Walk/Run; the separate mover and shooter execute the intent.
    /// </summary>
    [CreateAssetMenu(fileName = "SecurityPatrolConfig", menuName = "Blackout/Security Patrol Config")]
    public sealed class SecurityPatrolConfig : ScriptableObject
    {
        [Header("Perception (S2)")]
        [Min(0f)] public float eyeHeight = 1.62f;
        [Tooltip("Sight point above the player's feet; one ray to eye height, as in the existing S2 sensor.")]
        [Min(0f)] public float playerSightHeight = 1.62f;
        [Min(0f)] public float visionRange = 22f;

        [Tooltip("Full vision cone in degrees. Prototype: 1.95 radians (about 111.73 degrees).")]
        [Range(0f, 360f)] public float visionAngle = 1.95f * Mathf.Rad2Deg;

        [Tooltip("Unity listener distance cap. 65 m covers the largest reference noise radius (sniper). NoiseEvent.loudness remains the event's own radius in metres; falloff uses that radius, not this cap.")]
        [Min(0f)] public float hearingRange = 65f;

        [Tooltip("Hear when max(0, 1 - distance / noise radius) is strictly above this value. Prototype patrol hearing multiplier: 1.0; threshold: 0.04.")]
        [Range(0f, 1f)] public float hearingThreshold = 0.04f;

        [Header("Movement and states (S3)")]
        [Tooltip("FSM engagement distance, distinct from the weapon's 34 m maximum range.")]
        [Min(0f)] public float attackRange = 22f;

        [Min(0f)] public float walkSpeed = 1.55f;
        [Min(0f)] public float runSpeed = 4.1f;

        [Tooltip("Calm search duration. The reference uses 30 seconds while alert.")]
        [Min(0f)] public float searchTime = 14f;
        [Tooltip("Search duration during a cure alarm or Blackboard alert; generator-only heightening keeps the calm duration.")]
        [Min(0f)] public float alertSearchTime = 30f;

        [Header("Combat tuning (S3)")]
        [Tooltip("Player chest height above the supplied feet position. Reference: 1.8 m player height times 0.62.")]
        [Min(0f)] public float aimHeight = 1.8f * 0.62f;

        [Tooltip("Reference tuning for the separate AgentShooter; match this in the Agent Controller's difficulty stats. The brain does not add a second reaction timer.")]
        [Min(0f)] public float reactionDelay = 0.6f;

        [Tooltip("Base weapon spread in radians, before distance and movement modifiers. Prototype: 0.034 radians (about 1.95 degrees).")]
        [Min(0f)] public float aimError = 0.034f;
    }
}
