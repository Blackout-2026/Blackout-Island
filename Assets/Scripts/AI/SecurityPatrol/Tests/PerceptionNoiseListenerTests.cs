// Owner: World Builder (Security Patrol AI).
using System;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.AI.SecurityPatrol.Tests
{
    public sealed class PerceptionNoiseListenerTests
    {
        private sealed class NoSightQuery : IPerceptionRaycast
        {
            public bool IsBlocked(Vector3 origin, Vector3 target) => false;
        }

        private NoiseEventChannel channel;
        private GameObject owner;
        private GameObject weapon;
        private Perception perception;
        private PerceptionNoiseListener listener;

        [SetUp]
        public void SetUp()
        {
            channel = ScriptableObject.CreateInstance<NoiseEventChannel>();
            owner = new GameObject("Temporary patrol noise owner");
            weapon = new GameObject("Temporary owned weapon");
            weapon.transform.SetParent(owner.transform);
            perception = new Perception(22f, 120f, new NoSightQuery());
            listener = new PerceptionNoiseListener(perception, channel, Vector3.zero, owner);
        }

        [TearDown]
        public void TearDown()
        {
            listener.Dispose();
            Object.DestroyImmediate(channel);
            Object.DestroyImmediate(owner);
        }

        [Test]
        public void RaisedNoise_UsesTheLatestSuppliedListenerPosition()
        {
            NoiseEvent noise = Noise(Vector3.forward * 8f);
            channel.Raise(noise);
            Assert.That(perception.HeardNoise, Is.False);

            listener.Position = Vector3.forward * 7f;
            channel.Raise(noise);

            Assert.That(perception.HeardNoise, Is.True);
            Assert.That(perception.LastKnownPosition, Is.EqualTo(noise.position));
            Assert.That(perception.NoiseLoudness, Is.EqualTo(0.75f));
        }

        [Test]
        public void InitialPosition_IsUsedBeforeTheFirstPositionUpdate()
        {
            listener.Dispose();
            listener = new PerceptionNoiseListener(perception, channel, Vector3.forward * 7f, owner);

            channel.Raise(Noise(Vector3.forward * 8f));

            Assert.That(perception.HeardNoise, Is.True);
            Assert.That(perception.NoiseLoudness, Is.EqualTo(0.75f));
        }

        [Test]
        public void OwnRootAndChildSources_AreIgnored()
        {
            channel.Raise(Noise(Vector3.right, owner));
            channel.Raise(Noise(Vector3.right, weapon));
            Assert.That(perception.HeardNoise, Is.False);
            Assert.That(perception.HasLastKnownPosition, Is.False);

            channel.Raise(Noise(Vector3.right));
            Assert.That(perception.HeardNoise, Is.True);
        }

        [Test]
        public void Dispose_UnsubscribesAndIsSafeToRepeat()
        {
            listener.Dispose();
            listener.Dispose();

            channel.Raise(Noise(Vector3.right));

            Assert.That(perception.HeardNoise, Is.False);
            Assert.That(perception.HasLastKnownPosition, Is.False);
        }

        [Test]
        public void MultipleListeners_HaveIndependentMemoryAndSubscriptions()
        {
            Perception otherPerception = new Perception(22f, 120f, new NoSightQuery());
            using (var other = new PerceptionNoiseListener(otherPerception, channel, Vector3.right * 20f))
            {
                channel.Raise(Noise(Vector3.right));
                Assert.That(perception.HeardNoise, Is.True);
                Assert.That(otherPerception.HeardNoise, Is.False);

                listener.Dispose();
                other.Position = Vector3.zero;
                channel.Raise(Noise(Vector3.forward));

                Assert.That(otherPerception.HeardNoise, Is.True);
                Assert.That(perception.NoisePosition, Is.EqualTo(Vector3.right));
            }
        }

        [Test]
        public void MissingPerceptionOrChannel_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PerceptionNoiseListener(null, channel, Vector3.zero));
            Assert.Throws<ArgumentNullException>(() =>
                new PerceptionNoiseListener(perception, null, Vector3.zero));
        }

        [Test]
        public void WarmChannelEventsAndClearing_DoNotAllocateManagedMemory()
        {
            NoiseEvent noise = Noise(Vector3.right);
            for (int i = 0; i < 8; i++)
            {
                channel.Raise(noise);
                perception.ClearHeardNoise();
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
            {
                channel.Raise(noise);
                perception.ClearHeardNoise();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(perception.LastKnownPosition, Is.EqualTo(noise.position));
            Assert.That(allocated, Is.Zero);
        }

        private static NoiseEvent Noise(Vector3 position, GameObject source = null)
        {
            return new NoiseEvent(position, 4f, NoiseType.Footstep, source);
        }
    }
}
