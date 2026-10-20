// Owner: World Builder (Security Patrol AI).
using System;
using System.Collections.Generic;
using Blackout.Agents;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Blackout.AI.SecurityPatrol.Tests
{
    public sealed class SecurityPatrolCombatTests
    {
        private sealed class Context : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health => null;
            public Blackboard Blackboard { get; } = new Blackboard();
            public Transform Player => null;
            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null) => Array.Empty<Marker>();
        }

        private GameObject host;
        private GameObject target;
        private SecurityPatrolBrain brain;
        private SecurityPatrolConfig config;
        private Context context;
        private Random.State savedRandomState;
        private DamageInfo lastDamage;

        [SetUp]
        public void SetUp()
        {
            savedRandomState = Random.state;
            Random.InitState(17);
            host = new GameObject("Patrol-1");
            brain = host.AddComponent<SecurityPatrolBrain>();
            config = ScriptableObject.CreateInstance<SecurityPatrolConfig>();
            var serialized = new SerializedObject(brain);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            context = new Context { Self = host.transform };
            brain.Initialize(context);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            if (target != null) Object.DestroyImmediate(target);
            Object.DestroyImmediate(config);
            Random.state = savedRandomState;
        }

        [TestCase(SecurityPatrolState.Patrol)]
        [TestCase(SecurityPatrolState.Investigate)]
        [TestCase(SecurityPatrolState.Search)]
        public void VisiblePlayerOutsideAttackRange_EntersPursue(SecurityPatrolState initial)
        {
            EnterState(initial);

            AgentIntent intent = brain.Tick(0f, Sight(30f));

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Pursue));
            Assert.That(intent.moveSpeed, Is.EqualTo(MoveSpeed.Run));
            Assert.That(intent.moveTarget, Is.EqualTo(Vector3.forward * 30f));
            Assert.That(intent.fire, Is.False);
            Assert.That(host.transform.position, Is.EqualTo(Vector3.zero));
        }

        [TestCase(SecurityPatrolState.Patrol)]
        [TestCase(SecurityPatrolState.Investigate)]
        [TestCase(SecurityPatrolState.Pursue)]
        [TestCase(SecurityPatrolState.Search)]
        public void VisiblePlayerInRange_EntersAttackAndRequestsFire(SecurityPatrolState initial)
        {
            EnterState(initial);

            AgentIntent intent = brain.Tick(0f, Sight(10f));

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
            Assert.That(intent.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(intent.hasLookTarget && intent.aim && intent.fire, Is.True);
            Assert.That(intent.lookTarget, Is.EqualTo(Vector3.forward * 10f + Vector3.up * config.aimHeight));
            Assert.That(host.transform.rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void Sight_OutranksANoiseAndASearchTimeout()
        {
            var input = Sight(10f);
            input.heardNoise = true;
            input.noisePosition = Vector3.left * 20f;
            brain.Tick(0f, input);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));

            brain.Initialize(context);
            EnterState(SecurityPatrolState.Search);
            brain.Tick(config.searchTime, input);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
        }

        [Test]
        public void Pursue_TracksVisiblePositionsAndRetainsTheLastConfirmedPosition()
        {
            brain.Tick(0f, Sight(30f));
            AgentIntent visible = brain.Tick(0.1f, Sight(32f));
            var hidden = new PatrolPerceptionInput
            {
                canSeePlayer = false,
                playerPosition = Vector3.right * 100f,
                heardNoise = true,
                noisePosition = Vector3.left * 100f
            };

            AgentIntent lost = brain.Tick(0.5f, hidden);

            Assert.That(visible.moveTarget, Is.EqualTo(Vector3.forward * 32f));
            Assert.That(lost.moveTarget, Is.EqualTo(visible.moveTarget));
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Pursue));
        }

        [Test]
        public void Pursue_LostSightAtLastKnownPosition_EntersSearch()
        {
            brain.Tick(0f, Sight(30f));
            host.transform.position = Vector3.forward * 30f;

            AgentIntent search = brain.Tick(0.1f, default);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Search));
            Assert.That(search.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(search.hasLookTarget && search.aim, Is.True);
            Assert.That(search.fire, Is.False);
        }

        [Test]
        public void Pursue_LostForMoreThanTwoPointFiveSecondsAndPursuingForFour_EntersSearch()
        {
            brain.Tick(0f, Sight(30f));
            brain.Tick(2.5f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Pursue));
            brain.Tick(0.5f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Pursue));

            AgentIntent search = brain.Tick(1f, default);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Search));
            Assert.That(search.moveTarget, Is.EqualTo(Vector3.forward * 30f));
            Assert.That(search.moveSpeed, Is.EqualTo(MoveSpeed.Walk));
        }

        [Test]
        public void Attack_LosingSightStopsFireImmediatelyThenPursuesAfterOneSecond()
        {
            brain.Tick(0f, Sight(10f));

            AgentIntent grace = brain.Tick(0.75f, default);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
            Assert.That(grace.fire, Is.False);
            AgentIntent pursue = brain.Tick(0.25f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Pursue));
            Assert.That(pursue.moveTarget, Is.EqualTo(Vector3.forward * 10f));
            Assert.That(pursue.moveSpeed, Is.EqualTo(MoveSpeed.Run));
            Assert.That(pursue.fire, Is.False);
        }

        [Test]
        public void Attack_RetargetsVisiblePlayerAndUsesConfiguredChestHeight()
        {
            config.aimHeight = 1.2f;
            brain.Tick(0f, Sight(10f));

            AgentIntent intent = brain.Tick(0.1f, Sight(12f));

            Assert.That(intent.lookTarget, Is.EqualTo(Vector3.forward * 12f + Vector3.up * 1.2f));
            Assert.That(intent.fire, Is.True);
        }

        [Test]
        public void Attack_RangeHysteresisPreventsStateFlickerButFireStaysWithinAttackRange()
        {
            brain.Tick(0f, Sight(21f));
            AgentIntent hold = brain.Tick(0.1f, Sight(23f));
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
            Assert.That(hold.fire, Is.False);

            AgentIntent pursue = brain.Tick(0.1f, Sight(25.31f));
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Pursue));
            Assert.That(pursue.fire, Is.False);
            brain.Tick(0.1f, Sight(21f));
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
        }

        [Test]
        public void Attack_UsesConfiguredRangeIncludingItsBoundary()
        {
            config.attackRange = 8f;
            brain.Tick(0f, Sight(9f));
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Pursue));

            Assert.That(brain.Tick(0f, Sight(8f)).fire, Is.True);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
        }

        [Test]
        public void Reinitialize_ForgetsThePreviousPlayerPosition()
        {
            brain.Tick(0f, Sight(30f));
            brain.Initialize(context);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.DetectedPlayer, Vector3.forward * 50f), Is.True);
            brain.Tick(0f, default);

            AgentIntent search = brain.Tick(4f, default);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Search));
            Assert.That(search.moveTarget, Is.EqualTo(Vector3.forward * 50f));
        }

        [Test]
        public void AttackIntent_ExistingShooterAppliesOneReactionDelayAndResetsItOnLostSight()
        {
            AgentShooter shooter = MakeShooter();
            AgentIntent fire = brain.Tick(0f, Sight(10f));
            Assert.That(fire.fire, Is.True, "The intent requests engagement; only the shooter delays the shot.");
            shooter.Apply(fire);
            shooter.Tick(0.59f);
            Assert.That(shooter.ShotsFired, Is.Zero);
            shooter.Tick(0.02f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(1));

            shooter.Apply(brain.Tick(0.5f, default));
            shooter.Tick(1f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(1));
            shooter.Apply(brain.Tick(0f, Sight(10f)));
            shooter.Tick(0.59f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(1));
            shooter.Tick(0.02f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(2));
        }

        [Test]
        public void AttackIntent_ExistingShooterPerturbsTheShotWithReferenceAimError()
        {
            // Isolate the real ray from level geometry and restore Unity's random state in TearDown.
            host.transform.position = Vector3.one * 1000f;
            AgentShooter shooter = MakeShooter(1 << PhysicsLayers.Player);
            target = new GameObject("Test player") { layer = PhysicsLayers.Player };
            target.transform.position = host.transform.position + Vector3.forward * 10f;
            var collider = target.AddComponent<BoxCollider>();
            collider.center = Vector3.up * config.aimHeight;
            collider.size = new Vector3(10f, 10f, 0.5f);
            Health health = target.AddComponent<Health>();
            health.ResetHealth(0f);
            health.OnDamaged += CaptureDamage;
            Physics.SyncTransforms();
            var input = new PatrolPerceptionInput { canSeePlayer = true, playerPosition = target.transform.position };
            AgentIntent fire = brain.Tick(0f, input);
            Vector3 perfectDirection = (fire.lookTarget - (host.transform.position + Vector3.up * 1.62f)).normalized;

            shooter.Apply(fire);
            shooter.Tick(config.reactionDelay + 0.01f);

            Assert.That(shooter.Config.spread, Is.EqualTo(0.034f));
            Assert.That(shooter.ShotsFired, Is.EqualTo(1));
            Assert.That(health.Current, Is.EqualTo(health.Max - shooter.Config.damage));
            Assert.That(lastDamage.type, Is.EqualTo(DamageType.Bullet));
            Assert.That(Vector3.Angle(perfectDirection, lastDamage.direction), Is.GreaterThan(0.001f));
        }

        [Test]
        public void WarmAutomaticCombatCycles_DoNotAllocateManagedMemory()
        {
            for (int i = 0; i < 8; i++) CombatCycle();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 128; i++) CombatCycle();

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        private void EnterState(SecurityPatrolState state)
        {
            if (state == SecurityPatrolState.Investigate)
                brain.Tick(0f, new PatrolPerceptionInput { heardNoise = true, noisePosition = Vector3.left * 6f });
            else if (state == SecurityPatrolState.Pursue || state == SecurityPatrolState.Search)
            {
                brain.Tick(0f, Sight(30f));
                if (state == SecurityPatrolState.Search) brain.Tick(4f, default);
            }
            Assert.That(brain.CurrentState, Is.EqualTo(state));
        }

        private void CombatCycle()
        {
            brain.Tick(0f, Sight(30f));
            brain.Tick(0f, Sight(10f));
            brain.Tick(1f, default);
            brain.Tick(4f, default);
            brain.Tick(14f, default);
        }

        private static PatrolPerceptionInput Sight(float distance) =>
            new PatrolPerceptionInput { canSeePlayer = true, playerPosition = Vector3.forward * distance };

        private AgentShooter MakeShooter(int hitMask = 0)
        {
            AgentShooter shooter = host.AddComponent<AgentShooter>();
            shooter.Configure(new AgentWeaponConfig
            {
                reactionDelay = config.reactionDelay,
                spread = config.aimError,
                range = 34f
            }, newHitMask: hitMask);
            return shooter;
        }

        private void CaptureDamage(DamageInfo damage) => lastDamage = damage;
    }
}
