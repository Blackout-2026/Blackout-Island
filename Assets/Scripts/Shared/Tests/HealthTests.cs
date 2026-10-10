using NUnit.Framework;
using UnityEngine;

namespace Blackout.Shared.Tests
{
    public class HealthTests
    {
        private GameObject gameObject;
        private Health health;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("HealthTest");
            health = gameObject.AddComponent<Health>();
            // Awake does not run in EditMode, so start from full health the way Awake would.
            health.ResetHealth(0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        private static DamageInfo Hit(float amount)
        {
            return new DamageInfo(amount, Vector3.zero, Vector3.forward, null, DamageType.Bullet);
        }

        [Test]
        public void ResetHealth_StartsAtMax()
        {
            Assert.AreEqual(100f, health.Current);
            Assert.IsTrue(health.IsAlive);
        }

        [Test]
        public void TakeDamage_ReducesHealthAndRaisesOnDamaged()
        {
            float received = 0f;
            health.OnDamaged += info => received = info.amount;

            health.TakeDamage(Hit(30f));

            Assert.AreEqual(70f, health.Current);
            Assert.AreEqual(30f, received);
        }

        [Test]
        public void TakeDamage_IgnoresZeroAndNegativeAmounts()
        {
            int damagedCount = 0;
            health.OnDamaged += _ => damagedCount++;

            health.TakeDamage(Hit(0f));
            health.TakeDamage(Hit(-10f));

            Assert.AreEqual(100f, health.Current);
            Assert.AreEqual(0, damagedCount);
        }

        [Test]
        public void TakeDamage_Overkill_ClampsToZeroAndDiesOnce()
        {
            int diedCount = 0;
            health.OnDied += () => diedCount++;

            health.TakeDamage(Hit(150f));
            health.TakeDamage(Hit(10f));

            Assert.AreEqual(0f, health.Current);
            Assert.IsFalse(health.IsAlive);
            Assert.AreEqual(1, diedCount);
        }

        [Test]
        public void TakeDamage_WhenDead_DoesNotRaiseOnDamaged()
        {
            health.TakeDamage(Hit(100f));
            int damagedCount = 0;
            health.OnDamaged += _ => damagedCount++;

            health.TakeDamage(Hit(10f));

            Assert.AreEqual(0, damagedCount);
        }

        [Test]
        public void Invulnerable_BlocksDamageUntilTimerRunsOut()
        {
            health.ResetHealth(1.5f); // respawn invulnerability from the prototype

            health.TakeDamage(Hit(40f));
            Assert.AreEqual(100f, health.Current, "blocked while invulnerable");

            health.Tick(1.0f);
            health.TakeDamage(Hit(40f));
            Assert.AreEqual(100f, health.Current, "still invulnerable after 1.0 s");

            health.Tick(0.6f);
            Assert.IsFalse(health.IsInvulnerable);
            health.TakeDamage(Hit(40f));
            Assert.AreEqual(60f, health.Current, "damage applies after 1.6 s");
        }

        [Test]
        public void SetInvulnerable_NeverShortensALongerTimer()
        {
            health.SetInvulnerable(2f);
            health.SetInvulnerable(0.5f);

            health.Tick(1f);

            Assert.IsTrue(health.IsInvulnerable);
        }

        [Test]
        public void Heal_ClampsToMaxAndReturnsAmountGained()
        {
            health.TakeDamage(Hit(30f));

            float gained = health.Heal(50f);

            Assert.AreEqual(30f, gained);
            Assert.AreEqual(100f, health.Current);
        }

        [Test]
        public void Heal_WhenDead_DoesNothing()
        {
            health.TakeDamage(Hit(100f));

            float gained = health.Heal(50f);

            Assert.AreEqual(0f, gained);
            Assert.IsFalse(health.IsAlive);
        }

        [Test]
        public void ResetHealth_RevivesAtFullHealth()
        {
            health.TakeDamage(Hit(100f));

            health.ResetHealth(1.5f);

            Assert.IsTrue(health.IsAlive);
            Assert.AreEqual(100f, health.Current);
            Assert.IsTrue(health.IsInvulnerable);
        }
    }
}
