using System.Collections.Generic;
using System.Reflection;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class MarkerIndexTests
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

        // Marker has private serialized fields and no setters (World Builder owns it), so tests set them by reflection.
        private Marker MakeMarker(MarkerType type, string id, string owner)
        {
            var go = new GameObject(id);
            created.Add(go);
            Marker marker = go.AddComponent<Marker>();
            Set(marker, "type", type);
            Set(marker, "id", id);
            Set(marker, "owner", owner);
            return marker;
        }

        private static void Set(Marker marker, string field, object value)
        {
            typeof(Marker).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(marker, value);
        }

        [Test]
        public void Get_FiltersByType()
        {
            var index = new MarkerIndex(new[]
            {
                MakeMarker(MarkerType.Waypoint, "w1", "Patrol-1"),
                MakeMarker(MarkerType.CoverPoint, "c1", ""),
            });

            Assert.AreEqual(1, index.Get(MarkerType.Waypoint).Count);
            Assert.AreEqual(1, index.Get(MarkerType.CoverPoint).Count);
            Assert.AreEqual(0, index.Get(MarkerType.BoatExit).Count);
        }

        [Test]
        public void Get_WithOwner_ReturnsOnlyThatOwnersMarkers()
        {
            var index = new MarkerIndex(new[]
            {
                MakeMarker(MarkerType.Waypoint, "w1", "Patrol-1"),
                MakeMarker(MarkerType.Waypoint, "w2", "Patrol-1"),
                MakeMarker(MarkerType.Waypoint, "w3", "Patrol-2"),
            });

            Assert.AreEqual(2, index.Get(MarkerType.Waypoint, "Patrol-1").Count);
            Assert.AreEqual(1, index.Get(MarkerType.Waypoint, "Patrol-2").Count);
            Assert.AreEqual(3, index.Get(MarkerType.Waypoint).Count, "null owner means any owner");
        }

        [Test]
        public void Get_SameQueryTwice_ReturnsTheSameCachedList()
        {
            var index = new MarkerIndex(new[] { MakeMarker(MarkerType.Waypoint, "w1", "Patrol-1") });

            Assert.AreSame(index.Get(MarkerType.Waypoint, "Patrol-1"), index.Get(MarkerType.Waypoint, "Patrol-1"));
        }

        [Test]
        public void Get_SkipsMarkersDestroyedAfterTheIndexWasBuilt()
        {
            Marker doomed = MakeMarker(MarkerType.Waypoint, "w1", "Patrol-1");
            var index = new MarkerIndex(new[] { doomed });
            Object.DestroyImmediate(doomed.gameObject);

            Assert.AreEqual(0, index.Get(MarkerType.Waypoint).Count);
        }

        [Test]
        public void NullSource_GivesAnEmptyIndex()
        {
            var index = new MarkerIndex(null);

            Assert.AreEqual(0, index.Count);
            Assert.AreEqual(0, index.Get(MarkerType.Waypoint).Count);
        }
    }
}
