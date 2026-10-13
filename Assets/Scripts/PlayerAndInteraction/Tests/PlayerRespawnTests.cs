using NUnit.Framework;
using UnityEngine;

namespace Blackout.Player.Tests
{
    public class PlayerRespawnTests
    {
        private const float Tolerance = 0.001f;

        // ---- death sequence -----------------------------------------------------------

        [Test]
        public void Death_New_IsAlive()
        {
            var death = new DeathSequence(2.2f);
            Assert.IsFalse(death.IsDead);
            Assert.AreEqual(0f, death.FallProgress);
            Assert.IsFalse(death.Tick(10f));
        }

        [Test]
        public void Death_RespawnIsDueOnceAfterTheDelay()
        {
            var death = new DeathSequence(2.2f);
            death.Begin();

            Assert.IsFalse(death.Tick(2.1f));
            Assert.IsTrue(death.IsDead);
            Assert.AreEqual(0.1f, death.TimeUntilRespawn, Tolerance);

            Assert.IsTrue(death.Tick(0.2f));
            Assert.IsFalse(death.IsDead);
            Assert.IsFalse(death.Tick(1f), "only once");
        }

        [Test]
        public void Death_FallProgress_ReachesOneAfterPoint8Seconds()
        {
            var death = new DeathSequence(2.2f);
            death.Begin();
            death.Tick(0.4f);
            Assert.AreEqual(0.5f, death.FallProgress, Tolerance);
            death.Tick(1f);
            Assert.AreEqual(1f, death.FallProgress, Tolerance);
        }

        // ---- fall damage --------------------------------------------------------------

        [Test]
        public void FallDamage_UpTo13MetresPerSecond_IsZero()
        {
            Assert.AreEqual(0f, PlayerHealthRules.FallDamage(13f, 13f, 8f));
            Assert.AreEqual(0f, PlayerHealthRules.FallDamage(6f, 13f, 8f));
        }

        [Test]
        public void FallDamage_Above13_Is8PerMetrePerSecond()
        {
            Assert.AreEqual(16f, PlayerHealthRules.FallDamage(15f, 13f, 8f), Tolerance);
        }

        // ---- checkpoints --------------------------------------------------------------

        [Test]
        public void Checkpoint_BeforeTheCure_OnlyNormalCheckpointsCount()
        {
            Assert.IsTrue(PlayerHealthRules.IsCheckpointEligible("Checkpoint_Forest", false));
            Assert.IsFalse(PlayerHealthRules.IsCheckpointEligible("Checkpoint_EscapeEntrance", false));
        }

        [Test]
        public void Checkpoint_AfterTheCure_OnlyEscapeCheckpointsCount()
        {
            Assert.IsTrue(PlayerHealthRules.IsCheckpointEligible("Checkpoint_EscapeForest", true));
            Assert.IsFalse(PlayerHealthRules.IsCheckpointEligible("Checkpoint_GeneratorRoom", true));
        }

        [Test]
        public void Checkpoint_InReach_UsesTheRadius()
        {
            Assert.IsTrue(PlayerHealthRules.InReach(new Vector3(0f, 1f, 5.9f), new Vector3(0f, 1f, 0f), 6f));
            Assert.IsFalse(PlayerHealthRules.InReach(new Vector3(0f, 1f, 6.1f), new Vector3(0f, 1f, 0f), 6f));
        }
    }
}
