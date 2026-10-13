using NUnit.Framework;
using UnityEngine;

namespace Blackout.Hud.Tests
{
    public class HudMathTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void Ghost_TrailsDownTowardsHealth()
        {
            float ghost = HudMath.Ghost(100f, 60f, 0.1f);
            Assert.Less(ghost, 100f);
            Assert.Greater(ghost, 60f);
        }

        [Test]
        public void Ghost_NeverBelowHealth()
        {
            Assert.AreEqual(80f, HudMath.Ghost(50f, 80f, 0.1f), Tolerance); // healed: ghost jumps up
            Assert.AreEqual(60f, HudMath.Ghost(100f, 60f, 100f), Tolerance);
        }

        [Test]
        public void DamageFlash_25DamageIsFull_AndFadesOut()
        {
            Assert.AreEqual(0.6f, HudMath.AddDamageFlash(0f, 15f), Tolerance);
            Assert.AreEqual(1f, HudMath.AddDamageFlash(0.6f, 25f), Tolerance);
            Assert.AreEqual(0.3f, HudMath.DecayDamageFlash(1f, 0.5f), Tolerance);
            Assert.AreEqual(0f, HudMath.DecayDamageFlash(0.1f, 1f), Tolerance);
        }

        [Test]
        public void LowHealthPulse_OffAbove40_FullWhenDead()
        {
            Assert.AreEqual(0f, HudMath.LowHealthPulse(40f, true, 0.3f), Tolerance);
            Assert.AreEqual(1f, HudMath.LowHealthPulse(100f, false, 0.3f), Tolerance);
            Assert.AreEqual(0.75f, HudMath.LowHealthPulse(0f, true, 0f), Tolerance); // sin(0) = 0
        }

        [Test]
        public void Vignette_IsCappedAt90Percent()
        {
            Assert.AreEqual(0.9f, HudMath.VignetteAlpha(1f, 1f), Tolerance);
            Assert.AreEqual(0.5f, HudMath.VignetteAlpha(0f, 1f), Tolerance);
        }

        [Test]
        public void DamageAngle_RightIsPositive_LeftIsNegative_BehindIs180()
        {
            Assert.AreEqual(90f, HudMath.DamageAngle(Vector3.forward, Vector3.right), Tolerance);
            Assert.AreEqual(-90f, HudMath.DamageAngle(Vector3.forward, Vector3.left), Tolerance);
            Assert.AreEqual(180f, Mathf.Abs(HudMath.DamageAngle(Vector3.forward, Vector3.back)), Tolerance);
        }

        [Test]
        public void DamageAngle_IgnoresHeight()
        {
            Assert.AreEqual(0f, HudMath.DamageAngle(Vector3.forward, new Vector3(0f, 5f, 1f)), Tolerance);
        }
    }
}
