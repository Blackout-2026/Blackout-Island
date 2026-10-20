// Owner: Systems Engineer (Hunter AI).
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>The strongest recent noise, its timestamp and its decay (docs/reference/systems-engineer.md section 14).</summary>
    public class NoiseMemoryTests
    {
        private const float Memory = 8f;
        private static readonly Vector3 Shot = new Vector3(10f, 0f, 5f);
        private static readonly Vector3 Step = new Vector3(-3f, 0f, 2f);

        [Test]
        public void Empty_HasNothing()
        {
            var memory = new NoiseMemory(Memory);

            Assert.IsFalse(memory.HasNoise(0f));
            Assert.AreEqual(0f, memory.LoudnessAt(0f));
            Assert.AreEqual(float.PositiveInfinity, memory.TimeSince(0f));
        }

        [Test]
        public void Offer_RemembersTheNoiseAndItsTime()
        {
            var memory = new NoiseMemory(Memory);

            Assert.IsTrue(memory.Offer(Shot, NoiseType.Gunshot, 0.6f, 3f));
            Assert.AreEqual(Shot, memory.Position);
            Assert.AreEqual(NoiseType.Gunshot, memory.Type);
            Assert.AreEqual(3f, memory.HeardAt);
            Assert.AreEqual(0.6f, memory.LoudnessAt(3f));
            Assert.AreEqual(2f, memory.TimeSince(5f));
        }

        [Test]
        public void Decay_HalfAfterFourSeconds_GoneAfterEight()
        {
            var memory = new NoiseMemory(Memory);
            memory.Offer(Shot, NoiseType.Gunshot, 0.6f, 0f);

            Assert.AreEqual(0.3f, memory.LoudnessAt(4f), 1e-6f);
            Assert.IsTrue(memory.HasNoise(7.9f));
            Assert.AreEqual(0f, memory.LoudnessAt(8f));
            Assert.IsFalse(memory.HasNoise(8f));
            Assert.AreEqual(8f, memory.TimeSince(8f), "the timestamp is kept after the noise has faded");
        }

        [Test]
        public void LouderNoise_Replaces()
        {
            var memory = new NoiseMemory(Memory);
            memory.Offer(Step, NoiseType.Footstep, 0.2f, 0f);

            Assert.IsTrue(memory.Offer(Shot, NoiseType.Gunshot, 0.6f, 1f));
            Assert.AreEqual(NoiseType.Gunshot, memory.Type);
        }

        [Test]
        public void QuieterNoise_IsIgnoredWhileTheOldOneIsStillLouder()
        {
            var memory = new NoiseMemory(Memory);
            memory.Offer(Shot, NoiseType.Gunshot, 0.6f, 0f);

            Assert.IsFalse(memory.Offer(Step, NoiseType.Footstep, 0.2f, 1f), "0.2 < 0.525 left of the shot");
            Assert.AreEqual(Shot, memory.Position);
        }

        [Test]
        public void QuieterNoise_WinsOnceTheOldOneHasFadedBelowIt()
        {
            var memory = new NoiseMemory(Memory);
            memory.Offer(Shot, NoiseType.Gunshot, 0.6f, 0f);

            Assert.IsTrue(memory.Offer(Step, NoiseType.Footstep, 0.2f, 6f), "0.2 > 0.15 left of the shot");
            Assert.AreEqual(Step, memory.Position);
            Assert.AreEqual(0f, memory.TimeSince(6f));
        }

        [Test]
        public void SameLoudness_RefreshesThePositionAndTime()
        {
            var memory = new NoiseMemory(Memory);
            memory.Offer(Step, NoiseType.Sprint, 0.5f, 0f);
            var nextStep = new Vector3(-2f, 0f, 3f);

            Assert.IsTrue(memory.Offer(nextStep, NoiseType.Sprint, 0.5f, 0.4f), "a running player's steps stay tracked");
            Assert.AreEqual(nextStep, memory.Position);
            Assert.AreEqual(0.4f, memory.HeardAt);
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        public void SilentNoise_IsNotRemembered(float perceived)
        {
            var memory = new NoiseMemory(Memory);

            Assert.IsFalse(memory.Offer(Shot, NoiseType.Gunshot, perceived, 0f));
            Assert.IsFalse(memory.HasNoise(0f));
        }

        [Test]
        public void Clear_ForgetsTheNoise()
        {
            var memory = new NoiseMemory(Memory);
            memory.Offer(Shot, NoiseType.Gunshot, 0.6f, 0f);
            memory.Clear();

            Assert.IsFalse(memory.HasNoise(0f));
            Assert.AreEqual(float.PositiveInfinity, memory.TimeSince(1f));
        }
    }
}
