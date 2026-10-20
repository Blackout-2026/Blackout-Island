// Owner: Systems Engineer (Hunter AI).
using System.Collections.Generic;
using System.Reflection;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>
    /// Cover, waypoints and randomness from the real world provider (docs/reference/systems-engineer.md section 15),
    /// with markers made in the test (hidden, never saved). No terrain or NavMesh is loaded, so cover comes from the
    /// CoverPoint markers and points are not snapped.
    /// </summary>
    public class HunterWorldTests
    {
        private readonly List<Object> created = new List<Object>();
        private FakeContext context;
        private HunterConfig config;

        private sealed class FakeContext : IAgentContext
        {
            public readonly List<Marker> Waypoints = new List<Marker>();
            public readonly List<Marker> Covers = new List<Marker>();
            private readonly List<Marker> none = new List<Marker>();

            public Transform Self { get; set; }
            public Health Health => null;
            public Blackboard Blackboard { get; } = new Blackboard();
            public Transform Player => null;

            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null)
            {
                if (type == MarkerType.Waypoint)
                {
                    return Waypoints.FindAll(m => owner == null || m.Owner == owner);
                }

                return type == MarkerType.CoverPoint ? Covers : none;
            }
        }

        [SetUp]
        public void SetUp()
        {
            config = Track(ScriptableObject.CreateInstance<HunterConfig>());
            var self = Track(new GameObject("Hunter-1") { hideFlags = HideFlags.HideAndDontSave });
            context = new FakeContext { Self = self.transform };
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        [Test]
        public void Behind_IsOnTheFarSideFromTheThreat()
        {
            Vector3 point = CoverMath.Behind(new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, 30f), 0.9f);

            AssertNear(new Vector3(0f, 0f, 9.1f), point);
        }

        [Test]
        public void Cover_IsTheNearestWithinTheRadius_BehindItFromTheThreat()
        {
            context.Covers.Add(MarkerAt(MarkerType.CoverPoint, "Cover_01", "", new Vector3(5f, 0f, 0f)));
            context.Covers.Add(MarkerAt(MarkerType.CoverPoint, "Cover_02", "", new Vector3(0f, 0f, 3f)));
            context.Covers.Add(MarkerAt(MarkerType.CoverPoint, "Cover_03", "", new Vector3(0f, 0f, 30f)));
            var world = new HunterWorld(context, config);

            Assert.IsTrue(world.TryFindCover(Vector3.zero, new Vector3(0f, 0f, 20f), 12f, out Vector3 point));
            AssertNear(new Vector3(0f, 0f, 2.1f), point, "Cover_02, 0.9 m on the Hunter's side");
        }

        [Test]
        public void Cover_NoneWithinTheRadius_IsNotFound()
        {
            context.Covers.Add(MarkerAt(MarkerType.CoverPoint, "Cover_01", "", new Vector3(0f, 0f, 30f)));
            var world = new HunterWorld(context, config);

            Assert.IsFalse(world.TryFindCover(Vector3.zero, new Vector3(0f, 0f, 20f), 12f, out _));
        }

        [Test]
        public void AdvanceCover_MustBeNearerTheTargetByTheGain()
        {
            var target = new Vector3(0f, 0f, 40f);
            context.Covers.Add(MarkerAt(MarkerType.CoverPoint, "Sideways", "", new Vector3(3f, 0f, 0f)));
            context.Covers.Add(MarkerAt(MarkerType.CoverPoint, "Ahead", "", new Vector3(0f, 0f, 10f)));
            context.Covers.Add(MarkerAt(MarkerType.CoverPoint, "TooFar", "", new Vector3(0f, 0f, 20f)));
            var world = new HunterWorld(context, config);

            Assert.IsTrue(world.TryFindAdvanceCover(Vector3.zero, target, 15f, 4f, out Vector3 point));
            AssertNear(new Vector3(0f, 0f, 9.1f), point, "Ahead: 10 m nearer, within 15 m; Sideways gains nothing");
        }

        [Test]
        public void Waypoints_AreThisAgentsOwn_InIdOrder()
        {
            context.Waypoints.Add(MarkerAt(MarkerType.Waypoint, "Waypoint_Hunter-1_02", "Hunter-1", new Vector3(2f, 0f, 0f)));
            context.Waypoints.Add(MarkerAt(MarkerType.Waypoint, "Waypoint_Hunter-2_01", "Hunter-2", new Vector3(9f, 0f, 0f)));
            context.Waypoints.Add(MarkerAt(MarkerType.Waypoint, "Waypoint_Hunter-1_01", "Hunter-1", new Vector3(1f, 0f, 0f)));
            var world = new HunterWorld(context, config);

            Assert.AreEqual(2, world.WaypointCount);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), world.GetWaypoint(0));
            Assert.AreEqual(new Vector3(2f, 0f, 0f), world.GetWaypoint(1));
        }

        [Test]
        public void Random_IsRepeatableForTheSameName()
        {
            var first = new HunterWorld(context, config);
            var second = new HunterWorld(context, config);

            for (int i = 0; i < 5; i++)
            {
                float value = first.Random01();
                Assert.AreEqual(value, second.Random01());
                Assert.That(value, Is.InRange(0f, 1f));
            }
        }

        private static void AssertNear(Vector3 expected, Vector3 actual, string message = null)
        {
            Assert.AreEqual(0f, Vector3.Distance(expected, actual), 1e-4f, $"{message} (got {actual})");
        }

        private Marker MarkerAt(MarkerType type, string id, string owner, Vector3 position)
        {
            var go = Track(new GameObject(id) { hideFlags = HideFlags.HideAndDontSave });
            go.transform.position = position;
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

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
