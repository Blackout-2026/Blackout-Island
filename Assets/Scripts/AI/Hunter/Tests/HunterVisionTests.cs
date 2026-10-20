// Owner: Systems Engineer (Hunter AI).
using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>
    /// The Hunter's eyes and awareness meter (docs/reference/systems-engineer.md section 15). The Hunter stands at the
    /// origin facing +z; brain ticks are 0.1 s, as AgentRoot thinks at 10 Hz. A walking player 10 m ahead fills
    /// 2.2 x (1.25 - 10 / 25) = 1.87 awareness per second, so 0.187 per tick.
    /// </summary>
    public class HunterVisionTests
    {
        private const float Tick = 0.1f;
        private const float WalkSpeed = 4.3f;
        private const float SprintSpeed = 7f;

        private sealed class FakeSight : ISightCheck
        {
            public Func<Vector3, bool> ClearTo = _ => true;

            public bool IsClear(Vector3 from, Vector3 to) => ClearTo(to);
        }

        private HunterConfig config;
        private HunterVision vision;
        private FakeSight sight;
        private float now;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<HunterConfig>();
            vision = new HunterVision(config);
            sight = new FakeSight();
            now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [TestCase(0f, 10f, true, TestName = "Ahead in range")]
        [TestCase(0f, -10f, false, TestName = "Behind")]
        [TestCase(0f, 30f, false, TestName = "Beyond 25 m")]
        [TestCase(8.39f, 5.45f, true, TestName = "57 degrees to the side, inside the cone")]
        [TestCase(8.66f, 5f, false, TestName = "60 degrees to the side, outside the cone")]
        [TestCase(0f, -2f, true, TestName = "Behind but within 2.2 m")]
        public void InView_ConeRangeAndCloseSense(float x, float z, bool expected)
        {
            bool seen = HunterVision.InView(Vector3.zero, Vector3.forward, new Vector3(x, 0f, z), config.sightRange,
                config.fieldOfViewDegrees, config.closeSenseRange, out _);

            Assert.AreEqual(expected, seen);
        }

        [TestCase(0f, 0.5f)]
        [TestCase(0.29f, 0.5f)]
        [TestCase(WalkSpeed, 1f)]
        [TestCase(SprintSpeed, 1.25f)]
        public void Visibility_FromThePlayersSpeed(float speed, float expected)
        {
            Assert.AreEqual(expected, HunterVision.Visibility(speed, config));
        }

        [Test]
        public void FillRate_FasterUpClose_AndThreeTimesOnceDetected()
        {
            Assert.AreEqual(2.75f, HunterVision.FillRate(0f, 25f, 1f, false, config), 1e-5f);
            Assert.AreEqual(1.87f, HunterVision.FillRate(10f, 25f, 1f, false, config), 1e-5f);
            Assert.AreEqual(5.61f, HunterVision.FillRate(10f, 25f, 1f, true, config), 1e-5f);
        }

        [Test]
        public void WalkingPlayer_IsDetectedOnTheSixthTick()
        {
            Assert.AreEqual(6, TicksToDetect(WalkSpeed));
        }

        /// <summary>The playbook's camouflage: standing still halves the fill rate, so it takes about twice as long.</summary>
        [Test]
        public void StillPlayer_Camouflage_TakesAboutTwiceAsLong()
        {
            Assert.AreEqual(11, TicksToDetect(0f));
        }

        [Test]
        public void SprintingPlayer_IsDetectedSooner()
        {
            Assert.AreEqual(5, TicksToDetect(SprintSpeed));
        }

        [Test]
        public void WallInTheWay_NeverDetects()
        {
            sight.ClearTo = _ => false;
            for (int i = 0; i < 50; i++)
            {
                Step(new Vector3(0f, 0f, 10f), WalkSpeed);
            }

            Assert.IsFalse(vision.CanSee);
            Assert.IsFalse(vision.Detected);
            Assert.AreEqual(0f, vision.Awareness);
        }

        [Test]
        public void HeadHidden_ChestVisible_StillSeen()
        {
            sight.ClearTo = target => target.y < 1.2f;
            Step(new Vector3(0f, 0f, 10f), WalkSpeed);

            Assert.IsTrue(vision.CanSee);
        }

        [Test]
        public void Detection_RecordsTheLastSeenPosition_OnlyOnceDetected()
        {
            var player = new Vector3(1f, 0f, 10f);
            Step(player, WalkSpeed);
            Assert.IsFalse(vision.HasSighting);
            Assert.AreEqual(float.NegativeInfinity, vision.LastSeenTime);

            DetectAt(player);
            Assert.IsTrue(vision.HasSighting);
            Assert.AreEqual(player, vision.LastSeenPosition);
            Assert.AreEqual(now, vision.LastSeenTime, 1e-5f);
        }

        [Test]
        public void Detection_IsLostSixSecondsAfterTheLastSighting()
        {
            DetectAt(new Vector3(0f, 0f, 10f));
            float seenAt = vision.LastSeenTime;
            sight.ClearTo = _ => false;

            while (now < seenAt + 5.85f)
            {
                Step(new Vector3(0f, 0f, 10f), WalkSpeed);
            }

            Assert.IsTrue(vision.Detected, "still detected 5.9 s later");
            Step(new Vector3(0f, 0f, 10f), WalkSpeed);
            Step(new Vector3(0f, 0f, 10f), WalkSpeed);
            Assert.IsFalse(vision.Detected);
            Assert.IsTrue(vision.HasSighting, "the sighting is kept for Search");
        }

        [Test]
        public void Awareness_EmptiesSlowerWhileDetected()
        {
            DetectAt(new Vector3(0f, 0f, 10f));
            float before = vision.Awareness;
            sight.ClearTo = _ => false;
            Step(new Vector3(0f, 0f, 10f), WalkSpeed);

            Assert.AreEqual(before - 0.025f, vision.Awareness, 1e-5f, "0.25 per second while detected");
        }

        [Test]
        public void Awareness_IsCappedAtOneAndAHalf()
        {
            for (int i = 0; i < 100; i++)
            {
                Step(new Vector3(0f, 0f, 3f), SprintSpeed);
            }

            Assert.AreEqual(1.5f, vision.Awareness);
        }

        [Test]
        public void DeadPlayer_EmptiesOnePerSecond_AndIsNotDetected()
        {
            DetectAt(new Vector3(0f, 0f, 10f));
            float before = vision.Awareness;
            vision.Update(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 10f), false, 0f, sight, now += Tick, Tick);

            Assert.IsFalse(vision.Detected);
            Assert.IsFalse(vision.CanSee);
            Assert.AreEqual(before - 0.1f, vision.Awareness, 1e-5f);
        }

        [Test]
        public void CommanderAlert_ExtendsTheRange()
        {
            var player = new Vector3(0f, 0f, 30f);
            Step(player, WalkSpeed);
            Assert.IsFalse(vision.CanSee, "30 m is beyond 25 m");

            vision.RangeMultiplier = 1.4f;
            Step(player, WalkSpeed);
            Assert.IsTrue(vision.CanSee, "and within 35 m");
        }

        private int TicksToDetect(float playerSpeed)
        {
            for (int tick = 1; tick <= 100; tick++)
            {
                Step(new Vector3(0f, 0f, 10f), playerSpeed);
                if (vision.Detected)
                {
                    return tick;
                }
            }

            return -1;
        }

        private void DetectAt(Vector3 player)
        {
            for (int i = 0; i < 20 && !vision.Detected; i++)
            {
                Step(player, WalkSpeed);
            }

            Assert.IsTrue(vision.Detected);
        }

        private void Step(Vector3 player, float playerSpeed)
        {
            now += Tick;
            vision.Update(Vector3.zero, Vector3.forward, player, true, playerSpeed, sight, now, Tick);
        }
    }
}
