using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class ShotMathTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void AimError_AtZeroDistance_JustEarlyEngagementScaling()
        {
            // engaged < 1.2 s: x1.6. distance 0: (1 + 0/28) = 1.
            float error = ShotMath.AimError(spread: 0.035f, distance: 0f, engagedSeconds: 0f, heightened: false);

            Assert.AreEqual(0.035f * 1.6f, error, Tolerance);
        }

        [Test]
        public void AimError_GrowsWithDistance()
        {
            float near = ShotMath.AimError(0.035f, distance: 0f, engagedSeconds: 5f, heightened: false);
            float far = ShotMath.AimError(0.035f, distance: 28f, engagedSeconds: 5f, heightened: false);

            Assert.AreEqual(0.035f, near, Tolerance);
            Assert.AreEqual(0.035f * 2f, far, Tolerance, "distance 28 doubles the error (1 + 28/28)");
        }

        [Test]
        public void AimError_SettlesLower_OnceEngagedPastOnePointTwoSeconds()
        {
            float justEngaged = ShotMath.AimError(0.035f, 0f, engagedSeconds: 1.19f, heightened: false);
            float settled = ShotMath.AimError(0.035f, 0f, engagedSeconds: 1.2f, heightened: false);

            Assert.AreEqual(0.035f * 1.6f, justEngaged, Tolerance);
            Assert.AreEqual(0.035f, settled, Tolerance);
        }

        [Test]
        public void AimError_Heightened_IsTenPercentTighter()
        {
            float normal = ShotMath.AimError(0.035f, 0f, 5f, heightened: false);
            float heightened = ShotMath.AimError(0.035f, 0f, 5f, heightened: true);

            Assert.AreEqual(normal * 0.9f, heightened, Tolerance);
        }

        [Test]
        public void PerturbedDirection_WithZeroError_ReturnsForwardExactly()
        {
            Vector3 direction = ShotMath.PerturbedDirection(Vector3.forward, Vector3.right, Vector3.up, 0f, 0.5f, 0.5f);

            Assert.AreEqual(Vector3.forward, direction);
        }

        [Test]
        public void PerturbedDirection_AtTheTopOfTheRandomRange_ReturnsForwardExactly()
        {
            // ln(1) = 0, so the Rayleigh radius is exactly 0 regardless of the error.
            Vector3 direction = ShotMath.PerturbedDirection(Vector3.forward, Vector3.right, Vector3.up, 0.1f, 1f, 0.25f);

            Assert.AreEqual(Vector3.forward, direction, "u01 = 1 gives zero perturbation");
        }

        [Test]
        public void PerturbedDirection_StaysNormalized()
        {
            Vector3 direction = ShotMath.PerturbedDirection(Vector3.forward, Vector3.right, Vector3.up, 0.1f, 0.3f, 0.7f);

            Assert.AreEqual(1f, direction.magnitude, 0.0001f);
        }

        [Test]
        public void PerturbedDirection_AngleZero_OffsetsPurelyAlongRight()
        {
            Vector3 direction = ShotMath.PerturbedDirection(Vector3.forward, Vector3.right, Vector3.up, 0.2f, 0.2f, 0f);

            Assert.Greater(direction.x, 0f, "angle01 = 0 means cos(0) = 1: the offset is along +right");
            Assert.AreEqual(0f, direction.y, 0.0001f);
        }

        [Test]
        public void NextShotDelay_MidBurst_IsTheInterval()
        {
            float delay = ShotMath.NextShotDelay(shotsFiredInBurst: 1, burstCount: 3, interval: 0.11f, pause: 0.9f, pauseRandom01: 0.5f);

            Assert.AreEqual(0.11f, delay, Tolerance);
        }

        [Test]
        public void NextShotDelay_BurstComplete_IsARandomisedPause()
        {
            float min = ShotMath.NextShotDelay(3, 3, 0.11f, 0.9f, pauseRandom01: 0f);
            float max = ShotMath.NextShotDelay(3, 3, 0.11f, 0.9f, pauseRandom01: 1f);
            float mid = ShotMath.NextShotDelay(3, 3, 0.11f, 0.9f, pauseRandom01: 0.5f);

            Assert.AreEqual(0.9f * 0.8f, min, Tolerance);
            Assert.AreEqual(0.9f * 1.2f, max, Tolerance);
            Assert.AreEqual(0.9f, mid, Tolerance);
        }

        [Test]
        public void DefaultConfig_MatchesThePrototypesFallbackValues()
        {
            var config = new AgentWeaponConfig();

            Assert.AreEqual(7f, config.damage);
            Assert.AreEqual(3, config.burstCount);
            Assert.AreEqual(0.11f, config.burstInterval, Tolerance);
            Assert.AreEqual(0.9f, config.burstPause, Tolerance);
            Assert.AreEqual(0.035f, config.spread, Tolerance);
            Assert.AreEqual(30f, config.range);
            Assert.AreEqual(0.55f, config.reactionDelay, Tolerance);
            Assert.AreEqual(40f, config.noiseLoudness);
        }
    }
}
