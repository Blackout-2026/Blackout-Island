// Owner: World Builder (Security Patrol AI).
using System;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.SecurityPatrol.Tests
{
    public sealed class PerceptionVisionTests
    {
        private sealed class SightQuery : IPerceptionRaycast
        {
            public bool Blocked;
            public int Calls;
            public Vector3 Origin;
            public Vector3 Target;

            public bool IsBlocked(Vector3 origin, Vector3 target)
            {
                Calls++;
                Origin = origin;
                Target = target;
                return Blocked;
            }
        }

        private static readonly Vector3 Eye = new Vector3(3f, 4.12f, 74f);
        private SightQuery query;
        private Perception perception;

        [SetUp]
        public void SetUp()
        {
            query = new SightQuery();
            perception = new Perception(22f, 120f, query);
        }

        [TestCase(-59f, true)]
        [TestCase(59f, true)]
        [TestCase(-61f, false)]
        [TestCase(61f, false)]
        [TestCase(180f, false)]
        public void FullCone_UsesHalfTheConfiguredAngle(float yaw, bool visible)
        {
            Vector3 target = Eye + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 10f;

            perception.UpdateVision(Eye, Vector3.forward, target);

            Assert.That(perception.CanSeePlayer, Is.EqualTo(visible));
            Assert.That(query.Calls, Is.EqualTo(visible ? 1 : 0));
            Assert.That(perception.HasLastKnownPosition, Is.EqualTo(visible));
            if (visible)
                Assert.That(perception.LastKnownPosition, Is.EqualTo(target));
        }

        [TestCase(22f, true)]
        [TestCase(22.01f, false)]
        public void RangeBoundary_IncludesTheMaximumDistance(float distance, bool visible)
        {
            perception.UpdateVision(Eye, Vector3.forward, Eye + Vector3.forward * distance);

            Assert.That(perception.CanSeePlayer, Is.EqualTo(visible));
            Assert.That(query.Calls, Is.EqualTo(visible ? 1 : 0));
        }

        [Test]
        public void RangeMultiplier_IsAbsoluteAndDoesNotModifyTheBaseRange()
        {
            Vector3 target = Eye + Vector3.forward * 25f;
            perception.UpdateVision(Eye, Vector3.forward, target, 1.4f);
            Assert.That(perception.CanSeePlayer, Is.True);
            perception.UpdateVision(Eye, Vector3.forward, target, 1.4f);
            Assert.That(perception.CanSeePlayer, Is.True);
            perception.UpdateVision(Eye, Vector3.forward, Eye + Vector3.forward * 31f, 1.4f);
            Assert.That(perception.CanSeePlayer, Is.False);
            perception.UpdateVision(Eye, Vector3.forward, target);
            Assert.That(perception.CanSeePlayer, Is.False);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidRangeMultiplier_IsRejected(float multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                perception.UpdateVision(Eye, Vector3.forward, Eye + Vector3.forward, multiplier));
        }

        [Test]
        public void HorizontalCone_UsesFull3DDistanceAndTheSuppliedSightPoints()
        {
            Vector3 target = Eye + new Vector3(0f, 8f, 6f);
            perception.UpdateVision(Eye, new Vector3(0f, 5f, 4f), target);

            Assert.That(perception.CanSeePlayer, Is.True);
            Assert.That(query.Origin, Is.EqualTo(Eye));
            Assert.That(query.Target, Is.EqualTo(target));

            perception.UpdateVision(Eye, Vector3.forward, Eye + new Vector3(0f, 22f, 6f));

            Assert.That(perception.CanSeePlayer, Is.False);
            Assert.That(query.Calls, Is.EqualTo(1));
        }

        [Test]
        public void BlockedSight_DoesNotRevealAnUnseenPlayerPosition()
        {
            Vector3 confirmed = Eye + Vector3.forward * 8f;
            perception.UpdateVision(Eye, Vector3.forward, confirmed);
            query.Blocked = true;

            perception.UpdateVision(Eye, Vector3.forward, Eye + Vector3.forward * 12f);

            Assert.That(perception.CanSeePlayer, Is.False);
            Assert.That(perception.HasLastKnownPosition, Is.True);
            Assert.That(perception.LastKnownPosition, Is.EqualTo(confirmed));

            query.Blocked = false;
            perception.UpdateVision(Eye, Vector3.forward, Eye - Vector3.forward * 8f);
            Assert.That(perception.CanSeePlayer, Is.False);
            Assert.That(perception.LastKnownPosition, Is.EqualTo(confirmed));
            Assert.That(query.Calls, Is.EqualTo(2));
        }

        [Test]
        public void ZeroRangeOrMissingHorizontalFacing_DisablesSight()
        {
            perception.UpdateVision(Eye, Vector3.up, Eye + Vector3.forward);
            Assert.That(perception.CanSeePlayer, Is.False);

            perception = new Perception(0f, 120f, query);
            perception.UpdateVision(Eye, Vector3.forward, Eye);
            Assert.That(perception.CanSeePlayer, Is.False);
            Assert.That(perception.HasLastKnownPosition, Is.False);
            Assert.That(query.Calls, Is.Zero);
        }

        [Test]
        public void FullCircle_AllowsSightBehindTheObserver()
        {
            perception = new Perception(22f, 360f, query);
            perception.UpdateVision(Eye, Vector3.forward, Eye - Vector3.forward * 5f);

            Assert.That(perception.CanSeePlayer, Is.True);
        }

        [Test]
        public void InvalidSettingsOrMissingQuery_AreRejectedAtConstruction()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Perception(-1f, 120f, query));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Perception(float.NaN, 120f, query));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Perception(22f, 361f, query));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Perception(22f, float.PositiveInfinity, query));
            Assert.Throws<ArgumentNullException>(() => new Perception(22f, 120f, null));
        }

        [Test]
        public void WarmVisionChecks_DoNotAllocateManagedMemory()
        {
            Vector3 target = Eye + Vector3.forward * 10f;
            for (int i = 0; i < 8; i++)
                perception.UpdateVision(Eye, Vector3.forward, target);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
                perception.UpdateVision(Eye, Vector3.forward, target);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(perception.CanSeePlayer, Is.True);
            Assert.That(allocated, Is.Zero);
        }
    }
}
