using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class AgentRegistryTests
    {
        private sealed class FakeAgent : IAgentInfo
        {
            public FakeAgent(string name) { AgentName = name; }
            public string AgentName { get; }
            public Transform Transform => null;
            public bool IsAlive => true;
        }

        private GameObject gameObject;
        private AgentRegistry registry;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("RegistryTest");
            registry = gameObject.AddComponent<AgentRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Register_AddsTheAgentAndPublishesAnInitialState()
        {
            var agent = new FakeAgent("Patrol-1");

            registry.Register(agent);

            Assert.AreEqual(1, registry.Agents.Count);
            Assert.IsTrue(registry.Blackboard.AgentStates.ContainsKey("Patrol-1"));
        }

        [Test]
        public void Register_SameAgentTwice_IsIgnored()
        {
            var agent = new FakeAgent("Patrol-1");

            registry.Register(agent);
            registry.Register(agent);

            Assert.AreEqual(1, registry.Agents.Count);
        }

        [Test]
        public void SetState_IsVisibleOnTheBlackboard()
        {
            var agent = new FakeAgent("Hunter-1");
            registry.Register(agent);

            registry.SetState(agent, "Ambush");

            Assert.AreEqual("Ambush", registry.Blackboard.AgentStates["Hunter-1"]);
        }

        [Test]
        public void Unregister_RemovesTheAgentAndItsState()
        {
            var agent = new FakeAgent("Hunter-1");
            registry.Register(agent);

            registry.Unregister(agent);

            Assert.AreEqual(0, registry.Agents.Count);
            Assert.IsFalse(registry.Blackboard.AgentStates.ContainsKey("Hunter-1"));
        }

        [Test]
        public void CreateContext_SharesTheRegistrysBlackboard()
        {
            var selfObject = new GameObject("Agent");
            try
            {
                IAgentContext context = registry.CreateContext(selfObject.transform, null);

                Assert.AreSame(registry.Blackboard, context.Blackboard);
                Assert.AreSame(selfObject.transform, context.Self);
            }
            finally
            {
                Object.DestroyImmediate(selfObject);
            }
        }
    }
}
