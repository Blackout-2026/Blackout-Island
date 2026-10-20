// Owner: World Builder (Security Patrol AI).
using System;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.SecurityPatrol.Tests
{
    public sealed class PerceptionHearingTests
    {
        private sealed class NoSightQuery : IPerceptionRaycast
        {
            public bool IsBlocked(Vector3 origin, Vector3 target) => false;
        }

        private Perception perception;
        private NoSightQuery query;

        [SetUp]
        public void SetUp()
        {
            query = new NoSightQuery();
            perception = new Perception(22f, 120f, query);
        }

        [TestCase(9.5f, true)]
        [TestCase(9.7f, false)]
        [TestCase(10f, false)]
        [TestCase(11f, false)]
        public void NoiseRadiusAndDistance_DetermineAudibility(float distance, bool audible)
        {
            Vector3 source = Vector3.forward * distance;

            bool remembered = perception.HearNoise(Vector3.zero, Noise(source, 10f));

            Assert.That(remembered, Is.EqualTo(audible));
            Assert.That(perception.HeardNoise, Is.EqualTo(audible));
            Assert.That(perception.HasLastKnownPosition, Is.EqualTo(audible));
            if (audible)
            {
                Assert.That(perception.NoiseLoudness, Is.EqualTo(1f - distance / 10f).Within(0.00001f));
                Assert.That(perception.LastKnownPosition, Is.EqualTo(source));
            }
        }

        [Test]
        public void ExactlyAtThreshold_IsNotHeard()
        {
            perception = new Perception(22f, 120f, query, hearingThreshold: 0.5f);

            Assert.That(perception.HearNoise(Vector3.zero, Noise(Vector3.forward * 5f, 10f)), Is.False);
            Assert.That(perception.HeardNoise, Is.False);
        }

        [Test]
        public void LouderEvent_ReachesTheSameListenerWhenAQuieterOneCannot()
        {
            Vector3 listener = new Vector3(10f, 2.5f, 20f);
            Vector3 source = listener + Vector3.right * 8f;

            Assert.That(perception.HearNoise(listener, Noise(source, 4.5f)), Is.False);
            Assert.That(perception.HearNoise(listener, Noise(source, 13f)), Is.True);
            Assert.That(perception.NoisePosition, Is.EqualTo(source));
        }

        [Test]
        public void ListenerCapAndDisabledHearing_OverrideALargeNoiseRadius()
        {
            Vector3 source = Vector3.forward * 66f;
            Assert.That(perception.HearNoise(Vector3.zero, Noise(source, 1000f)), Is.False);
            Assert.That(perception.HearNoise(Vector3.forward, Noise(source, 1000f)), Is.True);

            perception = new Perception(22f, 120f, query, hearingRange: 0f);
            Assert.That(perception.HearNoise(Vector3.zero, Noise(Vector3.zero, 1000f)), Is.False);
            Assert.That(perception.HasLastKnownPosition, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidOrSilentNoise_DoesNotChangeMemory(float radius)
        {
            Assert.That(perception.HearNoise(Vector3.zero, Noise(Vector3.zero, radius)), Is.False);
            Assert.That(perception.HeardNoise, Is.False);
            Assert.That(perception.HasLastKnownPosition, Is.False);
        }

        [Test]
        public void StrongestPendingNoiseWins_AndClearingAllowsANewQuietNoise()
        {
            Vector3 loud = Vector3.right * 2f;
            Vector3 quiet = Vector3.forward * 8f;
            Assert.That(perception.HearNoise(Vector3.zero, Noise(loud, 10f)), Is.True);
            Assert.That(perception.HearNoise(Vector3.zero, Noise(quiet, 10f)), Is.False);
            Assert.That(perception.NoisePosition, Is.EqualTo(loud));
            Assert.That(perception.LastKnownPosition, Is.EqualTo(loud));

            perception.ClearHeardNoise();

            Assert.That(perception.HeardNoise, Is.False);
            Assert.That(perception.NoiseLoudness, Is.Zero);
            Assert.That(perception.LastKnownPosition, Is.EqualTo(loud));
            Assert.That(perception.HearNoise(Vector3.zero, Noise(quiet, 10f)), Is.True);
            Assert.That(perception.LastKnownPosition, Is.EqualTo(quiet));
        }

        [Test]
        public void EqualStrengthNoise_RemembersTheNewestPosition()
        {
            perception.HearNoise(Vector3.zero, Noise(Vector3.right * 5f, 10f));

            Assert.That(perception.HearNoise(Vector3.zero, Noise(Vector3.forward * 5f, 10f)), Is.True);
            Assert.That(perception.NoisePosition, Is.EqualTo(Vector3.forward * 5f));
        }

        [Test]
        public void SeeingPlayer_TakesPriorityOverANoiseUntilSightIsLost()
        {
            Vector3 player = Vector3.forward * 10f;
            Vector3 noise = Vector3.right * 5f;
            perception.UpdateVision(Vector3.zero, Vector3.forward, player);

            perception.HearNoise(Vector3.zero, Noise(noise, 10f));

            Assert.That(perception.CanSeePlayer, Is.True);
            Assert.That(perception.HeardNoise, Is.True);
            Assert.That(perception.NoisePosition, Is.EqualTo(noise));
            Assert.That(perception.LastKnownPosition, Is.EqualTo(player));

            perception.ClearHeardNoise();
            perception.UpdateVision(Vector3.zero, Vector3.back, player);
            perception.HearNoise(Vector3.zero, Noise(noise, 10f));

            Assert.That(perception.CanSeePlayer, Is.False);
            Assert.That(perception.LastKnownPosition, Is.EqualTo(noise));
        }

        [Test]
        public void Forget_ClearsVisionAndNoiseMemory()
        {
            perception.UpdateVision(Vector3.zero, Vector3.forward, Vector3.forward * 5f);
            perception.HearNoise(Vector3.zero, Noise(Vector3.right, 10f));

            perception.Forget();

            Assert.That(perception.CanSeePlayer, Is.False);
            Assert.That(perception.HeardNoise, Is.False);
            Assert.That(perception.HasLastKnownPosition, Is.False);
            Assert.That(perception.LastKnownPosition, Is.EqualTo(Vector3.zero));
            Assert.That(perception.NoisePosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void InvalidHearingSettings_AreRejectedAtConstruction()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Perception(22f, 120f, query, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Perception(22f, 120f, query, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Perception(22f, 120f, query, 65f, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Perception(22f, 120f, query, 65f, 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Perception(22f, 120f, query, 65f, float.NaN));
        }

        private static NoiseEvent Noise(Vector3 position, float radius)
        {
            return new NoiseEvent(position, radius, NoiseType.Footstep, null);
        }
    }
}
