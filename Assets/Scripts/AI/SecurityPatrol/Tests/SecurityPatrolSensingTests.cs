// Owner: World Builder (Security Patrol AI).
using System;
using System.Collections.Generic;
using System.Reflection;
using Blackout.Agents;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.AI.SecurityPatrol.Tests
{
    public sealed class SecurityPatrolSensingTests
    {
        private sealed class Context : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health { get; set; }
            public Blackboard Blackboard { get; } = new Blackboard();
            public Transform Player { get; set; }
            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null) => Array.Empty<Marker>();
        }

        private sealed class SightQuery : IPerceptionRaycast
        {
            public bool Blocked;
            public Vector3 Origin;
            public Vector3 Target;
            public bool IsBlocked(Vector3 origin, Vector3 target)
            {
                Origin = origin;
                Target = target;
                return Blocked;
            }
        }

        private readonly List<GameObject> objects = new List<GameObject>();
        private GameObject host;
        private GameObject player;
        private SecurityPatrolBrain brain;
        private SecurityPatrolConfig config;
        private NoiseEventChannel noise;
        private VoidEventChannel respawned;
        private Context context;
        private SightQuery query;

        [SetUp]
        public void SetUp()
        {
            host = MakeObject("Patrol-1");
            Health health = host.AddComponent<Health>();
            health.ResetHealth(0f);
            player = MakeObject("Test player");
            player.AddComponent<Health>().ResetHealth(0f);
            player.transform.position = Vector3.forward * 10f;
            config = ScriptableObject.CreateInstance<SecurityPatrolConfig>();
            noise = ScriptableObject.CreateInstance<NoiseEventChannel>();
            respawned = ScriptableObject.CreateInstance<VoidEventChannel>();
            brain = host.AddComponent<SecurityPatrolBrain>();
            SetChannels(noise);
            context = new Context { Self = host.transform, Health = health, Player = player.transform };
            query = new SightQuery();
            brain.Initialize(context, query);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(noise);
            Object.DestroyImmediate(respawned);
        }

        [Test]
        public void RuntimeTick_UsesEyePointsButPublishesFeetAndAimsAtTheChest()
        {
            AgentIntent intent = brain.Tick(0.1f);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
            Assert.That(intent.fire && intent.aim, Is.True);
            Assert.That(query.Origin, Is.EqualTo(host.transform.position + Vector3.up * config.eyeHeight));
            Assert.That(query.Target, Is.EqualTo(player.transform.position + Vector3.up * config.playerSightHeight));
            Assert.That(intent.lookTarget, Is.EqualTo(player.transform.position + Vector3.up * config.aimHeight));
            Assert.That(context.Blackboard.lastKnownPlayerPos, Is.EqualTo(player.transform.position));
            Assert.That(context.Blackboard.lastKnownPlayerTime, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void RuntimeSightLoss_StopsFireAndReachesPursueThenSearch()
        {
            query.Blocked = true;
            Assert.That(brain.Tick(0f).fire, Is.False);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            query.Blocked = false;
            Assert.That(brain.Tick(0f).fire, Is.True);
            query.Blocked = true;

            Assert.That(brain.Tick(0.5f).fire, Is.False);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
            brain.Tick(0.5f);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Pursue));
            AgentIntent search = brain.Tick(4f);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Search));
            Assert.That(search.moveTarget, Is.EqualTo(player.transform.position));
        }

        [Test]
        public void NoiseChannel_InvestigatesOnceAndDoesNotReplayAConsumedNoise()
        {
            context.Player = null;
            Vector3 position = Vector3.right * 6f;
            RaiseNoise(position);

            AgentIntent investigate = brain.Tick(0f);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
            Assert.That(investigate.moveTarget, Is.EqualTo(position));
            host.transform.position = position;
            brain.Tick(3.5f);
            brain.Tick(0.1f);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [Test]
        public void Hearing_UsesTheMovedOwnerPositionBetweenThinkTicks()
        {
            context.Player = null;
            brain.Tick(0f);
            host.transform.position = Vector3.right * 50f;
            Vector3 position = Vector3.right * 55f;

            RaiseNoise(position);
            AgentIntent investigate = brain.Tick(0f);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
            Assert.That(investigate.moveTarget, Is.EqualTo(position));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Hearing_IgnoresTheAgentAndItsChildren(bool childSource)
        {
            context.Player = null;
            GameObject source = host;
            if (childSource)
            {
                source = MakeObject("Agent weapon");
                source.transform.SetParent(host.transform, false);
            }

            noise.Raise(new NoiseEvent(host.transform.position, 40f, NoiseType.Gunshot, source));
            brain.Tick(0f);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [Test]
        public void SeenPlayer_WinsOverNoiseAndTheNoiseDoesNotReturnAfterSearch()
        {
            RaiseNoise(Vector3.left * 6f);
            Assert.That(brain.Tick(0f).fire, Is.True);
            query.Blocked = true;
            brain.Tick(1f);
            brain.Tick(4f);
            brain.Tick(config.searchTime);
            brain.Tick(0.1f);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WorldFlags_ExtendBothVisionAndAttackRangeToThirtyPointEightMetres(bool cure)
        {
            player.transform.position = Vector3.forward * 25f;
            Assert.That(brain.Tick(0f).fire, Is.False);
            context.Blackboard.generatorDisabled = !cure;
            context.Blackboard.cureTaken = cure;

            Assert.That(brain.Tick(0f).fire, Is.True);
            Assert.That(brain.VisionRangeMultiplier, Is.EqualTo(1.4f));
            for (int i = 0; i < 20; i++) brain.Tick(0.1f);
            Assert.That(brain.VisionRangeMultiplier, Is.EqualTo(1.4f));
            Assert.That(config.visionRange, Is.EqualTo(22f));
            player.transform.position = Vector3.forward * 30.81f;
            Assert.That(brain.Tick(0.1f).fire, Is.False, "The alert must not stack into a second range multiplier.");
        }

        [Test]
        public void Heightening_ExtendsRangeWithoutChangingTheConeAngle()
        {
            context.Blackboard.generatorDisabled = true;
            player.transform.position = Vector3.right * 25f;

            Assert.That(brain.Tick(0f).fire, Is.False);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [Test]
        public void AlertReceiverAndConfiguredBroadcast_UseAbsoluteVisionValues()
        {
            player.transform.position = Vector3.forward * 25f;
            brain.OnAlert(new AgentAlert(1.25f, 1.4f, 0.9f));
            Assert.That(brain.Tick(0f).fire, Is.True);
            context.Blackboard.generatorDisabled = true;
            context.Blackboard.BroadcastAlert(new AgentAlert(1.3f, 1.5f, 0.8f));
            player.transform.position = Vector3.forward * 32f;

            Assert.That(brain.Tick(0.1f).fire, Is.True);
            Assert.That(brain.VisionRangeMultiplier, Is.EqualTo(1.5f));
        }

        [TestCase(false, false, 0, 2.5f)]
        [TestCase(true, false, 0, 2.5f)]
        [TestCase(false, true, 0, 5f)]
        [TestCase(false, false, 1, 5f)]
        public void Search_UsesCalmOrAlertConfigThenGivesUp(bool generator, bool cure, int alertLevel, float duration)
        {
            config.searchTime = 2.5f;
            config.alertSearchTime = 5f;
            SetFlags(generator, cure, alertLevel);
            brain.Tick(0f, new PatrolPerceptionInput { canSeePlayer = true, playerPosition = Vector3.forward * 40f });
            brain.Tick(4f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Search));

            brain.Tick(duration - 0.5f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Search));
            brain.Tick(0.5f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [TestCase(false, false, 0, SecurityPatrolState.Patrol)]
        [TestCase(true, false, 0, SecurityPatrolState.Patrol)]
        [TestCase(false, true, 0, SecurityPatrolState.Search)]
        [TestCase(false, false, 1, SecurityPatrolState.Search)]
        public void CompletedInvestigation_SearchesOnlyDuringAnAlarm(bool generator, bool cure, int alertLevel,
            SecurityPatrolState expected)
        {
            context.Player = null;
            SetFlags(generator, cure, alertLevel);
            RaiseNoise(host.transform.position);
            brain.Tick(0f);

            brain.Tick(3.5f);

            Assert.That(brain.CurrentState, Is.EqualTo(expected));
        }

        [Test]
        public void RespawnChannel_ClearsNoisePendingTransitionsAndPlayerMemoryButKeepsHeightening()
        {
            context.Blackboard.generatorDisabled = true;
            brain.Tick(0f);
            query.Blocked = true;
            RaiseNoise(Vector3.left * 6f);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.CannotAttack, Vector3.forward * 10f), Is.True);

            respawned.Raise();

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            Assert.That(brain.Tick(0.1f).fire, Is.False);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            Assert.That(brain.VisionRangeMultiplier, Is.EqualTo(1.4f));
        }

        [Test]
        public void OwnDeath_UnsubscribesAndReinitializeStartsWithoutDeadTimeNoises()
        {
            context.Player = null;
            context.Health.TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null, DamageType.Generic));
            RaiseNoise(Vector3.left * 6f);
            Assert.That(brain.Tick(0.1f).fire, Is.False);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.CanAttack, Vector3.forward), Is.False);
            context.Health.ResetHealth(0f);

            brain.Initialize(context, query);
            brain.Tick(0f);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [Test]
        public void DisableAndEnable_DiscardDisabledTimeNoisesAndResumeListening()
        {
            context.Player = null;
            brain.enabled = false;
            // EditMode does not run ordinary MonoBehaviour callbacks; invoke the managed callbacks directly.
            InvokeLifecycle("OnDisable");
            RaiseNoise(Vector3.left * 6f);
            Assert.That(brain.Tick(0f).fire, Is.False);

            brain.enabled = true;
            InvokeLifecycle("OnEnable");
            brain.Tick(0f);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            RaiseNoise(Vector3.right * 6f);
            brain.Tick(0f);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlayerDeathOrNeutralization_ClearsAttackImmediately(bool useBlackboard)
        {
            Assert.That(brain.Tick(0f).fire, Is.True);
            if (useBlackboard) context.Blackboard.playerNeutralized = true;
            else player.GetComponent<Health>().TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null, DamageType.Generic));

            AgentIntent intent = brain.Tick(0.1f);

            Assert.That(intent.fire, Is.False);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [Test]
        public void ReplacingThePlayer_RefreshesTheCachedHealthAndDiscardsOldPursuit()
        {
            brain.Tick(0f);
            query.Blocked = true;
            GameObject replacement = MakeObject("Replacement player");
            replacement.AddComponent<Health>().ResetHealth(0f);
            replacement.transform.position = Vector3.forward * 15f;
            context.Player = replacement.transform;

            brain.Tick(0.1f);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            query.Blocked = false;
            Assert.That(brain.Tick(0.1f).lookTarget,
                Is.EqualTo(replacement.transform.position + Vector3.up * config.aimHeight));
        }

        [Test]
        public void Reinitialize_UnsubscribesThePreviousNoiseChannel()
        {
            context.Player = null;
            var nextNoise = ScriptableObject.CreateInstance<NoiseEventChannel>();
            try
            {
                SetChannels(nextNoise);
                brain.Initialize(context, query);
                RaiseNoise(Vector3.left * 6f);
                brain.Tick(0f);
                Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
                nextNoise.Raise(new NoiseEvent(Vector3.right * 6f, 10f, NoiseType.Footstep, null));
                brain.Tick(0f);
                Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
            }
            finally { Object.DestroyImmediate(nextNoise); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AgentRootFlagSync_HeightensTheExecutorAndActualPatrolSensingTogether(bool cure)
        {
            var registry = MakeObject("Sensing test registry").AddComponent<AgentRegistry>();
            registry.SetMarkers(Array.Empty<Marker>());
            var registryFields = new SerializedObject(registry);
            registryFields.FindProperty("player").objectReferenceValue = player.transform;
            registryFields.ApplyModifiedPropertiesWithoutUndo();
            var mover = host.AddComponent<PathFollower>();
            var shooter = host.AddComponent<AgentShooter>();
            var root = host.AddComponent<AgentRoot>();
            root.SetKind(AgentKind.SecurityPatrol);
            root.Configure(brain, mover, null, shooter);
            root.Initialize(registry);
            brain.Initialize(registry.CreateContext(host.transform, context.Health), query);
            player.transform.position = Vector3.forward * 25f;
            registry.Blackboard.generatorDisabled = !cure;
            registry.Blackboard.cureTaken = cure;

            root.Think(0.1f);

            Assert.That(root.LastIntent.fire, Is.True);
            Assert.That(mover.SpeedMultiplier, Is.EqualTo(1.25f));
            Assert.That(root.VisionRangeMultiplier, Is.EqualTo(1.4f));
            Assert.That(brain.VisionRangeMultiplier, Is.EqualTo(1.4f));
            Assert.That(shooter.HeightenedAimErrorMultiplier, Is.EqualTo(0.9f));
            Assert.That(shooter.Heightened, Is.True);
        }

        [Test]
        public void WarmRuntimeSensingAndNoiseTicks_DoNotAllocateManagedMemory()
        {
            for (int i = 0; i < 8; i++) SensingTick();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 128; i++) SensingTick();

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Attack));
        }

        private void SensingTick()
        {
            RaiseNoise(Vector3.right * 6f);
            brain.Tick(0.1f);
        }

        private void InvokeLifecycle(string method) => typeof(SecurityPatrolBrain)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(brain, null);

        private void RaiseNoise(Vector3 position) => noise.Raise(new NoiseEvent(position, 10f, NoiseType.Footstep, null));

        private void SetChannels(NoiseEventChannel newNoise)
        {
            var fields = new SerializedObject(brain);
            fields.FindProperty("config").objectReferenceValue = config;
            fields.FindProperty("noiseChannel").objectReferenceValue = newNoise;
            fields.FindProperty("playerRespawned").objectReferenceValue = respawned;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetFlags(bool generator, bool cure, int alertLevel)
        {
            context.Blackboard.generatorDisabled = generator;
            context.Blackboard.cureTaken = cure;
            context.Blackboard.alertLevel = alertLevel;
        }

        private GameObject MakeObject(string name)
        {
            var obj = new GameObject(name);
            objects.Add(obj);
            return obj;
        }
    }
}
