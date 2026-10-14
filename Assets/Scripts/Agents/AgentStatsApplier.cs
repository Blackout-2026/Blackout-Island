// Owner: Agent Controller.
using System.Reflection;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Copies one agent type's <see cref="AgentStats"/> onto a spawned agent: maximum health onto Health, the weapon
    /// numbers onto AgentShooter's config, and the speeds onto PathFollower's settings. Any of the three components
    /// may be missing (a prefab under construction), and is then skipped.
    /// </summary>
    public static class AgentStatsApplier
    {
        // Health (Systems Engineer's shared contract) exposes Max but no way to change it, and the spawner has to set a
        // different maximum per agent type and difficulty. Until Health gets a public setter, the one private field is
        // written here and nowhere else; a test fails if the field is renamed. Swap this for the setter once it exists.
        private const string MaxHealthFieldName = "maxHealth";
        private static readonly FieldInfo MaxHealthField =
            typeof(Health).GetField(MaxHealthFieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>True when the private field Health stores its maximum in could be found.</summary>
        public static bool CanSetMaxHealth => MaxHealthField != null;

        public static void Apply(GameObject agent, in AgentStats stats)
        {
            ApplyHealth(agent.GetComponent<Health>(), stats.maxHealth);
            ApplyWeapon(agent.GetComponentInChildren<AgentShooter>(true), stats);
            ApplyMovement(agent.GetComponent<PathFollower>(), stats);
        }

        private static void ApplyHealth(Health health, float maxHealth)
        {
            if (health == null)
            {
                return;
            }

            if (MaxHealthField == null)
            {
                Debug.LogError($"AgentStatsApplier: Health has no field '{MaxHealthFieldName}' any more, so {health.name} keeps its prefab health. Ask the Systems Engineer for a public setter.");
                return;
            }

            MaxHealthField.SetValue(health, maxHealth);
            health.ResetHealth(0f); // a fresh agent starts at full health, whatever the new maximum is
        }

        private static void ApplyWeapon(AgentShooter shooter, in AgentStats stats)
        {
            if (shooter == null)
            {
                return;
            }

            AgentWeaponConfig weapon = shooter.Config;
            weapon.damage = stats.damage;
            weapon.range = stats.range;
            weapon.burstCount = stats.burstCount;
            weapon.burstInterval = stats.burstInterval;
            weapon.burstPause = stats.burstPause;
            weapon.spread = stats.spread;
            weapon.reactionDelay = stats.reactionDelay;
            weapon.noiseLoudness = stats.noiseLoudness;
        }

        private static void ApplyMovement(PathFollower follower, in AgentStats stats)
        {
            if (follower == null)
            {
                return;
            }

            MovementSettings movement = follower.Settings;
            movement.walkSpeed = stats.walkSpeed;
            movement.runSpeed = stats.runSpeed;
            movement.turnRateDegrees = stats.turnRateDegrees;
        }
    }
}
