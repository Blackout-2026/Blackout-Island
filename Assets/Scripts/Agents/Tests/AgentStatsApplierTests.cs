using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class AgentStatsApplierTests
    {
        private GameObject agent;

        [TearDown]
        public void TearDown()
        {
            if (agent != null)
            {
                Object.DestroyImmediate(agent);
            }
        }

        private static AgentStats Hunter()
        {
            return DifficultyConfig.DefaultStats()[(int)AgentKind.Hunter];
        }

        private GameObject MakeFullAgent()
        {
            agent = new GameObject("Hunter-1");
            agent.AddComponent<Health>().ResetHealth(0f); // Awake does not run in EditMode
            agent.AddComponent<AgentShooter>();
            agent.AddComponent<PathFollower>();
            return agent;
        }

        [Test]
        public void TheHealthFieldTheApplierWritesStillExists()
        {
            // If the Systems Engineer renames Health's private field, this fails here instead of silently
            // leaving every agent on the prefab's health at runtime.
            Assert.IsTrue(AgentStatsApplier.CanSetMaxHealth);
        }

        [Test]
        public void Apply_SetsTheMaximumHealth_AndStartsAtFullHealth()
        {
            GameObject go = MakeFullAgent();
            var health = go.GetComponent<Health>();
            Assert.AreEqual(100f, health.Max, "Health's own default");

            AgentStatsApplier.Apply(go, Hunter()); // 90 HP

            Assert.AreEqual(90f, health.Max);
            Assert.AreEqual(90f, health.Current);
            Assert.IsTrue(health.IsAlive);
        }

        [Test]
        public void Apply_SetsTheWeaponNumbers()
        {
            GameObject go = MakeFullAgent();

            AgentStatsApplier.Apply(go, Hunter());

            AgentWeaponConfig weapon = go.GetComponent<AgentShooter>().Config;
            Assert.AreEqual(12f, weapon.damage);
            Assert.AreEqual(45f, weapon.range);
            Assert.AreEqual(1, weapon.burstCount);
            Assert.AreEqual(1.35f, weapon.burstPause, 0.0001f);
            Assert.AreEqual(0.024f, weapon.spread, 0.0001f);
            Assert.AreEqual(0.7f, weapon.reactionDelay, 0.0001f);
            Assert.AreEqual(40f, weapon.noiseLoudness);
        }

        [Test]
        public void Apply_SetsTheMovementNumbers()
        {
            GameObject go = MakeFullAgent();

            AgentStatsApplier.Apply(go, Hunter());

            MovementSettings movement = go.GetComponent<PathFollower>().Settings;
            Assert.AreEqual(1.6f, movement.walkSpeed);
            Assert.AreEqual(4.5f, movement.runSpeed);
            Assert.AreEqual(6f * Mathf.Rad2Deg, movement.turnRateDegrees, 0.01f);
        }

        [Test]
        public void Apply_LeavesTheOtherSettingsAlone()
        {
            GameObject go = MakeFullAgent();
            var follower = go.GetComponent<PathFollower>();
            follower.Settings.acceleration = 9f; // a hand-tuned value on the prefab

            AgentStatsApplier.Apply(go, Hunter());

            Assert.AreEqual(9f, follower.Settings.acceleration);
        }

        [Test]
        public void Apply_Twice_GivesTheSameResult()
        {
            GameObject go = MakeFullAgent();

            AgentStatsApplier.Apply(go, Hunter());
            AgentStatsApplier.Apply(go, Hunter());

            Assert.AreEqual(90f, go.GetComponent<Health>().Max);
            Assert.AreEqual(12f, go.GetComponent<AgentShooter>().Config.damage);
        }

        [Test]
        public void Apply_WithMissingComponents_DoesNotThrow()
        {
            agent = new GameObject("Bare");

            Assert.DoesNotThrow(() => AgentStatsApplier.Apply(agent, Hunter()));
        }

        [Test]
        public void Apply_FindsTheShooterOnAChildToo()
        {
            agent = new GameObject("Agent");
            agent.AddComponent<Health>().ResetHealth(0f);
            var child = new GameObject("Weapon");
            child.transform.SetParent(agent.transform, false);
            AgentShooter shooter = child.AddComponent<AgentShooter>();

            AgentStatsApplier.Apply(agent, Hunter());

            Assert.AreEqual(12f, shooter.Config.damage);
        }
    }
}
