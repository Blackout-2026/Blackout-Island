// Owner: Core Developer (Tactical Guard AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.TacticalGuard.Tests
{
    /// <summary>The whole brain with a fake context and a fake sensor: world in, AgentIntent out.</summary>
    public sealed class TacticalGuardBrainTests
    {
        private sealed class FakeContext : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health { get; set; }
            public Blackboard Blackboard { get; set; }
            public Transform Player { get; set; }
            public readonly List<Marker> covers = new List<Marker>();
            public readonly List<Marker> spawns = new List<Marker>();

            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null)
            {
                return type == MarkerType.CoverPoint ? covers : type == MarkerType.AgentSpawn ? spawns : new List<Marker>();
            }
        }

        // Answers the guard's sight line (target = player's chest) from a flag, and any cover check from a set of exposed x positions.
        private sealed class FakeSensor : IGuardSensor
        {
            public bool playerVisible;
            public readonly HashSet<int> exposedCoverX = new HashSet<int>();
            public float chestHeight;

            public bool HasLineOfSight(Vector3 from, Vector3 to)
            {
                if (Mathf.Abs(to.y - chestHeight) < 0.001f)
                {
                    return playerVisible;
                }
                return exposedCoverX.Contains(Mathf.RoundToInt(to.x));
            }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private TacticalGuardConfig config;
        private FakeSensor sensor;
        private CoverClaims claims;
        private FakeContext context;
        private TacticalGuardBrain brain;
        private float time;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<TacticalGuardConfig>();
            sensor = new FakeSensor { chestHeight = config.playerChestHeight };
            claims = new CoverClaims();
            time = 100f;
            context = NewContext("TG-01", Vector3.zero);
            context.Player = NewObject("Player", new Vector3(0f, 0f, 12f)).transform;
            brain = NewBrain(context);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            created.Clear();
            UnityEngine.Object.DestroyImmediate(config);
        }

        private GameObject NewObject(string objectName, Vector3 position)
        {
            var go = new GameObject(objectName);
            go.transform.position = position;
            created.Add(go);
            return go;
        }

        private FakeContext NewContext(string guardName, Vector3 position)
        {
            GameObject guard = NewObject(guardName, position);
            Health health = guard.AddComponent<Health>();
            health.ResetHealth(0f); // Awake does not run in EditMode tests
            return new FakeContext { Self = guard.transform, Health = health, Blackboard = new Blackboard() };
        }

        private TacticalGuardBrain NewBrain(FakeContext ctx)
        {
            TacticalGuardBrain b = ctx.Self.gameObject.AddComponent<TacticalGuardBrain>();
            b.SetConfig(config);
            b.SetSensor(sensor);
            b.SetClaims(claims);
            b.SetClock(() => time);
            b.Initialize(ctx);
            return b;
        }

        private Marker AddCover(FakeContext ctx, float x, float z)
        {
            Marker marker = NewObject("CoverPoint_" + x, new Vector3(x, 0f, z)).AddComponent<Marker>();
            ctx.covers.Add(marker);
            return marker;
        }

        private void Hurt(FakeContext ctx, float amount)
        {
            ctx.Health.TakeDamage(new DamageInfo(amount, Vector3.zero, Vector3.zero, null, DamageType.Bullet));
        }

        [Test]
        public void Calm_GuardAtItsPostHoldsAndScans()
        {
            AgentIntent intent = brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.HoldPost, brain.CurrentAction);
            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
            Assert.IsTrue(intent.hasLookTarget);
            Assert.IsFalse(intent.fire);
        }

        [Test]
        public void Calm_GuardAwayFromItsPostWalksBack()
        {
            context.Self.position = new Vector3(8f, 0f, 0f);

            AgentIntent intent = brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.HoldPost, brain.CurrentAction);
            Assert.AreEqual(MoveSpeed.Walk, intent.moveSpeed);
            Assert.AreEqual(brain.Post, intent.moveTarget);
        }

        [Test]
        public void Post_IsTheGuardsAgentSpawnMarkerWhenThereIsOne()
        {
            FakeContext other = NewContext("TG-02", new Vector3(30f, 0f, 30f));
            Marker spawn = NewObject("AgentSpawn_TG-02", new Vector3(2.5f, 0f, 129f)).AddComponent<Marker>();
            other.spawns.Add(spawn);

            TacticalGuardBrain b = NewBrain(other);

            Assert.AreEqual(new Vector3(2.5f, 0f, 129f), b.Post);
        }

        [Test]
        public void VisibleHealthyPlayerInRange_GuardAttacksTheChest()
        {
            sensor.playerVisible = true;

            AgentIntent intent = brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.Attack, brain.CurrentAction);
            Assert.IsTrue(intent.fire);
            Assert.IsTrue(intent.aim);
            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
            Assert.AreEqual(new Vector3(0f, config.playerChestHeight, 12f), intent.lookTarget);
        }

        [Test]
        public void PlayerBeyondSightRange_IsNotSeen()
        {
            sensor.playerVisible = true;
            context.Player.position = new Vector3(0f, 0f, config.sightRange + 5f);

            brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.HoldPost, brain.CurrentAction);
            Assert.IsTrue(float.IsNegativeInfinity(context.Blackboard.lastKnownPlayerTime));
        }

        [Test]
        public void Seeing_ThePlayerWritesASightingToTheBlackboard()
        {
            sensor.playerVisible = true;

            brain.Tick(0.1f);

            Assert.AreEqual(100f, context.Blackboard.lastKnownPlayerTime);
            Assert.AreEqual(new Vector3(0f, 0f, 12f), context.Blackboard.lastKnownPlayerPos);
        }

        [Test]
        public void HurtAndLowOnHealth_GuardRunsToCoverAndClaimsIt()
        {
            Marker cover = AddCover(context, 3f, 0f);
            sensor.playerVisible = true;
            Hurt(context, 70f);

            AgentIntent intent = brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.MoveToCover, brain.CurrentAction);
            Assert.AreEqual(MoveSpeed.Run, intent.moveSpeed);
            Assert.AreEqual(cover.transform.position, intent.moveTarget);
            Assert.IsTrue(intent.fire, "shoots on the way because the player is still visible");
            Assert.AreSame(cover, brain.HeldCover);
            Assert.IsTrue(claims.IsClaimedByOther(cover, new object()));
        }

        [Test]
        public void TwoGuardsUnderFire_ClaimDifferentCoverPoints()
        {
            FakeContext second = NewContext("TG-02", new Vector3(1f, 0f, 0f));
            second.Player = context.Player;
            TacticalGuardBrain brainB = NewBrain(second);
            Marker near = AddCover(context, 3f, 0f);
            Marker far = AddCover(context, -6f, 0f);
            second.covers.AddRange(context.covers);
            sensor.playerVisible = true;
            Hurt(context, 70f);
            Hurt(second, 70f);

            brain.Tick(0.1f);
            brainB.Tick(0.1f);

            Assert.AreEqual(GuardActionType.MoveToCover, brain.CurrentAction);
            Assert.AreEqual(GuardActionType.MoveToCover, brainB.CurrentAction);
            Assert.IsNotNull(brain.HeldCover);
            Assert.IsNotNull(brainB.HeldCover);
            Assert.AreNotSame(brain.HeldCover, brainB.HeldCover);
            Assert.AreEqual(2, claims.Count);
            Assert.IsTrue(near != null && far != null);
        }

        [Test]
        public void NoCoverAvailable_HurtGuardDoesNotChooseCover()
        {
            sensor.playerVisible = true;
            Hurt(context, 70f);

            brain.Tick(0.1f);

            Assert.AreNotEqual(GuardActionType.MoveToCover, brain.CurrentAction);
            Assert.IsNull(brain.HeldCover);
        }

        [Test]
        public void CoverExposedToThePlayer_IsNotChosen()
        {
            AddCover(context, 3f, 0f);
            sensor.exposedCoverX.Add(3);
            sensor.playerVisible = true;
            Hurt(context, 70f);

            brain.Tick(0.1f);

            Assert.IsNull(brain.HeldCover);
        }

        [Test]
        public void ClaimedCover_IsReleasedWhenTheGuardReturnsToItsPost()
        {
            AddCover(context, 3f, 0f);
            sensor.playerVisible = true;
            Hurt(context, 70f);
            brain.Tick(0.1f);
            Assert.IsNotNull(brain.HeldCover);

            sensor.playerVisible = false;
            time += 30f; // damage and sighting are long forgotten
            brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.HoldPost, brain.CurrentAction);
            Assert.IsNull(brain.HeldCover);
            Assert.AreEqual(0, claims.Count);
        }

        [Test]
        public void ClaimedCover_IsReleasedWhenTheGuardDies()
        {
            AddCover(context, 3f, 0f);
            sensor.playerVisible = true;
            Hurt(context, 70f);
            brain.Tick(0.1f);
            Assert.AreEqual(1, claims.Count);

            Hurt(context, 100f);

            Assert.AreEqual(0, claims.Count);
        }

        [Test]
        public void JustLostSightOfThePlayer_GuardRepositionsTowardTheLastKnownPosition()
        {
            sensor.playerVisible = true;
            brain.Tick(0.1f); // sighting recorded
            sensor.playerVisible = false;
            time += 1f;

            AgentIntent intent = brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.Reposition, brain.CurrentAction);
            Assert.AreEqual(MoveSpeed.Run, intent.moveSpeed);
            Assert.AreEqual(new Vector3(0f, 0f, 12f), intent.moveTarget);
            Assert.IsFalse(intent.fire);
        }

        [Test]
        public void Reposition_NeverLeadsFurtherFromThePostThanTheLimit()
        {
            context.Blackboard.lastKnownPlayerPos = new Vector3(0f, 0f, 90f);
            context.Blackboard.lastKnownPlayerTime = time;
            context.Player = null;

            AgentIntent intent = brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.Reposition, brain.CurrentAction);
            Assert.That(intent.moveTarget.z, Is.EqualTo(config.repositionLimit).Within(0.001f));
        }

        [Test]
        public void Alert_WidensTheRepositionLimit()
        {
            context.Blackboard.lastKnownPlayerPos = new Vector3(0f, 0f, 90f);
            context.Blackboard.lastKnownPlayerTime = time;
            context.Blackboard.alertLevel = 1;
            context.Player = null;

            AgentIntent intent = brain.Tick(0.1f);

            Assert.That(intent.moveTarget.z, Is.EqualTo(config.repositionLimitAlert).Within(0.001f));
        }

        [Test]
        public void SquadKnowledge_AnotherAgentsSightingMakesTheGuardRespond()
        {
            context.Blackboard.lastKnownPlayerPos = new Vector3(5f, 0f, 8f);
            context.Blackboard.lastKnownPlayerTime = time - 2f;

            brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.Reposition, brain.CurrentAction);
        }

        [Test]
        public void OldSighting_IsForgottenAndTheGuardHolds()
        {
            context.Blackboard.lastKnownPlayerPos = new Vector3(5f, 0f, 8f);
            context.Blackboard.lastKnownPlayerTime = time - config.knowledgeWindow - 1f;

            brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.HoldPost, brain.CurrentAction);
        }

        [Test]
        public void NoPlayerInTheScene_GuardJustHoldsItsPost()
        {
            context.Player = null;

            AgentIntent intent = brain.Tick(0.1f);

            Assert.AreEqual(GuardActionType.HoldPost, brain.CurrentAction);
            Assert.IsFalse(intent.fire);
        }

        [Test]
        public void Damage_RaisesThreatForAWhileThenFades()
        {
            AddCover(context, 3f, 0f);
            Hurt(context, 70f); // shot by someone it cannot see
            context.Blackboard.lastKnownPlayerPos = new Vector3(0f, 0f, 12f);
            context.Blackboard.lastKnownPlayerTime = time;

            brain.Tick(0.1f);
            Assert.AreEqual(GuardActionType.MoveToCover, brain.CurrentAction);

            time += config.damageWindow + 1f;
            context.Blackboard.lastKnownPlayerTime = time; // still hears about the player, but no longer under fire
            brain.Tick(0.1f);
            Assert.AreEqual(GuardActionType.Reposition, brain.CurrentAction);
        }

        [Test]
        public void GetDebugText_NamesTheCurrentAction()
        {
            brain.Tick(0.1f);

            StringAssert.Contains("HoldPost", brain.GetDebugText());
        }

        [Test]
        public void TickBeforeInitialize_ReturnsIdle()
        {
            TacticalGuardBrain uninitialised = NewObject("Loose", Vector3.zero).AddComponent<TacticalGuardBrain>();

            AgentIntent intent = uninitialised.Tick(0.1f);

            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
            Assert.IsFalse(intent.fire);
        }

        [Test]
        public void MissingConfig_FallsBackToDefaultsInsteadOfFailing()
        {
            FakeContext other = NewContext("TG-09", Vector3.zero);
            TacticalGuardBrain b = other.Self.gameObject.AddComponent<TacticalGuardBrain>();
            b.SetSensor(sensor);
            b.SetClaims(claims);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no TacticalGuardConfig"));
            b.Initialize(other);
            b.Tick(0.1f);

            Assert.IsNotNull(b.Config);
            UnityEngine.Object.DestroyImmediate(b.Config); // EditMode never runs OnDestroy, so the fallback asset is freed here
        }
    }
}
