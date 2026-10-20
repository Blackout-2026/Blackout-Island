// Owner: Agent Controller. S3 changes approved by the owner through this session's user.
using System;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Agents.Tests
{
    public sealed class AgentWorldAlertTests
    {
        private sealed class Brain : IAgentBrain, IAlertReceiver
        {
            public int Alerts;
            public AgentAlert Alert;
            public void Initialize(IAgentContext context) { }
            public AgentIntent Tick(float deltaTime) => AgentIntent.Idle;
            public void OnAlert(AgentAlert value) { Alerts++; Alert = value; }
        }

        private readonly List<GameObject> objects = new List<GameObject>();
        private AgentRegistry registry;
        private Brain brain;

        [SetUp]
        public void SetUp()
        {
            registry = MakeObject("Test registry").AddComponent<AgentRegistry>();
            registry.SetMarkers(Array.Empty<Marker>());
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [TestCase(AgentKind.SecurityPatrol, true)]
        [TestCase(AgentKind.TacticalGuard, true)]
        [TestCase(AgentKind.Hunter, false)]
        [TestCase(AgentKind.Commander, false)]
        public void GeneratorFlag_OnlyHeightensPatrolsAndGuards(AgentKind kind, bool heightened)
        {
            AgentRoot root = MakeAgent(kind);
            registry.Blackboard.generatorDisabled = true;

            root.Think(0.1f);

            AssertAlert(root, heightened ? 1.25f : 1f, heightened ? 1.4f : 1f, heightened ? 0.9f : 1f);
            Assert.That(brain.Alerts, Is.EqualTo(heightened ? 1 : 0));
            Assert.That(registry.Blackboard.HasAlertBuff, Is.False, "A local generator buff must not alert every agent.");
            Assert.That(registry.Blackboard.alertLevel, Is.Zero);
        }

        [TestCase(AgentKind.SecurityPatrol)]
        [TestCase(AgentKind.TacticalGuard)]
        [TestCase(AgentKind.Hunter)]
        [TestCase(AgentKind.Commander)]
        public void CureFlag_HeightensEveryLivingKindWithoutACommander(AgentKind kind)
        {
            AgentRoot root = MakeAgent(kind);
            registry.Blackboard.cureTaken = true;

            root.Think(0.1f);

            AssertAlert(root, 1.25f, 1.4f, 0.9f);
            Assert.That(brain.Alerts, Is.EqualTo(1));
            Assert.That(registry.Blackboard.HasAlertBuff, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LateInitialization_InheritsExistingWorldFlags(bool cure)
        {
            registry.Blackboard.cureTaken = cure;
            registry.Blackboard.generatorDisabled = !cure;

            AgentRoot root = MakeAgent(AgentKind.SecurityPatrol);

            AssertAlert(root, 1.25f, 1.4f, 0.9f);
            Assert.That(brain.Alerts, Is.EqualTo(1));
        }

        [Test]
        public void GeneratorThenCureThenBroadcast_AssignsTheSameBuffOnce()
        {
            AgentRoot root = MakeAgent(AgentKind.SecurityPatrol);
            registry.Blackboard.generatorDisabled = true;
            root.Think(0.1f);
            registry.Blackboard.cureTaken = true;
            root.Think(0.1f);
            registry.Blackboard.BroadcastAlert(new AgentAlert(1.25f, 1.4f, 0.9f));
            root.Think(0.1f);

            AssertAlert(root, 1.25f, 1.4f, 0.9f);
            Assert.That(brain.Alerts, Is.EqualTo(1));
        }

        [Test]
        public void ConfiguredBroadcast_ReplacesFallbackAndRetainsItsAbsoluteValues()
        {
            AgentRoot root = MakeAgent(AgentKind.SecurityPatrol);
            registry.Blackboard.generatorDisabled = true;
            root.Think(0.1f);

            registry.Blackboard.BroadcastAlert(new AgentAlert(1.3f, 1.5f, 0.8f));
            registry.Blackboard.cureTaken = true;
            root.Think(0.1f);
            root.Think(0.1f);

            AssertAlert(root, 1.3f, 1.5f, 0.8f);
            Assert.That(brain.Alerts, Is.EqualTo(2));
            Assert.That(brain.Alert.visionMultiplier, Is.EqualTo(1.5f));
        }

        [Test]
        public void ReplacingBrain_ReplaysTheLocalWorldAlert()
        {
            AgentRoot root = MakeAgent(AgentKind.SecurityPatrol);
            registry.Blackboard.generatorDisabled = true;
            root.Think(0.1f);
            var replacement = new Brain();

            root.SetBrain(replacement);
            root.Think(0.1f);

            Assert.That(replacement.Alerts, Is.EqualTo(1));
            Assert.That(replacement.Alert.visionMultiplier, Is.EqualTo(1.4f));
        }

        [Test]
        public void DeadAgent_DoesNotReceiveWorldBuffs()
        {
            AgentRoot root = MakeAgent(AgentKind.SecurityPatrol);
            root.GetComponent<Health>().TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null, DamageType.Generic));
            registry.Blackboard.cureTaken = true;

            root.Think(0.1f);

            AssertAlert(root, 1f, 1f, 1f);
            Assert.That(brain.Alerts, Is.Zero);
        }

        [Test]
        public void DisabledAgent_WaitsUntilItIsEnabledToApplyWorldBuffs()
        {
            AgentRoot root = MakeAgent(AgentKind.SecurityPatrol);
            root.enabled = false;
            registry.Blackboard.generatorDisabled = true;
            root.Think(0.1f);
            Assert.That(brain.Alerts, Is.Zero);

            root.enabled = true;
            root.Think(0.1f);

            AssertAlert(root, 1.25f, 1.4f, 0.9f);
            Assert.That(brain.Alerts, Is.EqualTo(1));
        }

        [Test]
        public void WarmAlertedThinkTicks_DoNotAllocateOrRepeatBrainNotifications()
        {
            AgentRoot root = MakeAgent(AgentKind.SecurityPatrol);
            registry.Blackboard.generatorDisabled = true;
            for (int i = 0; i < 8; i++) root.Think(0.1f);
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 128; i++) root.Think(0.1f);

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(brain.Alerts, Is.EqualTo(1));
        }

        private AgentRoot MakeAgent(AgentKind kind)
        {
            GameObject unit = MakeObject("Unit with arbitrary name");
            unit.AddComponent<Health>().ResetHealth(0f);
            var mover = unit.AddComponent<PathFollower>();
            var shooter = unit.AddComponent<AgentShooter>();
            var root = unit.AddComponent<AgentRoot>();
            brain = new Brain();
            root.SetKind(kind);
            root.Configure(brain, mover, null, shooter);
            root.Initialize(registry);
            return root;
        }

        private static void AssertAlert(AgentRoot root, float speed, float vision, float error)
        {
            Assert.That(root.GetComponent<PathFollower>().SpeedMultiplier, Is.EqualTo(speed));
            Assert.That(root.VisionRangeMultiplier, Is.EqualTo(vision));
            AgentShooter shooter = root.GetComponent<AgentShooter>();
            Assert.That(shooter.Heightened, Is.EqualTo(error != 1f));
            Assert.That(shooter.Heightened ? shooter.HeightenedAimErrorMultiplier : 1f, Is.EqualTo(error));
        }

        private GameObject MakeObject(string name)
        {
            var obj = new GameObject(name);
            objects.Add(obj);
            return obj;
        }
    }
}
