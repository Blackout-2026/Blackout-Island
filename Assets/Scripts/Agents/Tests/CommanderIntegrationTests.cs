// Owner: Agent Controller.
using System.Collections.Generic;
using Blackout.AI.Commander;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public sealed class CommanderIntegrationTests
    {
        private sealed class UnitBrain : IAgentBrain, IDirectiveReceiver, IAlertReceiver
        {
            public int Orders;
            public int Alerts;
            public Directive Last;
            public AgentAlert Alert;
            public void Initialize(IAgentContext context) { }
            public AgentIntent Tick(float deltaTime) => AgentIntent.Idle;
            public void OnDirective(Directive value) { Orders++; Last = value; }
            public void OnAlert(AgentAlert value) { Alerts++; Alert = value; }
        }

        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<AgentRoot> roots = new List<AgentRoot>();
        private readonly List<UnitBrain> units = new List<UnitBrain>();
        private AgentRegistry registry;
        private BlackboardHolder holder;
        private CommanderBrain commander;
        private Health commanderHealth;
        private CommanderConfig config;
        private VoidEventChannel cure;
        private VoidEventChannel died;
        private VoidEventChannel respawned;

        [SetUp]
        public void SetUp()
        {
            GameObject registryObject = MakeObject("G2-Registry");
            registry = registryObject.AddComponent<AgentRegistry>();
            registry.SetMarkers(System.Array.Empty<Marker>());
            holder = registryObject.GetComponent<BlackboardHolder>();
            cure = ScriptableObject.CreateInstance<VoidEventChannel>();
            died = ScriptableObject.CreateInstance<VoidEventChannel>();
            respawned = ScriptableObject.CreateInstance<VoidEventChannel>();
            holder.Bind(null, cure);
            holder.BindPlayerLifecycle(died, respawned);
            for (int i = 0; i < 3; i++) AddUnit("G2-Unit-" + i);

            config = ScriptableObject.CreateInstance<CommanderConfig>();
            GameObject commanderObject = MakeObject("G2-Commander");
            commanderHealth = commanderObject.AddComponent<Health>();
            commanderHealth.ResetHealth(0f);
            commander = commanderObject.AddComponent<CommanderBrain>();
            commander.SetConfig(config);
            commander.SetClock(() => 100f);
            var root = commanderObject.AddComponent<AgentRoot>();
            root.Configure(commander, null, null, null);
            root.Initialize(registry);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(cure);
            Object.DestroyImmediate(died);
            Object.DestroyImmediate(respawned);
            objects.Clear();
            roots.Clear();
            units.Clear();
        }

        [Test]
        public void CureChannel_SynchronouslyReplansAndDeliversToAllThreeBrains()
        {
            cure.Raise();

            Assert.That(commander.PlanVersion, Is.EqualTo(1));
            Assert.That(registry.Blackboard.cureTaken, Is.True);
            foreach (UnitBrain unit in units)
            {
                Assert.That(unit.Orders, Is.EqualTo(1));
                Assert.That(unit.Last.type, Is.EqualTo(DirectiveType.SearchZone));
                Assert.That(unit.Alerts, Is.EqualTo(1));
            }
            Assert.That(registry.Blackboard.Agents.Count, Is.EqualTo(4));
        }

        [Test]
        public void CureAlert_AppliesConfiguredAbsoluteMultipliersWithoutStacking()
        {
            config.alertSpeedMultiplier = 1.3f;
            config.alertVisionMultiplier = 1.5f;
            config.alertAimErrorMultiplier = 0.8f;

            cure.Raise();
            cure.Raise();

            foreach (AgentRoot root in roots)
            {
                Assert.That(root.GetComponent<PathFollower>().SpeedMultiplier, Is.EqualTo(1.3f));
                Assert.That(root.VisionRangeMultiplier, Is.EqualTo(1.5f));
                Assert.That(root.GetComponent<AgentShooter>().Heightened, Is.True);
                Assert.That(root.GetComponent<AgentShooter>().HeightenedAimErrorMultiplier, Is.EqualTo(0.8f));
            }
            Assert.That(units[0].Alerts, Is.EqualTo(1));
            Assert.That(units[0].Alert.visionMultiplier, Is.EqualTo(1.5f));
        }

        [Test]
        public void LateSpawn_InheritsExistingAlertAndNextDirective()
        {
            cure.Raise();

            AddUnit("G2-Reinforcement");
            commander.Tick(0.1f);

            Assert.That(units[3].Alerts, Is.EqualTo(1));
            Assert.That(units[3].Orders, Is.EqualTo(1));
            Assert.That(roots[3].GetComponent<PathFollower>().SpeedMultiplier, Is.EqualTo(1.25f));
        }

        [Test]
        public void ReplacingBrain_ReplaysLastDirectiveAndCurrentAlert()
        {
            cure.Raise();
            var replacement = new UnitBrain();

            roots[0].SetBrain(replacement);

            Assert.That(replacement.Orders, Is.EqualTo(1));
            Assert.That(replacement.Last.type, Is.EqualTo(DirectiveType.SearchZone));
            Assert.That(replacement.Alerts, Is.EqualTo(1));
            Assert.That(registry.Blackboard.Agents.Count, Is.EqualTo(4));
        }

        [Test]
        public void CommanderKilledBeforeCure_LeavesOrdersSilentButSurvivorsApplyTheWorldBuff()
        {
            Kill(commanderHealth);

            cure.Raise();

            Assert.That(registry.Blackboard.cureTaken, Is.True);
            Assert.That(registry.Blackboard.HasAlertBuff, Is.False);
            Assert.That(units[0].Orders, Is.Zero);
            Assert.That(registry.Blackboard.Agents.Count, Is.EqualTo(3));
            foreach (AgentRoot root in roots)
            {
                root.Think(0.1f);
                Assert.That(root.GetComponent<PathFollower>().SpeedMultiplier, Is.EqualTo(1.25f));
                Assert.That(root.VisionRangeMultiplier, Is.EqualTo(1.4f));
            }
        }

        [Test]
        public void CommanderKilledAfterOrder_LeavesOtherAgentsLastDirectiveIntact()
        {
            cure.Raise();
            BlackboardAgent survivor = registry.Blackboard.Agents[0];
            Directive last = survivor.LastDirective;

            Kill(commanderHealth);
            cure.Raise();
            commander.Tick(2f);

            Assert.That(units[0].Orders, Is.EqualTo(1));
            Assert.That(survivor.LastDirective.type, Is.EqualTo(last.type));
            Assert.That(survivor.LastDirective.position, Is.EqualTo(last.position));
        }

        [Test]
        public void PlayerLifecycle_UpdatesNeutralizationAndClearsStaleMemoryOnRespawn()
        {
            registry.Blackboard.lastKnownPlayerTime = 100f;
            registry.Blackboard.squadAssembled = true;
            cure.Raise();
            died.Raise();
            commander.Tick(0.1f);
            Assert.That(commander.State, Is.EqualTo("PlayerNeutralized"));
            Assert.That(registry.Blackboard.playerNeutralized, Is.True);

            respawned.Raise();
            commander.Tick(0.1f);

            Assert.That(registry.Blackboard.playerNeutralized, Is.False);
            Assert.That(registry.Blackboard.lastKnownPlayerTime, Is.EqualTo(float.NegativeInfinity));
            Assert.That(units[0].Last.type, Is.EqualTo(DirectiveType.SearchZone));
        }

        private void AddUnit(string name)
        {
            GameObject obj = MakeObject(name);
            obj.AddComponent<Health>().ResetHealth(0f);
            var mover = obj.AddComponent<PathFollower>();
            var shooter = obj.AddComponent<AgentShooter>();
            var root = obj.AddComponent<AgentRoot>();
            var brain = new UnitBrain();
            root.Configure(brain, mover, null, shooter);
            root.Initialize(registry);
            roots.Add(root);
            units.Add(brain);
        }

        private GameObject MakeObject(string name)
        {
            var obj = new GameObject(name);
            objects.Add(obj);
            return obj;
        }

        private static void Kill(Health health) => health.TakeDamage(
            new DamageInfo(1000f, Vector3.zero, Vector3.zero, null, DamageType.Generic));
    }
}
