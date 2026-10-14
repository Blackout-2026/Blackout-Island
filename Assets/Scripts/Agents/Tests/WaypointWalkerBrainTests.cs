using System.Collections.Generic;
using System.Reflection;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class WaypointWalkerBrainTests
    {
        private sealed class FakeContext : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health => null;
            public Blackboard Blackboard => null;
            public Transform Player => null;
            public IReadOnlyList<Marker> markers = new List<Marker>();
            public string lastOwner;

            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null)
            {
                lastOwner = owner;
                return type == MarkerType.Waypoint ? markers : new List<Marker>();
            }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private GameObject brainObject;
        private WaypointWalkerBrain brain;
        private FakeContext context;

        [SetUp]
        public void SetUp()
        {
            brainObject = new GameObject("Patrol-1");
            created.Add(brainObject);
            brain = brainObject.AddComponent<WaypointWalkerBrain>();
            context = new FakeContext { Self = brainObject.transform };
            brain.Initialize(context);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                Object.DestroyImmediate(go);
            }
            created.Clear();
        }

        private Marker Waypoint(string id, Vector3 position)
        {
            var go = new GameObject(id);
            created.Add(go);
            go.transform.position = position;
            Marker marker = go.AddComponent<Marker>();
            typeof(Marker).GetField("type", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(marker, MarkerType.Waypoint);
            typeof(Marker).GetField("id", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(marker, id);
            typeof(Marker).GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(marker, "Patrol-1");
            return marker;
        }

        [Test]
        public void WithNoWaypoints_StandsStill()
        {
            AgentIntent intent = brain.Tick(0.1f);

            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
        }

        [Test]
        public void AsksForItsOwnWaypoints_ByTheAgentName()
        {
            brain.Tick(0.1f);

            Assert.AreEqual("Patrol-1", context.lastOwner);
        }

        [Test]
        public void WalksToTheFirstWaypointByIdOrder_WhateverOrderTheMarkersArriveIn()
        {
            context.markers = new List<Marker>
            {
                Waypoint("Waypoint_Patrol-1_02", new Vector3(20f, 0f, 0f)),
                Waypoint("Waypoint_Patrol-1_01", new Vector3(10f, 0f, 0f)),
            };

            AgentIntent intent = brain.Tick(0.1f);

            Assert.AreEqual(MoveSpeed.Walk, intent.moveSpeed);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), intent.moveTarget);
        }

        [Test]
        public void PausesAtAWaypoint_ThenHeadsForTheNext()
        {
            context.markers = new List<Marker>
            {
                Waypoint("Waypoint_Patrol-1_01", new Vector3(10f, 0f, 0f)),
                Waypoint("Waypoint_Patrol-1_02", new Vector3(20f, 0f, 0f)),
            };
            brainObject.transform.position = new Vector3(10f, 0f, 0f); // standing on the first waypoint

            Assert.AreEqual(MoveSpeed.Stop, brain.Tick(0.1f).moveSpeed, "arrives: starts the pause");
            Assert.AreEqual(MoveSpeed.Stop, brain.Tick(1.0f).moveSpeed, "still pausing (1.1 s of 2.2 s)");
            Assert.AreEqual(MoveSpeed.Stop, brain.Tick(1.5f).moveSpeed, "the pause ends this tick");

            AgentIntent next = brain.Tick(0.1f);
            Assert.AreEqual(MoveSpeed.Walk, next.moveSpeed);
            Assert.AreEqual(new Vector3(20f, 0f, 0f), next.moveTarget);
        }

        [Test]
        public void LoopsBackToTheFirstWaypointAfterTheLast()
        {
            context.markers = new List<Marker>
            {
                Waypoint("Waypoint_Patrol-1_01", new Vector3(10f, 0f, 0f)),
                Waypoint("Waypoint_Patrol-1_02", new Vector3(20f, 0f, 0f)),
            };

            brainObject.transform.position = new Vector3(10f, 0f, 0f);
            brain.Tick(0.1f); brain.Tick(3f); // pause at the first, move on
            brainObject.transform.position = new Vector3(20f, 0f, 0f);
            brain.Tick(0.1f); brain.Tick(3f); // pause at the last, move on

            AgentIntent intent = brain.Tick(0.1f);
            Assert.AreEqual(new Vector3(10f, 0f, 0f), intent.moveTarget);
        }

        [Test]
        public void IgnoresHeight_WhenCheckingForArrival()
        {
            context.markers = new List<Marker> { Waypoint("Waypoint_Patrol-1_01", new Vector3(10f, 0f, 0f)) };
            brainObject.transform.position = new Vector3(10f, 5f, 0f);

            Assert.AreEqual(MoveSpeed.Stop, brain.Tick(0.1f).moveSpeed);
        }

        [Test]
        public void PicksUpWaypointsThatLoadAfterTheAgent()
        {
            Assert.AreEqual(MoveSpeed.Stop, brain.Tick(0.1f).moveSpeed);

            context.markers = new List<Marker> { Waypoint("Waypoint_Patrol-1_01", new Vector3(10f, 0f, 0f)) };

            Assert.AreEqual(MoveSpeed.Walk, brain.Tick(0.1f).moveSpeed);
        }

        [Test]
        public void DebugText_NamesTheWaypointItIsHeadingFor()
        {
            Assert.AreEqual("WaypointWalker (no waypoints)", brain.GetDebugText());

            context.markers = new List<Marker> { Waypoint("Waypoint_Patrol-1_01", new Vector3(10f, 0f, 0f)) };
            brain.Tick(0.1f);

            Assert.AreEqual("WaypointWalker -> Waypoint_Patrol-1_01", brain.GetDebugText());
        }
    }
}
