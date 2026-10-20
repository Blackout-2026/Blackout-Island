// Owner: World Builder (Security Patrol AI).
using System;
using System.Collections.Generic;
using System.Reflection;
using Blackout.Agents;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.AI.SecurityPatrol.Tests
{
    public sealed class SecurityPatrolDebugTests
    {
        private sealed class Context : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health { get; set; }
            public Blackboard Blackboard { get; } = new Blackboard();
            public Transform Player => null;
            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null) => Array.Empty<Marker>();
        }

        private GameObject host;
        private SecurityPatrolBrain brain;
        private Context context;
        private static readonly Vector3 Target = Vector3.forward * 10f;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Patrol-1");
            Health health = host.AddComponent<Health>();
            health.ResetHealth(0f);
            brain = host.AddComponent<SecurityPatrolBrain>();
            context = new Context { Self = host.transform, Health = health };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void SharedContract_ReportsUninitializedAndInitialPatrol()
        {
            IDebugInfo info = brain;
            Assert.That(info.GetDebugText(), Does.Contain("[FSM] Uninitialized"));
            Assert.That(info.GetDebugText(), Does.Contain("last transition: none"));

            brain.Initialize(context);

            Assert.That(info.GetDebugText(), Does.Contain("[FSM] Patrol"));
            Assert.That(info.GetDebugText(), Does.Contain("last transition: Initialized"));
        }

        [TestCase(SecurityPatrolState.Patrol, SecurityPatrolTrigger.HeardNoise, SecurityPatrolState.Investigate)]
        [TestCase(SecurityPatrolState.Patrol, SecurityPatrolTrigger.DetectedPlayer, SecurityPatrolState.Pursue)]
        [TestCase(SecurityPatrolState.Patrol, SecurityPatrolTrigger.CanAttack, SecurityPatrolState.Attack)]
        [TestCase(SecurityPatrolState.Investigate, SecurityPatrolTrigger.DetectedPlayer, SecurityPatrolState.Pursue)]
        [TestCase(SecurityPatrolState.Investigate, SecurityPatrolTrigger.CanAttack, SecurityPatrolState.Attack)]
        [TestCase(SecurityPatrolState.Investigate, SecurityPatrolTrigger.InvestigationComplete, SecurityPatrolState.Patrol)]
        [TestCase(SecurityPatrolState.Investigate, SecurityPatrolTrigger.InvestigationAlerted, SecurityPatrolState.Search)]
        [TestCase(SecurityPatrolState.Pursue, SecurityPatrolTrigger.CanAttack, SecurityPatrolState.Attack)]
        [TestCase(SecurityPatrolState.Pursue, SecurityPatrolTrigger.LostPlayer, SecurityPatrolState.Search)]
        [TestCase(SecurityPatrolState.Attack, SecurityPatrolTrigger.CannotAttack, SecurityPatrolState.Pursue)]
        [TestCase(SecurityPatrolState.Search, SecurityPatrolTrigger.DetectedPlayer, SecurityPatrolState.Pursue)]
        [TestCase(SecurityPatrolState.Search, SecurityPatrolTrigger.CanAttack, SecurityPatrolState.Attack)]
        [TestCase(SecurityPatrolState.Search, SecurityPatrolTrigger.SearchComplete, SecurityPatrolState.Patrol)]
        public void AppliedTransition_ReportsItsActualStateAndTrigger(SecurityPatrolState from,
            SecurityPatrolTrigger trigger, SecurityPatrolState to)
        {
            brain.Initialize(context);
            if (from == SecurityPatrolState.Investigate) Drive(SecurityPatrolTrigger.HeardNoise);
            else if (from == SecurityPatrolState.Attack) Drive(SecurityPatrolTrigger.CanAttack);
            else if (from == SecurityPatrolState.Pursue || from == SecurityPatrolState.Search)
            {
                Drive(SecurityPatrolTrigger.DetectedPlayer);
                if (from == SecurityPatrolState.Search) Drive(SecurityPatrolTrigger.LostPlayer);
            }
            string before = brain.GetDebugText();

            Assert.That(brain.TryRequestTransition(trigger, Target), Is.True);
            Assert.That(brain.GetDebugText(), Is.SameAs(before), "A queued request is not yet a transition.");
            brain.Tick(0f, default);

            Assert.That(brain.CurrentState, Is.EqualTo(to));
            Assert.That(brain.GetDebugText(), Is.EqualTo(
                $"Security Patrol [FSM] {to} | last transition: {from} -> {to} ({trigger})"));
        }

        [Test]
        public void RejectedRequestsAndUnchangedTicks_PreserveTheLastAppliedReason()
        {
            brain.Initialize(context);
            string initial = brain.GetDebugText();
            Assert.That(brain.TryRequestTransition((SecurityPatrolTrigger)999, Target), Is.False);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.HeardNoise, Target), Is.True);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.CanAttack, Target), Is.False);
            Assert.That(brain.GetDebugText(), Is.SameAs(initial));
            brain.Tick(0f, default);
            string applied = brain.GetDebugText();

            brain.Tick(0.1f, default);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.LostPlayer, Target), Is.False);

            Assert.That(brain.GetDebugText(), Is.SameAs(applied));
            Assert.That(applied, Does.Contain("(HeardNoise)"));
        }

        [Test]
        public void AutomaticSightLossAndSearchTimeout_UpdateTheReason()
        {
            brain.Initialize(context);
            brain.Tick(0f, new PatrolPerceptionInput { canSeePlayer = true, playerPosition = Target });
            Assert.That(brain.GetDebugText(), Does.Contain("[FSM] Attack"));
            Assert.That(brain.GetDebugText(), Does.Contain("(CanAttack)"));
            brain.Tick(1f, default);
            Assert.That(brain.GetDebugText(), Does.Contain("(CannotAttack)"));
            brain.Tick(4f, default);
            Assert.That(brain.GetDebugText(), Does.Contain("(LostPlayer)"));
            brain.Tick(14f, default);
            Assert.That(brain.GetDebugText(), Does.Contain("[FSM] Patrol"));
            Assert.That(brain.GetDebugText(), Does.Contain("(SearchComplete)"));
        }

        [Test]
        public void Reinitialize_DiscardsThePendingReason()
        {
            brain.Initialize(context);
            Drive(SecurityPatrolTrigger.CanAttack);
            brain.TryRequestTransition(SecurityPatrolTrigger.CannotAttack, Target);

            brain.Initialize(context);
            brain.Tick(0f, default);

            Assert.That(brain.GetDebugText(), Does.Contain("last transition: Initialized"));
        }

        [Test]
        public void PlayerRespawn_ReportsTheMemoryResetInsteadOfTheOldAttack()
        {
            VoidEventChannel respawn = ScriptableObject.CreateInstance<VoidEventChannel>();
            try
            {
                typeof(SecurityPatrolBrain).GetField("playerRespawned", BindingFlags.Instance |
                    BindingFlags.NonPublic).SetValue(brain, respawn);
                brain.Initialize(context);
                Drive(SecurityPatrolTrigger.CanAttack);

                respawn.Raise();

                Assert.That(brain.GetDebugText(), Does.Contain("[FSM] Patrol"));
                Assert.That(brain.GetDebugText(), Does.Contain("Player memory reset"));
            }
            finally { Object.DestroyImmediate(respawn); }
        }

        [Test]
        public void Death_ReportsTheResetEvenWithoutAnotherThinkTick()
        {
            brain.Initialize(context);
            Drive(SecurityPatrolTrigger.CanAttack);

            context.Health.TakeDamage(new DamageInfo(context.Health.Max, Vector3.zero,
                Vector3.forward, null, DamageType.Generic));

            Assert.That(brain.GetDebugText(), Does.Contain("[FSM] Patrol"));
            Assert.That(brain.GetDebugText(), Does.Contain("Agent died; memory cleared"));
        }

        [Test]
        public void Disable_ReportsTheResetAndClearsAPendingReason()
        {
            brain.Initialize(context);
            brain.TryRequestTransition(SecurityPatrolTrigger.CanAttack, Target);
            // EditMode does not guarantee live MonoBehaviour callback ordering.
            typeof(SecurityPatrolBrain).GetMethod("OnDisable", BindingFlags.Instance |
                BindingFlags.NonPublic).Invoke(brain, null);

            brain.Tick(0f, default);

            Assert.That(brain.GetDebugText(), Does.Contain("[FSM] Patrol"));
            Assert.That(brain.GetDebugText(), Does.Contain("Disabled; memory cleared"));
        }

        [Test]
        public void SharedHost_PublishesTheDebugTextToItsBlackboard()
        {
            GameObject services = new GameObject("Debug test services");
            try
            {
                AgentRegistry registry = services.AddComponent<AgentRegistry>();
                registry.SetMarkers(Array.Empty<Marker>());
                AgentRoot root = host.AddComponent<AgentRoot>();
                root.Configure(brain, null, null, null);
                root.Initialize(registry);
                Drive(SecurityPatrolTrigger.HeardNoise);

                typeof(AgentRoot).GetMethod("PublishState", BindingFlags.Instance |
                    BindingFlags.NonPublic).Invoke(root, null);

                Assert.That(registry.Blackboard.AgentStates[root.AgentName], Is.SameAs(brain.GetDebugText()));
            }
            finally { Object.DestroyImmediate(services); }
        }

        [Test]
        public void WarmTransitionsAndOverlayReads_AllocateNoManagedMemory()
        {
            brain.Initialize(context);
            for (int i = 0; i < 8; i++) RunCycle();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int accepted = 0;
            for (int i = 0; i < 128; i++) accepted += RunCycle();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(accepted, Is.EqualTo(128 * 6));
            Assert.That(allocated, Is.Zero);
        }

        private int RunCycle() => Drive(SecurityPatrolTrigger.HeardNoise) +
            Drive(SecurityPatrolTrigger.DetectedPlayer) + Drive(SecurityPatrolTrigger.CanAttack) +
            Drive(SecurityPatrolTrigger.CannotAttack) + Drive(SecurityPatrolTrigger.LostPlayer) +
            Drive(SecurityPatrolTrigger.SearchComplete);

        private int Drive(SecurityPatrolTrigger trigger)
        {
            bool accepted = brain.TryRequestTransition(trigger, Target);
            brain.Tick(0f, default);
            // Include real contract reads in the allocation measurement.
            IDebugInfo info = brain;
            return accepted && !string.IsNullOrEmpty(info.GetDebugText()) ? 1 : 0;
        }
    }
}
