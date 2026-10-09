using System.Collections.Generic;
using System.Reflection;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class AgentContextTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                Object.DestroyImmediate(go);
            }
            created.Clear();
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private static void Set(Marker marker, string field, object value)
        {
            typeof(Marker).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(marker, value);
        }

        [Test]
        public void GetMarkers_ComesFromTheMarkerSource()
        {
            Marker marker = Make("w1").AddComponent<Marker>();
            Set(marker, "type", MarkerType.Waypoint);
            Set(marker, "owner", "Patrol-1");
            var index = new MarkerIndex(new[] { marker });
            var context = new AgentContext(null, null, new Blackboard(), () => null, () => index);

            Assert.AreEqual(1, context.GetMarkers(MarkerType.Waypoint, "Patrol-1").Count);
            Assert.AreEqual(0, context.GetMarkers(MarkerType.Waypoint, "Patrol-2").Count);
        }

        [Test]
        public void Player_IsLookedUpEachTime_SoALatePlayerIsFound()
        {
            Transform player = null;
            var context = new AgentContext(null, null, new Blackboard(), () => player, () => new MarkerIndex(null));

            Assert.IsNull(context.Player);
            player = Make("Player").transform;
            Assert.AreSame(player, context.Player);
        }

        [Test]
        public void ExposesWhatItWasGiven()
        {
            Transform self = Make("Agent").transform;
            var blackboard = new Blackboard();
            var context = new AgentContext(self, null, blackboard, null, () => new MarkerIndex(null));

            Assert.AreSame(self, context.Self);
            Assert.AreSame(blackboard, context.Blackboard);
            Assert.IsNull(context.Player, "no player source means no player");
        }
    }
}
