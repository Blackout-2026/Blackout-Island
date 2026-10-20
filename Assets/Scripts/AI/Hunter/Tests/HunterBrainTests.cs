// Owner: Systems Engineer (Hunter AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>
    /// The whole brain: senses, facts, tree and intent (docs/reference/systems-engineer.md section 15). A fake agent
    /// context and a fake sight check stand in for the scene; the HearingSystem is the real one with a fake wall
    /// counter. The Hunter is at the origin facing +z; brain ticks are 0.1 s on a shared fake clock.
    /// </summary>
    public class HunterBrainTests
    {
        private const float Tick = 0.1f;

        private sealed class FakeContext : IAgentContext
        {
            private readonly List<Marker> none = new List<Marker>();

            public Transform Self { get; set; }
            public Health Health => null;
            public Blackboard Blackboard { get; } = new Blackboard();
            public Transform Player { get; set; }

            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null) => none;
        }

        private sealed class FakeSight : ISightCheck
        {
            public bool Clear = true;
            public bool IsClear(Vector3 from, Vector3 to) => Clear;
        }

        private sealed class NoWalls : IOcclusionCounter
        {
            public int CountWalls(Vector3 from, Vector3 to, int maxWalls) => 0;
        }

        private readonly List<Object> created = new List<Object>();
        private FakeContext context;
        private FakeSight sight;
        private HunterBrain brain;
        private HearingSystem hearing;
        private GameObject player;
        private float now;

        [SetUp]
        public void SetUp()
        {
            now = 50f;
            var hunter = Hidden("Hunter-1");
            player = Hidden("Player");
            player.transform.position = new Vector3(0f, 0f, 10f);

            hearing = hunter.AddComponent<HearingSystem>();
            hearing.SetConfig(Track(ScriptableObject.CreateInstance<HearingConfig>()));
            hearing.SetClock(() => now);
            hearing.SetOcclusion(new NoWalls());

            brain = hunter.AddComponent<HunterBrain>();
            brain.SetConfig(Track(ScriptableObject.CreateInstance<HunterConfig>()));
            brain.SetClock(() => now);
            sight = new FakeSight();
            brain.SetSight(sight);

            context = new FakeContext { Self = hunter.transform, Player = player.transform };
            brain.Initialize(context);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        [Test]
        public void SeesTheWalkingPlayer_DetectsThenFires_AndTellsTheSquad()
        {
            int ticks = TicksUntilFire(walking: true);

            Assert.AreEqual(6, ticks, "0.187 awareness a tick, as in HunterVisionTests");
            Assert.AreEqual("Attack", brain.ActiveBranch);
            Assert.AreEqual(now, context.Blackboard.lastKnownPlayerTime);
            Assert.AreEqual(player.transform.position, context.Blackboard.lastKnownPlayerPos);
        }

        /// <summary>The camouflage, end to end: the brain measures the player's speed from their movement.</summary>
        [Test]
        public void StillPlayer_IsDetectedLater_ThanAWalkingOne()
        {
            int still = TicksUntilFire(walking: false);

            Assert.AreEqual(10, still, "first tick counts as walking, then half speed");
        }

        [Test]
        public void WallInTheWay_NeverFires()
        {
            sight.Clear = false;
            for (int i = 0; i < 50; i++)
            {
                Assert.IsFalse(Step().fire);
            }

            Assert.IsFalse(brain.Vision.Detected);
        }

        [TestCase(NoiseType.Footstep, MoveSpeed.Walk)]
        [TestCase(NoiseType.Sprint, MoveSpeed.Run)]
        public void HearsAStep_AndGoesToIt(NoiseType type, MoveSpeed speed)
        {
            sight.Clear = false;
            var noise = new Vector3(-6f, 0f, -4f);
            hearing.Hear(new NoiseEvent(noise, 13f, type, player));

            AgentIntent intent = Step();
            Assert.AreEqual("Investigate", brain.ActiveBranch);
            Assert.AreEqual(noise, intent.moveTarget);
            Assert.AreEqual(speed, intent.moveSpeed);
        }

        [Test]
        public void HearsDistantGunfire_ApproachesCautiously()
        {
            sight.Clear = false;
            var shot = new Vector3(0f, 0f, -30f);
            hearing.Hear(new NoiseEvent(shot, 42f, NoiseType.Gunshot, null));

            AgentIntent intent = Step();
            Assert.AreEqual("CautiousApproach", brain.ActiveBranch);
            Assert.AreEqual(MoveSpeed.Walk, intent.moveSpeed);
            Assert.IsFalse(intent.fire);
        }

        [Test]
        public void CommanderOrder_RunsThere_WithoutFiring()
        {
            sight.Clear = false;
            var point = new Vector3(20f, 0f, 30f);
            brain.OnDirective(new Directive(DirectiveType.Converge, point, 0f));

            AgentIntent intent = Step();
            Assert.AreEqual("FollowOrders", brain.ActiveBranch);
            Assert.AreEqual(point, intent.moveTarget);
            Assert.AreEqual(MoveSpeed.Run, intent.moveSpeed);
            Assert.IsFalse(intent.fire);

            var moved = new Vector3(-20f, 0f, 30f);
            brain.OnDirective(new Directive(DirectiveType.Converge, moved, 0f));
            Assert.AreEqual(moved, Step().moveTarget, "a newer order replaces the old one");
        }

        [Test]
        public void CommanderAlert_SeesFurther()
        {
            player.transform.position = new Vector3(0f, 0f, 30f);
            Step();
            Assert.IsFalse(brain.Vision.CanSee, "30 m is beyond 25 m");

            brain.OnAlert(new AgentAlert(1.25f, 1.4f, 0.9f));
            Step();
            Assert.IsTrue(brain.Vision.CanSee);
        }

        [Test]
        public void DeadPlayer_IsNotDetected()
        {
            var health = player.AddComponent<Health>();
            health.ResetHealth(0f);
            brain.Initialize(context);
            TicksUntilFire(walking: true);
            Assert.IsTrue(brain.Vision.Detected);

            health.TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null, DamageType.Bullet));
            Step();
            Assert.IsFalse(brain.Vision.Detected);
            Assert.IsFalse(Step().fire);
        }

        [Test]
        public void NoPlayerInTheScene_StandsIdle()
        {
            context.Player = null;

            Assert.AreEqual(AgentIntent.Idle, Step());
        }

        [Test]
        public void BeforeInitialize_ReturnsIdle()
        {
            var fresh = Hidden("Fresh").AddComponent<HunterBrain>();

            Assert.AreEqual(AgentIntent.Idle, fresh.Tick(Tick));
        }

        [Test]
        public void NoConfig_UsesTheDefaults_AndWarns()
        {
            var bare = Hidden("Bare").AddComponent<HunterBrain>();
            LogAssert.Expect(LogType.Warning, "Bare: no HunterConfig assigned, using the built-in defaults.");
            bare.Initialize(context);

            Assert.AreEqual(25f, bare.Config.sightRange);
            Track(bare.Config);
        }

        [Test]
        public void Tick_AllocatesNothing()
        {
            void Run(int ticks)
            {
                for (int i = 0; i < ticks; i++)
                {
                    sight.Clear = i % 80 < 30;
                    player.transform.position = new Vector3(i % 7, 0f, 10f);
                    if (i % 40 == 35)
                    {
                        hearing.Hear(new NoiseEvent(new Vector3(-6f, 0f, -4f), 13f, NoiseType.Footstep, null));
                    }

                    Step();
                }
            }

            Run(200);
            long before = GC.GetAllocatedBytesForCurrentThread();
            Run(1000);
            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // ---- debug text (H4) -------------------------------------------------------------------------------------

        [Test]
        public void DebugText_BeforeTheFirstTick_SaysNotStarted()
        {
            var fresh = Hidden("Fresh").AddComponent<HunterBrain>();

            Assert.AreEqual("Hunter [BT] (not started)", fresh.GetDebugText());
        }

        [Test]
        public void DebugText_ShowsTheRunningPath_TheEyes_TheEars_AndTheOrder()
        {
            sight.Clear = false;
            hearing.Hear(new NoiseEvent(new Vector3(-6f, 0f, -4f), 13f, NoiseType.Sprint, player));
            brain.OnDirective(new Directive(DirectiveType.Converge, new Vector3(20f, 0f, 30f), 0f));
            Step();

            string[] lines = brain.GetDebugText().Split('\n');
            Assert.AreEqual(4, lines.Length);
            Assert.AreEqual("Hunter [BT] Investigate > MoveToNoise", lines[0]);
            Assert.AreEqual("eyes: sees no, detected no, awareness 0.00, never seen", lines[1]);
            StringAssert.StartsWith("ears: Sprint 0.76 heard 0.10 s ago through 0 walls", lines[2]);
            Assert.AreEqual("order: Converge (20.00, 30.00)", lines[3]);
        }

        [Test]
        public void DebugText_WhileAttacking()
        {
            TicksUntilFire(walking: true);

            string text = brain.GetDebugText();
            StringAssert.StartsWith("Hunter [BT] Attack > AimAndFire\neyes: sees yes, detected yes", text);
            StringAssert.Contains("last seen 0.00 s ago", text);
        }

        [Test]
        public void DebugText_IsRebuiltOncePerTick()
        {
            Step();
            string first = brain.GetDebugText();
            Assert.AreSame(first, brain.GetDebugText(), "same tick: the same string, nothing allocated");

            Step();
            Assert.AreNotSame(first, brain.GetDebugText());
        }

        [Test]
        public void LastIntent_IsWhatTheTickReturned()
        {
            sight.Clear = false;
            brain.OnDirective(new Directive(DirectiveType.Converge, new Vector3(20f, 0f, 30f), 0f));

            AgentIntent intent = Step();
            Assert.AreEqual(intent, brain.LastIntent);
        }

        private int TicksUntilFire(bool walking)
        {
            for (int tick = 1; tick <= 50; tick++)
            {
                if (walking)
                {
                    // 0.43 m sideways per tick: 4.3 m/s, about 10 m away the whole time.
                    player.transform.position += new Vector3(0.43f, 0f, 0f);
                }

                if (Step().fire)
                {
                    return tick;
                }
            }

            return -1;
        }

        private AgentIntent Step()
        {
            now += Tick;
            return brain.Tick(Tick);
        }

        private GameObject Hidden(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            created.Add(go);
            return go;
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
