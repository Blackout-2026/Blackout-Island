// Owner: Systems Engineer (Hunter AI).
using System;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>
    /// The data behind the Hunter's gizmos (docs/reference/systems-engineer.md section 16): the prototype's colours,
    /// the shape directions, and the recent noises the HearingSystem keeps. The drawing code only reads these.
    /// </summary>
    public class HunterGizmoTests
    {
        private sealed class FixedWalls : IOcclusionCounter
        {
            public int Walls;
            public int CountWalls(Vector3 from, Vector3 to, int maxWalls) => Mathf.Min(Walls, maxWalls);
        }

        private GameObject hunter;
        private HearingConfig config;
        private HearingSystem hearing;
        private FixedWalls walls;
        private float now;

        [SetUp]
        public void SetUp()
        {
            now = 10f;
            hunter = new GameObject("Hunter") { hideFlags = HideFlags.HideAndDontSave };
            config = ScriptableObject.CreateInstance<HearingConfig>();
            hearing = hunter.AddComponent<HearingSystem>();
            hearing.SetConfig(config);
            hearing.SetClock(() => now);
            walls = new FixedWalls();
            hearing.SetOcclusion(walls);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(hunter);
            Object.DestroyImmediate(config);
        }

        [TestCase("Attack")]
        [TestCase("Pursue")]
        public void Branch_InCombat_IsRed(string branch)
        {
            Assert.AreEqual(HunterGizmos.Combat, HunterGizmos.ForBranch(branch));
        }

        [TestCase("Investigate")]
        [TestCase("CautiousApproach")]
        [TestCase("Search")]
        [TestCase("FollowOrders")]
        public void Branch_Suspicious_IsOrange(string branch)
        {
            Assert.AreEqual(HunterGizmos.Suspicious, HunterGizmos.ForBranch(branch));
        }

        [TestCase("Roam")]
        [TestCase("Idle")]
        public void Branch_Calm_IsGreen(string branch)
        {
            Assert.AreEqual(HunterGizmos.Calm, HunterGizmos.ForBranch(branch));
        }

        [Test]
        public void Colours_AreThePrototypesOverlay()
        {
            Assert.AreEqual(new Color(0x40 / 255f, 1f, 0x80 / 255f), HunterGizmos.Calm);
            Assert.AreEqual(new Color(1f, 0xB0 / 255f, 0x30 / 255f), HunterGizmos.Suspicious);
            Assert.AreEqual(new Color(1f, 0x30 / 255f, 0x20 / 255f), HunterGizmos.Combat);
        }

        [TestCase(NoiseType.Gunshot, 0xFF5030)]
        [TestCase(NoiseType.Generator, 0xFF30FF)]
        [TestCase(NoiseType.Alarm, 0xFF30FF)]
        [TestCase(NoiseType.Footstep, 0xFFD040)]
        [TestCase(NoiseType.Sprint, 0xFFD040)]
        [TestCase(NoiseType.Door, 0xFFD040)]
        public void NoiseRings_UseThePrototypesColours(NoiseType type, int rgb)
        {
            var expected = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

            Assert.AreEqual(expected, HunterGizmos.ForNoise(type));
        }

        [Test]
        public void Direction_IsClockwiseFromForward()
        {
            Assert.AreEqual(0f, Vector3.Distance(Vector3.forward, HunterGizmos.Direction(0f)), 1e-6f);
            Assert.AreEqual(0f, Vector3.Distance(Vector3.right, HunterGizmos.Direction(Mathf.PI / 2f)), 1e-6f);
        }

        [Test]
        public void RecentNoises_NewestFirst_WithWallsAndWhetherRemembered()
        {
            walls.Walls = 1;
            hearing.Hear(Noise(new Vector3(0f, 0f, 6f), 13f, NoiseType.Sprint));
            now += 1f;
            walls.Walls = 0;
            hearing.Hear(Noise(new Vector3(0f, 0f, 20f), 42f, NoiseType.Gunshot));

            Assert.AreEqual(2, hearing.RecentCount);
            HearingSystem.HeardNoise newest = hearing.GetRecent(0);
            Assert.AreEqual(NoiseType.Gunshot, newest.Type);
            Assert.AreEqual(42f, newest.Radius);
            Assert.AreEqual(11f, newest.Time);
            Assert.AreEqual(0, newest.Walls);
            Assert.IsTrue(newest.Remembered);

            HearingSystem.HeardNoise oldest = hearing.GetRecent(1);
            Assert.AreEqual(NoiseType.Sprint, oldest.Type);
            Assert.AreEqual(1, oldest.Walls);
            Assert.AreEqual((1f - 6f / 13f) * 0.35f * 1.7f, oldest.Perceived, 1e-5f);
        }

        [Test]
        public void RecentNoises_KeepOnesThatCouldNotBeHeard_WithoutWalls()
        {
            hearing.Hear(Noise(new Vector3(0f, 0f, 30f), 13f, NoiseType.Footstep));

            HearingSystem.HeardNoise noise = hearing.GetRecent(0);
            Assert.AreEqual(-1, noise.Walls, "beyond its radius, so the raycast was skipped");
            Assert.IsFalse(noise.Remembered);
        }

        [Test]
        public void RecentNoises_LeaveOutTheHuntersOwn()
        {
            hearing.Hear(new NoiseEvent(new Vector3(0f, 0f, 5f), 40f, NoiseType.Gunshot, hunter));

            Assert.AreEqual(0, hearing.RecentCount);
        }

        [Test]
        public void RecentNoises_KeepTheLastEight()
        {
            for (int i = 1; i <= 10; i++)
            {
                now += 0.1f;
                hearing.Hear(Noise(new Vector3(0f, 0f, i), 13f, NoiseType.Footstep));
            }

            Assert.AreEqual(HearingSystem.RecentCapacity, hearing.RecentCount);
            Assert.AreEqual(10f, hearing.GetRecent(0).Position.z, "newest");
            Assert.AreEqual(3f, hearing.GetRecent(7).Position.z, "the first two were dropped");
            Assert.Throws<ArgumentOutOfRangeException>(() => hearing.GetRecent(8));
        }

        [Test]
        public void RecordingNoises_AllocatesNothing()
        {
            NoiseEvent noise = Noise(new Vector3(0f, 0f, 6f), 13f, NoiseType.Sprint);
            for (int i = 0; i < 10; i++)
            {
                hearing.Hear(noise);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                now += 0.05f;
                hearing.Hear(noise);
                _ = hearing.GetRecent(0);
            }

            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        private static NoiseEvent Noise(Vector3 position, float radius, NoiseType type)
        {
            return new NoiseEvent(position, radius, type, null);
        }
    }
}
