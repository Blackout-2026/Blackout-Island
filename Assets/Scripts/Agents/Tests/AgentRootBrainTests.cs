using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class AgentRootBrainTests
    {
        private sealed class CountingBrain : IAgentBrain
        {
            public int initializeCalls;
            public int tickCalls;
            public IAgentContext context;

            public void Initialize(IAgentContext newContext)
            {
                initializeCalls++;
                context = newContext;
            }

            public AgentIntent Tick(float deltaTime)
            {
                tickCalls++;
                return AgentIntent.Idle;
            }
        }

        private GameObject agentObject;
        private GameObject registryObject;

        [TearDown]
        public void TearDown()
        {
            if (agentObject != null)
            {
                Object.DestroyImmediate(agentObject);
            }
            if (registryObject != null)
            {
                Object.DestroyImmediate(registryObject);
            }
        }

        private AgentRoot MakeAgent()
        {
            agentObject = new GameObject("Hunter-1");
            AgentRoot root = agentObject.AddComponent<AgentRoot>();
            agentObject.GetComponent<Health>().ResetHealth(0f);
            root.Configure(null, null, null, null);
            return root;
        }

        [Test]
        public void SetBrain_BeforeStart_IsThenUsedByThink()
        {
            AgentRoot root = MakeAgent();
            var brain = new CountingBrain();

            root.SetBrain(brain);
            root.Think(0.1f);

            Assert.AreEqual(1, brain.tickCalls);
            Assert.AreEqual(0, brain.initializeCalls, "not initialised until the agent has started and has a registry");
        }

        [Test]
        public void SetBrain_AfterStart_InitialisesTheBrainStraightAway()
        {
            AgentRoot root = MakeAgent();
            registryObject = new GameObject("Registry");
            AgentRegistry registry = registryObject.AddComponent<AgentRegistry>();
            root.Initialize(registry); // what Start does
            var brain = new CountingBrain();

            root.SetBrain(brain);

            Assert.AreEqual(1, brain.initializeCalls);
            Assert.AreSame(agentObject.transform, brain.context.Self);
        }

        [Test]
        public void Registry_GetMarkers_AnswersFromItsMarkerIndex()
        {
            registryObject = new GameObject("Registry");
            AgentRegistry registry = registryObject.AddComponent<AgentRegistry>();
            var markerObject = new GameObject("Waypoint_Hunter-1_01");
            try
            {
                Marker marker = markerObject.AddComponent<Marker>();
                typeof(Marker).GetField("type", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(marker, MarkerType.Waypoint);
                typeof(Marker).GetField("owner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(marker, "Hunter-1");
                registry.SetMarkers(new[] { marker });

                Assert.AreEqual(1, registry.GetMarkers(MarkerType.Waypoint, "Hunter-1").Count);
                Assert.AreEqual(0, registry.GetMarkers(MarkerType.Waypoint, "Hunter-2").Count);
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }
    }
}
