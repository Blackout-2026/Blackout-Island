// Owner: Agent Controller (Commander AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Commander.Tests
{
    public sealed class CommanderBrainTests
    {
        private sealed class Context : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health { get; set; }
            public Blackboard Blackboard { get; set; }
            public Transform Player => null;
            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null) => Array.Empty<Marker>();
        }

        private sealed class Receiver : IDirectiveReceiver
        {
            public int Count;
            public Directive Last;
            public Action OnReceive;
            public void OnDirective(Directive directive)
            {
                Count++;
                Last = directive;
                OnReceive?.Invoke();
            }
        }

        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Receiver> receivers = new List<Receiver>();
        private readonly List<BlackboardAgent> units = new List<BlackboardAgent>();
        private CommanderConfig config;
        private CommanderBrain brain;
        private Context context;
        private Blackboard board;
        private float now;

        [SetUp]
        public void SetUp()
        {
            now = 100f;
            board = new Blackboard();
            GameObject commander = MakeObject("Commander");
            context = new Context { Self = commander.transform, Health = commander.GetComponent<Health>(), Blackboard = board };
            brain = commander.AddComponent<CommanderBrain>();
            config = ScriptableObject.CreateInstance<CommanderConfig>();
            brain.SetConfig(config);
            brain.SetClock(() => now);
            board.RegisterAgent("Commander", context.Self, context.Health, null);
            for (int i = 0; i < 3; i++)
                AddUnit("Unit-" + i, new Vector3(50f, 0f, 20f * i));
            brain.Initialize(context);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(objects[i]);
            UnityEngine.Object.DestroyImmediate(config);
            objects.Clear();
            receivers.Clear();
            units.Clear();
        }

        [Test]
        public void CalmCommander_MonitorsWithoutMovingOrShooting()
        {
            AgentIntent intent = brain.Tick(0.1f);

            Assert.That(brain.State, Is.EqualTo("Monitor"));
            Assert.That(brain.CurrentPlan, Is.Empty);
            Assert.That(intent.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(intent.fire, Is.False);
            Assert.That(intent.aim, Is.False);
            Assert.That(receivers[0].Count, Is.Zero);
        }

        [Test]
        public void Observation_UsesSixBlackboardFactsAndExcludesSelfAndDeadUnits()
        {
            Kill(units[2].Health);
            board.cureTaken = true;
            board.generatorDisabled = true;
            board.playerNeutralized = true;
            board.squadAssembled = true;
            SeePlayer();

            brain.Tick(0.1f);
            WorldState snapshot = brain.WorldState;

            Assert.That(snapshot.Count, Is.EqualTo(6));
            Assert.That(snapshot.Satisfies(new WorldState()
                .Set(CommanderFacts.PlayerLocated, true)
                .Set(CommanderFacts.CureTaken, true)
                .Set(CommanderFacts.GeneratorDisabled, true)
                .Set(CommanderFacts.AgentsAlive, 2)
                .Set(CommanderFacts.SquadAssembled, true)
                .Set(CommanderFacts.PlayerNeutralized, true)), Is.True);
        }

        [Test]
        public void CureNotification_ReplansAndIssuesSearchImmediately()
        {
            brain.Tick(0.1f);

            brain.NotifyCureTaken();

            Assert.That(board.cureTaken, Is.True);
            Assert.That(board.alertLevel, Is.EqualTo(1));
            Assert.That(brain.GoalName, Is.EqualTo("PlayerLocated"));
            Assert.That(brain.CurrentAction, Is.EqualTo(CommanderConfig.OrderSearchZone));
            Assert.That(receivers[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void Search_DistributesDistinctTargetsOnConfiguredRing()
        {
            board.alertLevel = 1;
            board.lastKnownPlayerTime = now - 10f;
            board.lastKnownPlayerPos = new Vector3(12f, 0f, 30f);

            brain.Tick(0.1f);

            for (int i = 0; i < receivers.Count; i++)
            {
                Assert.That(receivers[i].Last.type, Is.EqualTo(DirectiveType.SearchZone));
                Assert.That(receivers[i].Last.radius, Is.EqualTo(config.searchRadius));
                Assert.That(Vector3.Distance(receivers[i].Last.position, board.lastKnownPlayerPos),
                    Is.EqualTo(config.searchRingRadius).Within(0.001f));
            }
            Assert.That(Vector3.Distance(receivers[0].Last.position, receivers[1].Last.position), Is.GreaterThan(1f));
        }

        [Test]
        public void Search_ReissuesAtEightSecondsAndDoesNotInventASighting()
        {
            board.alertLevel = 1;
            brain.Tick(0f);
            Advance(7.9f);
            Assert.That(receivers[0].Count, Is.EqualTo(1));

            Advance(0.2f);

            Assert.That(receivers[0].Count, Is.EqualTo(2));
            Assert.That(board.lastKnownPlayerTime, Is.EqualTo(float.NegativeInfinity));
            Assert.That(brain.WorldState.Satisfies(new WorldState().Set(CommanderFacts.PlayerLocated, false)), Is.True);
        }

        [Test]
        public void SearchTimeout_ClearsPlanAndWaitsBeforeRetrying()
        {
            board.alertLevel = 1;
            brain.Tick(0f);
            int version = brain.PlanVersion;

            Advance(config.searchTimeout + 0.1f);
            Assert.That(brain.State, Is.EqualTo("ReplanPending"));
            Assert.That(brain.CurrentPlan, Is.Empty);
            Advance(0.1f);
            Assert.That(brain.PlanVersion, Is.EqualTo(version));
            Advance(config.searchInterval + 0.1f);
            Assert.That(brain.PlanVersion, Is.EqualTo(version + 1));
            Assert.That(receivers[0].Last.type, Is.EqualTo(DirectiveType.SearchZone));
        }

        [Test]
        public void FreshSighting_ChangesGoalAndOrdersConverge()
        {
            board.alertLevel = 1;
            brain.Tick(0f);
            board.squadAssembled = true;
            SeePlayer();

            brain.Tick(0.1f);

            Assert.That(brain.GoalName, Is.EqualTo("PlayerNeutralized"));
            Assert.That(brain.CurrentAction, Is.EqualTo(CommanderConfig.OrderConverge));
            Assert.That(receivers[0].Last.type, Is.EqualTo(DirectiveType.Converge));
            Assert.That(receivers[0].Last.position, Is.EqualTo(board.lastKnownPlayerPos));
            Assert.That(board.playerNeutralized, Is.False, "A predicted effect must not kill the player.");
        }

        [Test]
        public void Converge_ReissuesOncePerSecondAtUpdatedLastKnownPosition()
        {
            StartConverge();
            Advance(0.9f);
            Assert.That(receivers[0].Count, Is.EqualTo(1));
            board.lastKnownPlayerPos = new Vector3(30f, 0f, 40f);

            Advance(0.2f);

            Assert.That(receivers[0].Count, Is.EqualTo(2));
            Assert.That(receivers[0].Last.position, Is.EqualTo(board.lastKnownPlayerPos));
        }

        [Test]
        public void StaleSighting_InvalidatesConvergeAndStartsSearch()
        {
            StartConverge();

            Advance(config.sightingMemory + 0.1f);

            Assert.That(brain.GoalName, Is.EqualTo("PlayerLocated"));
            Assert.That(receivers[0].Last.type, Is.EqualTo(DirectiveType.SearchZone));
        }

        [Test]
        public void Regroup_IssuesOnceAndWaitsForTwoUnitsAtMidpoint()
        {
            board.alertLevel = 1;
            SeePlayer();
            Vector3 expectedRally = board.lastKnownPlayerPos * 0.5f;
            brain.Tick(0f);
            Assert.That(brain.CurrentAction, Is.EqualTo(CommanderConfig.OrderRegroup));
            Assert.That(receivers[0].Last.position, Is.EqualTo(expectedRally));
            Assert.That(board.squadAssembled, Is.False);
            Advance(0.5f);
            Assert.That(receivers[0].Count, Is.EqualTo(1));

            units[0].Transform.position = expectedRally;
            units[1].Transform.position = expectedRally;
            Advance(0.1f);
            Assert.That(board.squadAssembled, Is.True);
            Advance(0.1f);
            Assert.That(receivers[0].Last.type, Is.EqualTo(DirectiveType.Converge));
        }

        [Test]
        public void RegroupTimeout_CompletesWithEnoughSurvivorsAsInReference()
        {
            config.sightingMemory = 30f; // isolate the regroup timeout from the 6 s sighting expiry
            board.alertLevel = 1;
            SeePlayer();
            brain.Tick(0f);

            Advance(config.regroupTimeout + 0.1f);

            Assert.That(board.squadAssembled, Is.True);
            Assert.That(brain.CurrentAction, Is.EqualTo(CommanderConfig.OrderConverge));
        }

        [Test]
        public void RegroupTimeout_DoesNotInventAdditionalLivingAgents()
        {
            config.sightingMemory = 30f;
            Kill(units[1].Health);
            Kill(units[2].Health);
            board.alertLevel = 1;
            SeePlayer();
            brain.Tick(0f);

            Advance(config.regroupTimeout + 0.1f);

            Assert.That(board.squadAssembled, Is.False);
            Assert.That(brain.State, Is.EqualTo("ReplanPending"));
            Assert.That(receivers[1].Count, Is.Zero);
        }

        [Test]
        public void ConfiguredPrecondition_BlocksSearchUntilGeneratorIsDisabled()
        {
            config.orderSearchZone.preconditions = new[]
            {
                new CommanderFactValue(CommanderFact.GeneratorDisabled, true)
            };
            brain.Initialize(context);
            board.alertLevel = 1;
            brain.Tick(0f);
            Assert.That(brain.State, Is.EqualTo("NoPlan"));
            Assert.That(receivers[0].Count, Is.Zero);

            board.generatorDisabled = true;
            brain.Tick(0.1f);

            Assert.That(receivers[0].Last.type, Is.EqualTo(DirectiveType.SearchZone));
        }

        [Test]
        public void PlayerDeath_StopsOrdersAndRespawnAllowsNewPlan()
        {
            StartConverge();
            board.playerNeutralized = true;
            Advance(1.1f);
            Assert.That(brain.State, Is.EqualTo("PlayerNeutralized"));
            Assert.That(receivers[0].Count, Is.EqualTo(1));

            board.playerNeutralized = false;
            SeePlayer();
            brain.Tick(0.1f);

            Assert.That(receivers[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void CommanderDeath_StopsTicksAndCureNotificationsButRetainsOrders()
        {
            StartConverge();
            Directive previous = units[0].LastDirective;
            Kill(context.Health);

            Advance(2f);
            brain.NotifyCureTaken();

            Assert.That(brain.State, Is.EqualTo("Dead"));
            Assert.That(receivers[0].Count, Is.EqualTo(1));
            Assert.That(units[0].HasDirective, Is.True);
            Assert.That(units[0].LastDirective.type, Is.EqualTo(previous.type));
            Assert.That(units[0].LastDirective.position, Is.EqualTo(previous.position));
            Assert.That(board.cureTaken, Is.False);
        }

        [Test]
        public void CommanderKilledDuringDelivery_DoesNotSendTheRemainingOrders()
        {
            receivers[0].OnReceive = () => Kill(context.Health);

            StartConverge();

            Assert.That(receivers[0].Count, Is.EqualTo(1));
            Assert.That(receivers[1].Count, Is.Zero);
            Assert.That(receivers[2].Count, Is.Zero);
        }

        [Test]
        public void NoLivingSquad_DoesNotReplanEveryTickAndNoticesNewRegistration()
        {
            for (int i = 0; i < units.Count; i++) Kill(units[i].Health);
            board.alertLevel = 1;
            brain.Tick(0f);
            int version = brain.PlanVersion;
            Advance(1f);
            Assert.That(brain.State, Is.EqualTo("NoLivingSquad"));
            Assert.That(brain.PlanVersion, Is.EqualTo(version));

            AddUnit("Reinforcement", Vector3.zero);
            brain.Tick(0.1f);

            Assert.That(receivers[3].Last.type, Is.EqualTo(DirectiveType.SearchZone));
        }

        [Test]
        public void RegistrationUpdate_PreservesLastDirectiveAndRemovalInvalidatesSquad()
        {
            StartConverge();
            var replacement = new Receiver();

            BlackboardAgent updated = board.RegisterAgent(units[0].Name, units[0].Transform, units[0].Health, replacement);

            Assert.That(updated, Is.SameAs(units[0]));
            Assert.That(updated.HasDirective, Is.True);
            Assert.That(updated.LastDirective.type, Is.EqualTo(DirectiveType.Converge));
            Assert.That(board.Agents.Count, Is.EqualTo(4));
            board.RemoveAgent(units[1].Name);
            Assert.That(board.squadAssembled, Is.False);
            Advance(1.1f);
            Assert.That(replacement.Last.type, Is.EqualTo(DirectiveType.Regroup));
        }

        [Test]
        public void StableThinkTicks_DoNotAllocateOrReplan()
        {
            StartConverge();
            for (int i = 0; i < 20; i++) brain.Tick(0f);
            int version = brain.PlanVersion;
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 100; i++) brain.Tick(0f);

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(brain.PlanVersion, Is.EqualTo(version));
        }

        [Test]
        public void DebugText_ReportsLivePlanWithoutReplanningOrSendingExtraOrders()
        {
            StartConverge();
            int version = brain.PlanVersion;

            string text = brain.GetDebugText();
            brain.GetDebugText();

            Assert.That(text, Does.Contain("goal: PlayerNeutralized | state: OrderConverge"));
            Assert.That(text, Does.Contain("Plan: [OrderConverge(cost=2)] | totalCost=2 | remainingCost=2"));
            Assert.That(text, Does.Contain("agentsAlive=3"));
            Assert.That(text, Does.Contain("lastKnownPlayerPos=(20, 0, 0)"));
            Assert.That(brain.PlanVersion, Is.EqualTo(version));
            Assert.That(receivers[0].Count, Is.EqualTo(1));
            Assert.That(board.playerNeutralized, Is.False);
        }

        [Test]
        public void DebugText_ShowsDeathEvenWhenBrainTicksHaveStopped()
        {
            StartConverge();
            int version = brain.PlanVersion;
            Kill(context.Health);

            string text = brain.GetDebugText();

            Assert.That(text, Does.Contain("state: Dead"));
            Assert.That(brain.PlanVersion, Is.EqualTo(version));
            Assert.That(receivers[0].Count, Is.EqualTo(1));
        }

        private void StartConverge()
        {
            board.alertLevel = 1;
            board.squadAssembled = true;
            SeePlayer();
            brain.Tick(0f);
        }

        private void SeePlayer()
        {
            board.lastKnownPlayerTime = now;
            board.lastKnownPlayerPos = new Vector3(20f, 0f, 0f);
        }

        private void Advance(float seconds)
        {
            now += seconds;
            brain.Tick(seconds);
        }

        private GameObject MakeObject(string name)
        {
            var gameObject = new GameObject(name);
            objects.Add(gameObject);
            gameObject.AddComponent<Health>().ResetHealth(0f);
            return gameObject;
        }

        private void AddUnit(string name, Vector3 position)
        {
            GameObject gameObject = MakeObject(name);
            gameObject.transform.position = position;
            var receiver = new Receiver();
            receivers.Add(receiver);
            units.Add(board.RegisterAgent(name, gameObject.transform, gameObject.GetComponent<Health>(), receiver));
        }

        private static void Kill(Health health)
        {
            health.TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.zero, null, DamageType.Generic));
        }
    }
}
