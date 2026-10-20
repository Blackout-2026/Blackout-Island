// Owner: Systems Engineer (Hunter AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>
    /// The Hunter's tree with a fake blackboard (docs/reference/systems-engineer.md section 15): each test writes the
    /// facts the brain would sense, ticks the tree at 10 Hz and checks the AgentIntent. No scene, physics or player.
    /// The Hunter stands at the origin unless a test moves it.
    /// </summary>
    public class HunterTreeTests
    {
        private const float Tick = 0.1f;

        private sealed class FakeWorld : IHunterWorld
        {
            public readonly List<Vector3> Waypoints = new List<Vector3>();
            public Vector3? Cover;
            public Vector3? AdvanceCover;
            public float Random = 0.25f;
            public int AdvanceQueries;

            public int WaypointCount => Waypoints.Count;
            public Vector3 GetWaypoint(int index) => Waypoints[index];

            public bool TryFindCover(Vector3 self, Vector3 threat, float radius, out Vector3 point)
            {
                point = Cover ?? default;
                return Cover.HasValue;
            }

            public bool TryFindAdvanceCover(Vector3 self, Vector3 target, float range, float minGain, out Vector3 point)
            {
                AdvanceQueries++;
                point = AdvanceCover ?? default;
                return AdvanceCover.HasValue;
            }

            public float Random01() => Random;
        }

        private HunterConfig config;
        private FakeWorld world;
        private HunterTree tree;
        private float now;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<HunterConfig>();
            world = new FakeWorld();
            tree = new HunterTree(config, world);
            now = 100f;
            Set(HunterFacts.Self, Vector3.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        // ---- Attack and Pursue --------------------------------------------------------------------------------

        [Test]
        public void Attack_SeesThePlayerInRange_StandsAimsAndFiresAtTheChest()
        {
            SeePlayer(new Vector3(0f, 0f, 20f), canSee: true);

            AgentIntent intent = Step();
            Assert.AreEqual("Attack", tree.ActiveBranch);
            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
            Assert.IsTrue(intent.aim && intent.fire);
            Assert.IsTrue(intent.hasLookTarget);
            Assert.AreEqual(new Vector3(0f, 1.05f, 20f), intent.lookTarget);
        }

        [Test]
        public void Attack_Beyond32m_Pursues_UnlessTheAlertStretchesTheRange()
        {
            SeePlayer(new Vector3(0f, 0f, 40f), canSee: true);
            AgentIntent intent = Step();
            Assert.AreEqual("Pursue", tree.ActiveBranch);
            Assert.IsFalse(intent.fire);

            Set(HunterFacts.RangeMultiplier, 1.4f);
            intent = Step();
            Assert.AreEqual("Attack", tree.ActiveBranch, "40 m < 32 x 1.4");
            Assert.IsTrue(intent.fire);
        }

        [Test]
        public void Pursue_SightJustLost_RunsAtThePlayerWithoutFiring()
        {
            var player = new Vector3(5f, 0f, 20f);
            SeePlayer(player, canSee: false, secondsAgo: 0.3f);

            AgentIntent intent = Step();
            Assert.AreEqual("Pursue", tree.ActiveBranch);
            Assert.AreEqual(player, intent.moveTarget);
            Assert.AreEqual(MoveSpeed.Run, intent.moveSpeed);
            Assert.IsTrue(intent.aim);
            Assert.IsFalse(intent.fire);
        }

        [Test]
        public void Pursue_StopsSixMetresAway()
        {
            SeePlayer(new Vector3(0f, 0f, 5f), canSee: false, secondsAgo: 0.3f);

            AgentIntent intent = Step();
            Assert.AreEqual("Pursue", tree.ActiveBranch);
            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
        }

        // ---- reactions by noise type --------------------------------------------------------------------------

        [TestCase(NoiseType.Footstep, MoveSpeed.Walk)]
        [TestCase(NoiseType.Door, MoveSpeed.Walk)]
        [TestCase(NoiseType.Crate, MoveSpeed.Walk)]
        [TestCase(NoiseType.Melee, MoveSpeed.Walk)]
        [TestCase(NoiseType.Sprint, MoveSpeed.Run)]
        public void Investigate_WalksToFootsteps_RunsToSprinting(NoiseType type, MoveSpeed expected)
        {
            var noise = new Vector3(0f, 0f, 10f);
            Hear(type, noise, 0.2f);

            AgentIntent intent = Step();
            Assert.AreEqual("Investigate", tree.ActiveBranch);
            Assert.AreEqual(noise, intent.moveTarget);
            Assert.AreEqual(expected, intent.moveSpeed);
            Assert.IsTrue(intent.aim);
            Assert.IsFalse(intent.fire);
        }

        [TestCase(NoiseType.Footstep, 4f)]
        [TestCase(NoiseType.Sprint, 2f)]
        public void Investigate_ThenAmbushes_AimingAtTheSpot(NoiseType type, float ambushSeconds)
        {
            var noise = new Vector3(0f, 0f, 10f);
            world.Waypoints.Add(new Vector3(-30f, 0f, 0f));
            Hear(type, noise, 0.2f);
            Step();

            Set(HunterFacts.Self, new Vector3(0f, 0f, 8f));
            AgentIntent intent = Step();
            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed, "ambush: stand still");
            Assert.IsTrue(intent.aim);
            Assert.AreEqual(noise + Vector3.up * 1.2f, intent.lookTarget);

            float elapsed = RunWhile("Investigate", 10f);
            Assert.AreEqual(ambushSeconds, elapsed, 0.25f);
            Assert.AreEqual("Roam", tree.ActiveBranch, "the noise is handled and not investigated again");
        }

        [TestCase(0.04f, 0f, TestName = "Too quiet (not above 0.04)")]
        [TestCase(0.2f, 5.1f, TestName = "Too old (over 5 s)")]
        public void Investigate_IgnoresNoisesTooQuietOrTooOld(float loudness, float secondsAgo)
        {
            Hear(NoiseType.Footstep, new Vector3(0f, 0f, 10f), loudness, secondsAgo);

            Step();
            Assert.AreEqual("Idle", tree.ActiveBranch);
        }

        [Test]
        public void Investigate_IgnoresANoiseAlreadyHandled()
        {
            Hear(NoiseType.Footstep, new Vector3(0f, 0f, 10f), 0.2f);
            Set(HunterFacts.NoiseHandledAt, now + Tick);

            Step();
            Assert.AreEqual("Idle", tree.ActiveBranch);
        }

        [Test]
        public void CautiousApproach_FarGunfire_WalksToTheNextCover_PausesThenMovesOn()
        {
            var shot = new Vector3(0f, 0f, 40f);
            var bush = new Vector3(2f, 0f, 10f);
            world.AdvanceCover = bush;
            Hear(NoiseType.Gunshot, shot, 0.3f);

            AgentIntent intent = Step();
            Assert.AreEqual("CautiousApproach", tree.ActiveBranch);
            Assert.AreEqual(bush, intent.moveTarget);
            Assert.AreEqual(MoveSpeed.Walk, intent.moveSpeed, "slower than footsteps' run");
            Assert.AreEqual(shot + Vector3.up * 1.4f, intent.lookTarget, "eyes on the shot");

            Set(HunterFacts.Self, bush);
            intent = Step();
            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed, "pause at the cover");
            Assert.AreEqual(1, world.AdvanceQueries);

            world.AdvanceCover = new Vector3(-1f, 0f, 20f);
            for (int i = 0; i < 16; i++)
            {
                intent = Step();
            }

            Assert.AreEqual(2, world.AdvanceQueries, "after 1.5 s it looks for the next cover");
            Assert.AreEqual(new Vector3(-1f, 0f, 20f), intent.moveTarget);
            Assert.AreEqual(MoveSpeed.Walk, intent.moveSpeed);
        }

        [Test]
        public void CautiousApproach_NoCover_WalksStraightAtTheNoise()
        {
            var shot = new Vector3(0f, 0f, 40f);
            Hear(NoiseType.Generator, shot, 0.3f);

            AgentIntent intent = Step();
            Assert.AreEqual(shot, intent.moveTarget);
            Assert.AreEqual(MoveSpeed.Walk, intent.moveSpeed);
        }

        [Test]
        public void CautiousApproach_CloseGunfire_RunsToCover_HoldsTwoAndAHalfSeconds()
        {
            var shot = new Vector3(0f, 0f, 10f);
            var tree1 = new Vector3(3f, 0f, 2f);
            world.Cover = tree1;
            Hear(NoiseType.Gunshot, shot, 0.3f);

            AgentIntent intent = Step();
            Assert.AreEqual(tree1, intent.moveTarget);
            Assert.AreEqual(MoveSpeed.Run, intent.moveSpeed);

            Set(HunterFacts.Self, tree1);
            intent = Step();
            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
            Assert.IsTrue(intent.aim);
            Assert.IsFalse(intent.fire);

            float held = RunWhile("CautiousApproach", 10f);
            Assert.AreEqual(2.5f, held, 0.25f);
        }

        [Test]
        public void Gunfire_IsNotInvestigated_AndFootstepsAreNotApproachedCautiously()
        {
            Hear(NoiseType.Gunshot, new Vector3(0f, 0f, 40f), 0.3f);
            Step();
            Assert.AreEqual("CautiousApproach", tree.ActiveBranch);

            Hear(NoiseType.Footstep, new Vector3(0f, 0f, 40f), 0.3f);
            Step();
            Assert.AreEqual("Investigate", tree.ActiveBranch);
        }

        [Test]
        public void Gunfire_NeedsMoreThanSixHundredths()
        {
            Hear(NoiseType.Gunshot, new Vector3(0f, 0f, 40f), 0.05f);

            Step();
            Assert.AreEqual("Idle", tree.ActiveBranch, "0.05 is enough for a footstep, not for gunfire");
        }

        // ---- Search, orders, roam -----------------------------------------------------------------------------

        [Test]
        public void Search_RunsToTheLastSeenPosition_LooksAround_ThenForgets()
        {
            var lastSeen = new Vector3(4f, 0f, 12f);
            Set(HunterFacts.LastSeenTime, now - 3f);
            Set(HunterFacts.LastSeenPosition, lastSeen);

            AgentIntent intent = Step();
            Assert.AreEqual("Search", tree.ActiveBranch);
            Assert.AreEqual(lastSeen, intent.moveTarget);
            Assert.AreEqual(MoveSpeed.Run, intent.moveSpeed);

            Set(HunterFacts.Self, lastSeen);
            intent = Step();
            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed, "look around");
            Assert.IsTrue(intent.hasLookTarget);

            float looked = RunWhile("Search", 10f);
            Assert.AreEqual(3f, looked, 0.25f);
            Assert.AreEqual("Idle", tree.ActiveBranch, "the sighting is handled");
        }

        [Test]
        public void Search_NotAfterTenSeconds()
        {
            Set(HunterFacts.LastSeenTime, now - 10.5f);
            Set(HunterFacts.LastSeenPosition, new Vector3(4f, 0f, 12f));

            Step();
            Assert.AreEqual("Idle", tree.ActiveBranch);
        }

        /// <summary>The brief's order: a fresh noise outranks an old sighting.</summary>
        [Test]
        public void Noise_OutranksAnOldSighting()
        {
            Set(HunterFacts.LastSeenTime, now - 3f);
            Set(HunterFacts.LastSeenPosition, new Vector3(4f, 0f, 12f));
            Hear(NoiseType.Footstep, new Vector3(0f, 0f, 10f), 0.2f);

            Step();
            Assert.AreEqual("Investigate", tree.ActiveBranch);
        }

        [TestCase(DirectiveType.Converge, MoveSpeed.Run)]
        [TestCase(DirectiveType.Regroup, MoveSpeed.Walk)]
        public void Order_MovesThere_AndCompletesOnArrival(DirectiveType type, MoveSpeed speed)
        {
            var point = new Vector3(20f, 0f, 30f);
            GiveOrder(new Directive(type, point, 0f));

            AgentIntent intent = Step();
            Assert.AreEqual("FollowOrders", tree.ActiveBranch);
            Assert.AreEqual(point, intent.moveTarget);
            Assert.AreEqual(speed, intent.moveSpeed);
            Assert.IsFalse(intent.fire, "an order alone never fires");

            Set(HunterFacts.Self, point);
            Step();
            Step();
            Assert.AreEqual("Idle", tree.ActiveBranch, "complete");
        }

        [Test]
        public void SearchZoneOrder_RunsToThreePointsInsideTheRadius_ThenCompletes()
        {
            var centre = new Vector3(20f, 0f, 30f);
            world.Random = 0f;
            GiveOrder(new Directive(DirectiveType.SearchZone, centre, 10f));

            var visited = new List<Vector3>();
            for (int i = 0; i < 3; i++)
            {
                AgentIntent intent = Step();
                Assert.AreEqual("FollowOrders", tree.ActiveBranch);
                Assert.AreEqual(6f, Vector3.Distance(centre, intent.moveTarget), 1e-4f, "0.6 x the radius");
                visited.Add(intent.moveTarget);
                Set(HunterFacts.Self, intent.moveTarget);
            }

            Step();
            Assert.AreEqual("FollowOrders", tree.ActiveBranch, "the third point reached: complete");
            Step();
            Assert.AreEqual("Idle", tree.ActiveBranch);
            Assert.AreEqual(3, new HashSet<Vector3>(visited).Count, "three different points");
        }

        [Test]
        public void Order_InterruptedByANoise_ResumesAfterwards()
        {
            var point = new Vector3(20f, 0f, 30f);
            GiveOrder(new Directive(DirectiveType.Converge, point, 0f));
            Step();

            Hear(NoiseType.Footstep, new Vector3(0f, 0f, 2f), 0.2f);
            Step();
            Assert.AreEqual("Investigate", tree.ActiveBranch);

            RunWhile("Investigate", 10f);
            Assert.AreEqual("FollowOrders", tree.ActiveBranch, "the order was not cleared by the interruption");
        }

        [Test]
        public void Roam_WalksToTheNextWaypoint_ThenLooksAround()
        {
            for (int i = 0; i < 4; i++)
            {
                world.Waypoints.Add(new Vector3(i * 10f, 0f, 50f));
            }

            world.Random = 0.25f;
            AgentIntent intent = Step();
            Assert.AreEqual("Roam", tree.ActiveBranch);
            Assert.AreEqual(world.Waypoints[1], intent.moveTarget, "advance one (random < 0.5)");
            Assert.AreEqual(MoveSpeed.Walk, intent.moveSpeed);
            Assert.IsFalse(intent.aim);

            Set(HunterFacts.Self, world.Waypoints[1]);
            intent = Step();
            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed, "look around");

            float looked = RunWhileStill(10f);
            Assert.AreEqual(2.5f, looked, 0.25f, "2 + 0.25 x (4 - 2) s");
        }

        [Test]
        public void NothingToDo_NoWaypoints_StandsIdle()
        {
            AgentIntent intent = Step();

            Assert.AreEqual("Idle", tree.ActiveBranch);
            Assert.AreEqual(AgentIntent.Idle, intent);
        }

        [Test]
        public void SeeingThePlayer_InterruptsAnInvestigation()
        {
            Hear(NoiseType.Footstep, new Vector3(0f, 0f, 10f), 0.2f);
            Step();
            Assert.AreEqual("Investigate", tree.ActiveBranch);

            SeePlayer(new Vector3(0f, 0f, 15f), canSee: true);
            Step();
            Assert.AreEqual("Attack", tree.ActiveBranch);
        }

        [Test]
        public void Tick_AllocatesNothing()
        {
            for (int i = 0; i < 4; i++)
            {
                world.Waypoints.Add(new Vector3(i * 10f, 0f, 50f));
            }

            void Run(int ticks)
            {
                for (int i = 0; i < ticks; i++)
                {
                    int phase = i % 60;
                    Set(HunterFacts.PlayerDetected, phase < 10);
                    Set(HunterFacts.CanSeePlayer, phase < 10);
                    if (phase < 10)
                    {
                        Set(HunterFacts.LastSeenTime, now);
                        Set(HunterFacts.PlayerPosition, new Vector3(0f, 0f, 15f));
                    }

                    if (phase == 20)
                    {
                        Hear(i % 120 == 20 ? NoiseType.Gunshot : NoiseType.Footstep, new Vector3(0f, 0f, 30f), 0.3f);
                    }

                    Step();
                }
            }

            Run(300);
            long before = GC.GetAllocatedBytesForCurrentThread();
            Run(1200);
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // ---- helpers ------------------------------------------------------------------------------------------

        private AgentIntent Step()
        {
            now += Tick;
            Set(HunterFacts.Now, now);
            return tree.Tick(Tick);
        }

        /// <summary>Ticks while the branch stays active; returns the seconds it lasted.</summary>
        private float RunWhile(string branch, float maxSeconds)
        {
            float elapsed = 0f;
            while (tree.ActiveBranch == branch && elapsed < maxSeconds)
            {
                Step();
                elapsed += Tick;
            }

            return elapsed;
        }

        /// <summary>Ticks while the Hunter stands still; returns the seconds it lasted.</summary>
        private float RunWhileStill(float maxSeconds)
        {
            float elapsed = 0f;
            while (elapsed < maxSeconds && Step().moveSpeed == MoveSpeed.Stop)
            {
                elapsed += Tick;
            }

            return elapsed + Tick;
        }

        private void SeePlayer(Vector3 player, bool canSee, float secondsAgo = 0f)
        {
            Set(HunterFacts.PlayerDetected, true);
            Set(HunterFacts.CanSeePlayer, canSee);
            Set(HunterFacts.PlayerPosition, player);
            Set(HunterFacts.LastSeenTime, now + Tick - secondsAgo);
            Set(HunterFacts.LastSeenPosition, player);
        }

        private void Hear(NoiseType type, Vector3 position, float loudness, float secondsAgo = 0f)
        {
            Set(HunterFacts.HasNoise, true);
            Set(HunterFacts.HeardNoiseType, type);
            Set(HunterFacts.NoisePosition, position);
            Set(HunterFacts.NoiseLoudness, loudness);
            Set(HunterFacts.NoiseHeardAt, now + Tick - secondsAgo);
        }

        private void GiveOrder(Directive order)
        {
            Set(HunterFacts.HasOrder, true);
            Set(HunterFacts.Order, order);
            Set(HunterFacts.OrderReceivedAt, now);
        }

        private void Set<T>(BlackboardKey<T> key, T value)
        {
            tree.Facts.Set(key, value);
        }
    }
}
