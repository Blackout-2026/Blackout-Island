// Owner: World Builder (Security Patrol AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.AI.SecurityPatrol.Tests
{
    public sealed class SecurityPatrolBrainTests
    {
        private sealed class Context : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health { get; set; }
            public Blackboard Blackboard { get; } = new Blackboard();
            public Transform Player { get; set; }
            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null) => Array.Empty<Marker>();
        }

        private GameObject host;
        private GameObject self;
        private SecurityPatrolBrain brain;
        private Context context;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Patrol brain host");
            self = new GameObject("Patrol-1");
            self.transform.position = new Vector3(3f, 2.5f, 74f);
            brain = host.AddComponent<SecurityPatrolBrain>();
            context = new Context { Self = self.transform };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            if (self != null)
                Object.DestroyImmediate(self);
        }

        [Test]
        public void BeforeInitialization_ReturnsIdleAndRejectsRequests()
        {
            Assert.That(brain.IsInitialized, Is.False);
            Assert.That(brain.CurrentState, Is.Null);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.CanAttack, Vector3.one), Is.False);
            AssertIdle(brain.Tick(0.1f));
        }

        [Test]
        public void Initialize_RejectsMissingContextOrSelf()
        {
            Assert.Throws<ArgumentNullException>(() => brain.Initialize(null));
            Assert.Throws<ArgumentException>(() => brain.Initialize(new Context()));
            Assert.That(brain.IsInitialized, Is.False);
        }

        [Test]
        public void InitialPatrol_WaitsForLaterPerceptionAndRouteBehaviour()
        {
            context.Player = host.transform;
            context.Blackboard.alertLevel = 1;
            context.Blackboard.lastKnownPlayerPos = host.transform.position;
            brain.Initialize(context);

            for (int i = 0; i < 20; i++)
                AssertIdle(brain.Tick(0.1f));

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [Test]
        public void InvestigationPursuitAndSearch_ReturnIntentsWithoutExecutingMovement()
        {
            brain.Initialize(context);
            Vector3 position = self.transform.position;
            Quaternion rotation = self.transform.rotation;
            Vector3 noise = new Vector3(6f, 2.5f, 76f);
            Vector3 sighting = new Vector3(8f, 2.5f, 80f);

            AgentIntent investigate = RequestAndTick(SecurityPatrolTrigger.HeardNoise, noise);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
            Assert.That(investigate.moveTarget, Is.EqualTo(noise));
            Assert.That(investigate.moveSpeed, Is.EqualTo(MoveSpeed.Walk));
            Assert.That(investigate.aim, Is.False);
            Assert.That(investigate.fire, Is.False);

            AgentIntent pursue = RequestAndTick(SecurityPatrolTrigger.DetectedPlayer, sighting);
            Assert.That(pursue.moveTarget, Is.EqualTo(sighting));
            Assert.That(pursue.moveSpeed, Is.EqualTo(MoveSpeed.Run));

            AgentIntent search = RequestAndTick(SecurityPatrolTrigger.LostPlayer, sighting);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Search));
            Assert.That(search.moveTarget, Is.EqualTo(sighting));
            Assert.That(search.moveSpeed, Is.EqualTo(MoveSpeed.Walk));
            Assert.That(search.aim, Is.True);
            Assert.That(search.fire, Is.False);

            AssertIdle(RequestAndTick(SecurityPatrolTrigger.SearchComplete, position));
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            Assert.That(self.transform.position, Is.EqualTo(position));
            Assert.That(self.transform.rotation, Is.EqualTo(rotation));
        }

        [Test]
        public void Attack_ChangesAtTickBoundaryAndCannotFireWithoutConfirmedSight()
        {
            brain.Initialize(context);
            Vector3 target = new Vector3(5f, 3.5f, 78f);

            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.CanAttack, target), Is.True);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            AgentIntent attack = brain.Tick(0.1f);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
            Assert.That(attack.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(attack.hasLookTarget, Is.True);
            Assert.That(attack.lookTarget, Is.EqualTo(target + Vector3.up * (1.8f * 0.62f)));
            Assert.That(attack.aim, Is.True);
            Assert.That(attack.fire, Is.False);

            AgentIntent pursue = RequestAndTick(SecurityPatrolTrigger.CannotAttack, target);
            Assert.That(pursue.moveSpeed, Is.EqualTo(MoveSpeed.Run));
            Assert.That(pursue.aim, Is.False);
            Assert.That(pursue.hasLookTarget, Is.False);
            Assert.That(pursue.fire, Is.False);
        }

        [Test]
        public void InvalidRequest_DoesNotPreventALegalRequestInTheSameTick()
        {
            brain.Initialize(context);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.LostPlayer, Vector3.one), Is.False);
            Assert.That(brain.TryRequestTransition((SecurityPatrolTrigger)999, Vector3.one), Is.False);

            RequestAndTick(SecurityPatrolTrigger.HeardNoise, Vector3.one);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
        }

        [Test]
        public void CompletedInvestigation_ClearsItsMovementIntent()
        {
            brain.Initialize(context);
            RequestAndTick(SecurityPatrolTrigger.HeardNoise, Vector3.one);

            AssertIdle(RequestAndTick(SecurityPatrolTrigger.InvestigationComplete, Vector3.zero));

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            AgentIntent next = RequestAndTick(SecurityPatrolTrigger.HeardNoise, Vector3.forward);
            Assert.That(next.moveTarget, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void PendingRequest_FirstAcceptedTargetWinsUntilTick()
        {
            brain.Initialize(context);
            Vector3 first = new Vector3(5f, 2.5f, 75f);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.HeardNoise, first), Is.True);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.CanAttack, Vector3.one), Is.False);

            AgentIntent intent = brain.Tick(0.1f);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
            Assert.That(intent.moveTarget, Is.EqualTo(first));
        }

        [Test]
        public void Reinitialize_ClearsPendingRequestsAndOldAimFlags()
        {
            brain.Initialize(context);
            RequestAndTick(SecurityPatrolTrigger.CanAttack, Vector3.one);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.CannotAttack, Vector3.one), Is.True);

            brain.Initialize(context);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            AssertIdle(brain.Tick(0.1f));
            AgentIntent intent = RequestAndTick(SecurityPatrolTrigger.HeardNoise, Vector3.forward);
            Assert.That(intent.moveTarget, Is.EqualTo(Vector3.forward));
            Assert.That(intent.aim, Is.False);
            Assert.That(intent.hasLookTarget, Is.False);
        }

        [Test]
        public void DestroyedSelf_ReturnsIdleInsteadOfReadingADestroyedTransform()
        {
            brain.Initialize(context);
            RequestAndTick(SecurityPatrolTrigger.CanAttack, Vector3.one);
            Object.DestroyImmediate(self);

            AssertIdle(brain.Tick(0.1f));
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.CannotAttack, Vector3.one), Is.False);
        }

        [Test]
        public void DeadAgent_ReturnsIdleDespiteAPendingMovementRequest()
        {
            context.Health = self.AddComponent<Health>();
            context.Health.ResetHealth(0f);
            brain.Initialize(context);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.DetectedPlayer, Vector3.one), Is.True);
            brain.OnDirective(new Directive(DirectiveType.Regroup, Vector3.one, 10f));
            context.Health.TakeDamage(new DamageInfo(context.Health.Max, Vector3.zero,
                Vector3.forward, null, DamageType.Generic));

            AssertIdle(brain.Tick(0.1f));
            Assert.That(brain.HasDirective, Is.False);
            brain.OnDirective(new Directive(DirectiveType.Converge, Vector3.one, 0f));
            Assert.That(brain.HasDirective, Is.False);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [Test]
        public void WarmTicksAndTransitions_DoNotAllocateManagedMemory()
        {
            brain.Initialize(context);
            for (int i = 0; i < 8; i++)
                RunCycle();

            long before = GC.GetAllocatedBytesForCurrentThread();
            int accepted = 0;
            for (int i = 0; i < 128; i++)
                accepted += RunCycle();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(accepted, Is.EqualTo(128 * 9));
            Assert.That(allocated, Is.Zero);
        }

        [TestCase(DirectiveType.Converge, SecurityPatrolState.Pursue, MoveSpeed.Run)]
        [TestCase(DirectiveType.SearchZone, SecurityPatrolState.Search, MoveSpeed.Walk)]
        [TestCase(DirectiveType.Regroup, SecurityPatrolState.Investigate, MoveSpeed.Walk)]
        public void SquadOrder_UsesTheExistingFsm_WithoutMovingOrFiring(DirectiveType type, SecurityPatrolState state, MoveSpeed speed)
        {
            brain.Initialize(context);
            Vector3 start = self.transform.position;
            Vector3 target = start + Vector3.forward * 10f;
            brain.OnDirective(new Directive(type, target, 9f));
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol), "receiving must not tick the FSM");
            AgentIntent intent = brain.Tick(0f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(state));
            Assert.That(intent.moveTarget, Is.EqualTo(target));
            Assert.That(intent.moveSpeed, Is.EqualTo(speed));
            Assert.That(intent.fire, Is.False);
            Assert.That(self.transform.position, Is.EqualTo(start));
        }

        [Test]
        public void RepeatedSearch_DoesNotResetTimeout_AndMovedTargetsReplaceTheOrder()
        {
            brain.Initialize(context);
            var search = new Directive(DirectiveType.SearchZone, PositionAhead(50f), 9f);
            brain.OnDirective(search); brain.Tick(0f, default);
            for (int i = 0; i < 13; i++) { brain.OnDirective(search); brain.Tick(1f, default); }
            brain.Tick(1f, default);
            Assert.That(brain.HasDirective, Is.False);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            brain.OnDirective(new Directive(DirectiveType.Converge, PositionAhead(10f), 0f));
            brain.Tick(0f, default);
            Vector3 moved = PositionAhead(20f);
            brain.OnDirective(new Directive(DirectiveType.Converge, moved, 0f));
            brain.OnDirective(new Directive((DirectiveType)999, Vector3.zero, 0f));
            Assert.That(brain.Tick(0f, default).moveTarget, Is.EqualTo(moved));
        }

        [Test]
        public void SearchOrder_VisitsPointsWithinItsRadius()
        {
            brain.Initialize(context);
            Vector3 centre = self.transform.position;
            brain.OnDirective(new Directive(DirectiveType.SearchZone, centre, 5f));
            brain.Tick(0f, default);
            AgentIntent intent = brain.Tick(1.6f, default);
            Assert.That(Vector3.Distance(intent.moveTarget, centre), Is.EqualTo(3f).Within(.001f));
            Assert.That(intent.fire, Is.False);
        }

        [Test]
        public void CombatInterruptsAnOrder_ThenTheRetainedOrderResumes_AndReinitializeClearsIt()
        {
            brain.Initialize(context);
            Vector3 rally = PositionAhead(30f);
            brain.OnDirective(new Directive(DirectiveType.Regroup, rally, 10f));
            brain.Tick(0f, default);
            AgentIntent combat = brain.Tick(.1f, new PatrolPerceptionInput { canSeePlayer = true, playerPosition = PositionAhead(5f) });
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
            Assert.That(combat.fire, Is.True);
            brain.Tick(3f, default); // Attack -> local Pursue after lost sight.
            AgentIntent resumed = brain.Tick(.1f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
            Assert.That(resumed.moveTarget, Is.EqualTo(rally));
            Assert.That(resumed.fire, Is.False);
            brain.enabled = false; brain.enabled = true;
            Assert.That(brain.Tick(.1f, default).moveTarget, Is.EqualTo(rally));
            brain.Initialize(context);
            Assert.That(brain.HasDirective, Is.False);
        }

        private Vector3 PositionAhead(float distance) => self.transform.position + Vector3.forward * distance;

        private AgentIntent RequestAndTick(SecurityPatrolTrigger trigger, Vector3 target)
        {
            Assert.That(brain.TryRequestTransition(trigger, target), Is.True);
            return brain.Tick(0.1f);
        }

        private int RunCycle()
        {
            int accepted = Drive(SecurityPatrolTrigger.HeardNoise);
            accepted += Drive(SecurityPatrolTrigger.DetectedPlayer);
            accepted += Drive(SecurityPatrolTrigger.CanAttack);
            accepted += Drive(SecurityPatrolTrigger.CannotAttack);
            accepted += Drive(SecurityPatrolTrigger.LostPlayer);
            accepted += Drive(SecurityPatrolTrigger.CanAttack);
            accepted += Drive(SecurityPatrolTrigger.CannotAttack);
            // Return through Search to Patrol for the next cycle.
            accepted += Drive(SecurityPatrolTrigger.LostPlayer);
            accepted += Drive(SecurityPatrolTrigger.SearchComplete);
            return accepted;
        }

        private int Drive(SecurityPatrolTrigger trigger)
        {
            bool accepted = brain.TryRequestTransition(trigger, Vector3.one);
            brain.Tick(0.1f);
            return accepted ? 1 : 0;
        }

        private static void AssertIdle(AgentIntent intent)
        {
            Assert.That(intent.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(intent.hasLookTarget, Is.False);
            Assert.That(intent.aim, Is.False);
            Assert.That(intent.fire, Is.False);
        }
    }
}
