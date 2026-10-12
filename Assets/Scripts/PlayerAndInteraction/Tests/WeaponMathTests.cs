using NUnit.Framework;
using UnityEngine;

namespace Blackout.Weapons.Tests
{
    public class WeaponMathTests
    {
        private const float Tolerance = 0.001f;

        private WeaponData smg;

        [SetUp]
        public void SetUp()
        {
            // Defaults are the SMG's numbers: falloff 10 -> 32 m down to x0.45, spread 1.38 deg.
            smg = ScriptableObject.CreateInstance<WeaponData>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(smg);
        }

        // ---- damage falloff -----------------------------------------------------------

        [Test]
        public void Falloff_BeforeStart_IsFullDamage()
        {
            Assert.AreEqual(1f, WeaponMath.FalloffMultiplier(0f, smg), Tolerance);
            Assert.AreEqual(1f, WeaponMath.FalloffMultiplier(10f, smg), Tolerance);
        }

        [Test]
        public void Falloff_HalfwayBetweenStartAndEnd_IsHalfwayToMinimum()
        {
            // smoothstep(0.5) = 0.5, so lerp(1, 0.45, 0.5) = 0.725 at 21 m.
            Assert.AreEqual(0.725f, WeaponMath.FalloffMultiplier(21f, smg), Tolerance);
        }

        [Test]
        public void Falloff_AtAndBeyondEnd_IsMinimum()
        {
            Assert.AreEqual(0.45f, WeaponMath.FalloffMultiplier(32f, smg), Tolerance);
            Assert.AreEqual(0.45f, WeaponMath.FalloffMultiplier(90f, smg), Tolerance);
        }

        [Test]
        public void Falloff_DecreasesWithDistance()
        {
            float previous = 1f;
            for (float distance = 10f; distance <= 32f; distance += 2f)
            {
                float multiplier = WeaponMath.FalloffMultiplier(distance, smg);
                Assert.LessOrEqual(multiplier, previous + Tolerance);
                previous = multiplier;
            }
        }

        [Test]
        public void Falloff_SniperNumbers_NeverReduceDamage()
        {
            Assert.AreEqual(1f, WeaponMath.FalloffMultiplier(320f, 999f, 1000f, 1f), Tolerance);
        }

        // ---- spread and bloom ---------------------------------------------------------

        [Test]
        public void Spread_StandingStillOnTheGround_IsBaseSpread()
        {
            Assert.AreEqual(1.38f, WeaponMath.Spread(smg, 0f, 0f, true), Tolerance);
        }

        [Test]
        public void Spread_MovingAndAirborne_AddsBoth()
        {
            float expected = 1.38f + 1.15f + WeaponMath.AirborneSpread;
            Assert.AreEqual(expected, WeaponMath.Spread(smg, 2f, 0f, false), Tolerance); // move fraction clamped to 1
        }

        [Test]
        public void Bloom_IsCappedAtMaximum_AndRecoversToZero()
        {
            float bloom = 0f;
            for (int i = 0; i < 20; i++)
            {
                bloom = WeaponMath.AddBloom(bloom, smg);
            }

            Assert.AreEqual(smg.bloomMax, bloom, Tolerance);
            Assert.AreEqual(0f, WeaponMath.RecoverBloom(bloom, 1f), Tolerance);
        }

        [Test]
        public void SpreadDirection_ZeroSpread_IsForward()
        {
            Vector3 direction = WeaponMath.SpreadDirection(Vector3.forward, Vector3.right, Vector3.up, 0f, 0.3f, 1f);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, direction), Tolerance);
        }

        [Test]
        public void SpreadDirection_NeverLeavesTheCone()
        {
            for (int i = 0; i <= 10; i++)
            {
                float u = i / 10f;
                Vector3 direction = WeaponMath.SpreadDirection(Vector3.forward, Vector3.right, Vector3.up, 2f, u, u);
                Assert.LessOrEqual(Vector3.Angle(Vector3.forward, direction), 2f + Tolerance);
            }

            // radius01 = 1 lands exactly on the edge of the cone.
            Vector3 edge = WeaponMath.SpreadDirection(Vector3.forward, Vector3.right, Vector3.up, 2f, 0.25f, 1f);
            Assert.AreEqual(2f, Vector3.Angle(Vector3.forward, edge), 0.01f);
        }

        // ---- melee cone ---------------------------------------------------------------

        [Test]
        public void MeleeCone_TargetStraightAheadInReach_IsHit()
        {
            Assert.IsTrue(WeaponMath.InMeleeCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2f), 2.7f, 51.7f));
        }

        [Test]
        public void MeleeCone_TargetBeyondReach_IsMissed()
        {
            Assert.IsFalse(WeaponMath.InMeleeCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2.8f), 2.7f, 51.7f));
        }

        [Test]
        public void MeleeCone_TargetBehind_IsMissed()
        {
            Assert.IsFalse(WeaponMath.InMeleeCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1f), 2.7f, 51.7f));
        }

        [Test]
        public void MeleeCone_EdgeOfTheCone_HitsInsideAndMissesOutside()
        {
            Vector3 inside = Quaternion.Euler(0f, 50f, 0f) * Vector3.forward * 2f;
            Vector3 outside = Quaternion.Euler(0f, 54f, 0f) * Vector3.forward * 2f;
            Assert.IsTrue(WeaponMath.InMeleeCone(Vector3.zero, Vector3.forward, inside, 2.7f, 51.7f));
            Assert.IsFalse(WeaponMath.InMeleeCone(Vector3.zero, Vector3.forward, outside, 2.7f, 51.7f));
        }
    }
}
