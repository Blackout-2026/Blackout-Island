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
        [Min(0f)] public float visionRange = 22f;

        [Tooltip("Full vision cone in degrees. Prototype: 1.95 radians (about 111.73 degrees).")]
        [Range(0f, 360f)] public float visionAngle = 1.95f * Mathf.Rad2Deg;

        [Tooltip("Provisional Unity hearing cap, not a prototype patrol stat. 65 m is the largest listed noise radius (sniper); prototype hearing uses each event's radius and a 1.0 multiplier. Revisit in S2.")]
        [Min(0f)] public float hearingRange = 65f;

        [Header("Movement and states (S3)")]
        [Tooltip("FSM engagement distance, distinct from the weapon's 34 m maximum range.")]
        [Min(0f)] public float attackRange = 22f;

        [Min(0f)] public float walkSpeed = 1.55f;
        [Min(0f)] public float runSpeed = 4.1f;

        [Tooltip("Calm search duration. The reference uses 30 seconds while alert.")]
        [Min(0f)] public float searchTime = 14f;

        [Header("Combat tuning (S3)")]
        [Min(0f)] public float reactionDelay = 0.6f;

        [Tooltip("Base weapon spread in radians, before distance and movement modifiers. Prototype: 0.034 radians (about 1.95 degrees).")]
        [Min(0f)] public float aimError = 0.034f;
    }
}
