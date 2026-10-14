// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Every agent type's stats in one asset, plus four difficulty multipliers. At 1 the numbers are exactly the
    /// prototype's; raising or lowering a multiplier makes the whole game harder or easier without editing a
    /// prefab: health, damage, reaction delay (a smaller number reacts faster) and aim error (a smaller spread
    /// aims better). Vision, hearing and attack range belong to the AI brains, not here.
    /// </summary>
    [CreateAssetMenu(menuName = "Blackout/Agents/Difficulty Config", fileName = "DifficultyConfig")]
    public class DifficultyConfig : ScriptableObject
    {
        [SerializeField] private AgentStats[] stats = DefaultStats();

        [Header("Difficulty multipliers (1 = the prototype's numbers)")]
        [Min(0.1f)] public float healthMultiplier = 1f;
        [Min(0.1f)] public float damageMultiplier = 1f;
        [Tooltip("Above 1 the agents are slower to open fire; below 1 they react faster.")]
        [Min(0.1f)] public float reactionDelayMultiplier = 1f;
        [Tooltip("Above 1 the agents aim worse; below 1 they aim better.")]
        [Min(0.1f)] public float aimErrorMultiplier = 1f;

        /// <summary>The stats for a kind with the multipliers applied. A kind missing from the asset falls back to the prototype numbers.</summary>
        public AgentStats Get(AgentKind kind)
        {
            AgentStats found = Find(stats, kind) ?? Find(DefaultStats(), kind).Value;
            return Scaled(found, healthMultiplier, damageMultiplier, reactionDelayMultiplier, aimErrorMultiplier);
        }

        /// <summary>Applies the four multipliers to a copy of the stats. Everything else is untouched.</summary>
        public static AgentStats Scaled(AgentStats stats, float health, float damage, float reactionDelay, float aimError)
        {
            stats.maxHealth *= health;
            stats.damage *= damage;
            stats.reactionDelay *= reactionDelay;
            stats.spread *= aimError;
            return stats;
        }

        private static AgentStats? Find(AgentStats[] source, AgentKind kind)
        {
            if (source != null)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i].kind == kind)
                    {
                        return source[i];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// The prototype's numbers (docs/reference/agent-controller.md): HP, weapon rows from section 5, speeds and
        /// turn rates from section 3.1 (turn rates converted from rad/s to degrees/s). The gunfire noise is the
        /// prototype's default of 40 for every agent.
        /// </summary>
        public static AgentStats[] DefaultStats()
        {
            return new[]
            {
                new AgentStats
                {
                    kind = AgentKind.Hunter, maxHealth = 90f,
                    damage = 12f, range = 45f, burstCount = 1, burstInterval = 0.1f, burstPause = 1.35f,
                    spread = 0.024f, reactionDelay = 0.7f, noiseLoudness = 40f,
                    walkSpeed = 1.6f, runSpeed = 4.5f, turnRateDegrees = 6f * Mathf.Rad2Deg,
                },
                new AgentStats
                {
                    kind = AgentKind.SecurityPatrol, maxHealth = 80f,
                    damage = 7f, range = 34f, burstCount = 3, burstInterval = 0.12f, burstPause = 1.05f,
                    spread = 0.034f, reactionDelay = 0.6f, noiseLoudness = 40f,
                    walkSpeed = 1.55f, runSpeed = 4.1f, turnRateDegrees = 5f * Mathf.Rad2Deg,
                },
                new AgentStats
                {
                    kind = AgentKind.TacticalGuard, maxHealth = 100f,
                    damage = 8f, range = 34f, burstCount = 4, burstInterval = 0.1f, burstPause = 1.15f,
                    spread = 0.032f, reactionDelay = 0.5f, noiseLoudness = 40f,
                    walkSpeed = 1.4f, runSpeed = 3.5f, turnRateDegrees = 4f * Mathf.Rad2Deg,
                },
                new AgentStats
                {
                    kind = AgentKind.Commander, maxHealth = 70f,
                    damage = 5f, range = 22f, burstCount = 2, burstInterval = 0.25f, burstPause = 1.2f,
                    spread = 0.05f, reactionDelay = 0.8f, noiseLoudness = 40f,
                    walkSpeed = 1.3f, runSpeed = 3.4f, turnRateDegrees = 5f * Mathf.Rad2Deg,
                },
            };
        }
    }
}
