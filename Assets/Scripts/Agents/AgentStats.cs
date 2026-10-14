// Owner: Agent Controller.
using System;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>The four agent types. Each has its own decision architecture and its own row of <see cref="AgentStats"/>.</summary>
    public enum AgentKind
    {
        Hunter,
        SecurityPatrol,
        TacticalGuard,
        Commander
    }

    /// <summary>
    /// One agent type's numbers: hit points, weapon and movement. The values in <see cref="DifficultyConfig"/> are
    /// the prototype's (docs/reference/agent-controller.md sections 3.1 and 5). A struct, so the multipliers can
    /// produce a scaled copy without touching the asset.
    /// </summary>
    [Serializable]
    public struct AgentStats
    {
        public AgentKind kind;

        [Header("Health")]
        [Min(1f)] public float maxHealth;

        [Header("Weapon")]
        [Min(0f)] public float damage;
        [Min(0f)] public float range;
        [Min(1)] public int burstCount;
        [Min(0.01f)] public float burstInterval;
        [Min(0f)] public float burstPause;
        [Tooltip("Radians: the base aim error before distance and engagement scaling.")]
        [Min(0f)] public float spread;
        [Tooltip("Seconds between first wanting to fire and the first shot.")]
        [Min(0f)] public float reactionDelay;
        [Min(0f)] public float noiseLoudness;

        [Header("Movement")]
        [Min(0.1f)] public float walkSpeed;
        [Min(0.1f)] public float runSpeed;
        [Tooltip("Degrees per second (the prototype's rad/s times 57.3).")]
        [Min(1f)] public float turnRateDegrees;
    }
}
