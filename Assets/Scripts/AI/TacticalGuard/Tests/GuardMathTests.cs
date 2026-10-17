// Owner: Core Developer (Tactical Guard AI).
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.TacticalGuard.Tests
{
    public sealed class GuardMathTests
    {
        private TacticalGuardConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<TacticalGuardConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [TestCase(0f, 1f)]
        [TestCase(5f, 0.5f)]
        [TestCase(10f, 0f)]
        [TestCase(30f, 0f)]
        [TestCase(-3f, 1f)]
        public void Freshness_FallsLinearlyOverTheWindow(float age, float expected)
        {
            Assert.That(GuardMath.Freshness(age, 10f), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Freshness_NeverSeenIsZero()
        {
            Assert.AreEqual(0f, GuardMath.Freshness(float.PositiveInfinity, 10f));
            Assert.AreEqual(0f, GuardMath.Freshness(float.NaN, 10f));
        }

        [Test]
        public void Normalise_ClampsAndSurvivesZeroRange()
        {
            Assert.AreEqual(0.5f, GuardMath.Normalise(6f, 12f));
            Assert.AreEqual(1f, GuardMath.Normalise(60f, 12f));
            Assert.AreEqual(0f, GuardMath.Normalise(-1f, 12f));
            Assert.AreEqual(1f, GuardMath.Normalise(3f, 0f));
        }

        [Test]
        public void Threat_AddsTheSourcesAndClamps()
        {
            Assert.AreEqual(0f, GuardMath.Threat(false, 0f, false, false, config));
            Assert.That(GuardMath.Threat(true, 0f, false, false, config), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(GuardMath.Threat(false, 1f, false, false, config), Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(GuardMath.Threat(false, 0.5f, false, true, config), Is.EqualTo(0.5f).Within(0.0001f)); // 0.35 + 0.15
            Assert.AreEqual(1f, GuardMath.Threat(true, 1f, true, true, config));
        }

        [Test]
        public void Threat_AimedAtOnlyCountsWhileVisible()
        {
            Assert.AreEqual(0f, GuardMath.Threat(false, 0f, true, false, config));
            Assert.That(GuardMath.Threat(true, 0f, true, false, config), Is.EqualTo(0.65f).Within(0.0001f));
        }

        [Test]
        public void FlatDistance_IgnoresHeight()
        {
            Assert.AreEqual(5f, GuardMath.FlatDistance(new Vector3(0f, 0f, 0f), new Vector3(3f, 40f, 4f)), 0.0001f);
        }

        [Test]
        public void RepositionTarget_InsideTheLimitIsTheLastKnownPosition()
        {
            Vector3 target = GuardMath.RepositionTarget(new Vector3(0f, 2.5f, 0f), new Vector3(6f, 9f, 8f), 15f);

            Assert.AreEqual(new Vector3(6f, 2.5f, 8f), target);
        }

        [Test]
        public void RepositionTarget_BeyondTheLimitIsPulledBackAlongTheSameLine()
        {
            Vector3 post = new Vector3(10f, 2.5f, 10f);
            Vector3 target = GuardMath.RepositionTarget(post, new Vector3(10f, 2.5f, 70f), 15f);

            Assert.That(target.x, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(target.z, Is.EqualTo(25f).Within(0.0001f));
        }

        [Test]
        public void RepositionTarget_LastKnownOnThePostDoesNotDivideByZero()
        {
            Vector3 post = new Vector3(1f, 2f, 3f);

            Assert.AreEqual(post, GuardMath.RepositionTarget(post, post, 15f));
        }

        [Test]
        public void ScanPoint_StartsAheadOfThePostAndTurnsWithTime()
        {
            Vector3 post = new Vector3(2f, 2.5f, 4f);
            Vector3 atStart = GuardMath.ScanPoint(post, 0f, config);
            Vector3 later = GuardMath.ScanPoint(post, Mathf.PI / config.scanSpeed / 2f, config); // a quarter turn

            Assert.That(atStart.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(atStart.y, Is.EqualTo(2.5f + config.scanHeight).Within(0.0001f));
            Assert.That(atStart.z, Is.EqualTo(4f + config.scanRadius).Within(0.0001f));
            Assert.That(later.x, Is.EqualTo(2f + config.scanRadius).Within(0.001f));
            Assert.That(later.z, Is.EqualTo(4f).Within(0.001f));
        }
    }
}
