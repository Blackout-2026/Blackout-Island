// Owner: World Builder (Security Patrol AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.AI.SecurityPatrol.Tests
{
    public sealed class SecurityPatrolStateBehaviourTests
    {
        private sealed class Context : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health => null;
            public Blackboard Blackboard { get; } = new Blackboard();
            public Transform Player => null;
            public IReadOnlyList<Marker> Markers = Array.Empty<Marker>();
            public string RequestedOwner;

            // Deliberately return mixed owners: the brain must still reject another agent's route.
            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null)
            {
                RequestedOwner = owner;
                return Markers;
            }
        }

        private GameObject host;
        private GameObject markerRoot;
        private SecurityPatrolBrain brain;
        private Context context;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Patrol-1");
            markerRoot = new GameObject("Test waypoints");
            brain = host.AddComponent<SecurityPatrolBrain>();
            context = new Context { Self = host.transform };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(markerRoot);
        }

        [Test]
        public void Patrol_FiltersOwnersAndTypesAndWalksInOrdinalIdOrder()
        {
            Marker second = MakeMarker("Waypoint_Patrol-1_02", "Patrol-1", Vector3.right * 6f);
            Marker foreign = MakeMarker("Waypoint_Patrol-0_01", "Patrol-0", Vector3.left * 6f);
            Marker first = MakeMarker("Waypoint_Patrol-1_01", "Patrol-1", Vector3.forward * 6f);
            Marker cover = MakeMarker("CoverPoint_01", "Patrol-1", Vector3.back * 6f, MarkerType.CoverPoint);
            context.Markers = new[] { second, foreign, cover, null, first };
            brain.Initialize(context);

            AgentIntent intent = brain.Tick(0.1f, default);

            Assert.That(context.RequestedOwner, Is.EqualTo("Patrol-1"));
            Assert.That(intent.moveTarget, Is.EqualTo(first.transform.position));
            Assert.That(intent.moveSpeed, Is.EqualTo(MoveSpeed.Walk));
            Assert.That(intent.aim || intent.fire, Is.False);
            Assert.That(host.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(host.transform.rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void Patrol_PausesForReferenceTimeThenAdvancesAndLoops()
        {
            Marker first = MakeMarker("Waypoint_Patrol-1_01", "Patrol-1", Vector3.zero);
            Marker second = MakeMarker("Waypoint_Patrol-1_02", "Patrol-1", Vector3.forward * 6f);
            context.Markers = new[] { first, second };
            brain.Initialize(context);

            AgentIntent pause = brain.Tick(0f, default);
            Assert.That(pause.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(pause.hasLookTarget, Is.True);
            Assert.That(brain.Tick(2.1f, default).moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            AgentIntent walk = brain.Tick(0.11f, default);
            Assert.That(walk.moveTarget, Is.EqualTo(second.transform.position));
            Assert.That(walk.moveSpeed, Is.EqualTo(MoveSpeed.Walk));

            host.transform.position = second.transform.position;
            brain.Tick(0f, default);
            AgentIntent loop = brain.Tick(2.21f, default);
            Assert.That(loop.moveTarget, Is.EqualTo(first.transform.position));
            Assert.That(loop.moveSpeed, Is.EqualTo(MoveSpeed.Walk));
        }

        [Test]
        public void Patrol_UsesMarkersLoadedAfterInitialization()
        {
            brain.Initialize(context);
            Assert.That(brain.Tick(0.1f, default).moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Marker point = MakeMarker("Waypoint_Patrol-1_01", "Patrol-1", Vector3.forward * 6f);
            context.Markers = new[] { point };

            Assert.That(brain.Tick(0.1f, default).moveTarget, Is.EqualTo(point.transform.position));
        }

        [Test]
        public void HeardNoise_EntersInvestigationAndCopiesFakePerceptionTarget()
        {
            brain.Initialize(context);
            var input = new PatrolPerceptionInput { heardNoise = true, noisePosition = Vector3.right * 8f };

            AgentIntent intent = brain.Tick(0.1f, input);
            input.noisePosition = Vector3.left * 8f;

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
            Assert.That(intent.moveTarget, Is.EqualTo(Vector3.right * 8f));
            Assert.That(intent.moveSpeed, Is.EqualTo(MoveSpeed.Walk));
            Assert.That(intent.fire, Is.False);
            Assert.That(brain.Tick(0.1f, default).moveTarget, Is.EqualTo(Vector3.right * 8f));
            Assert.That(host.transform.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Investigation_OnlyCountsLookTimeAfterReachingTheNoise()
        {
            Vector3 target = Vector3.forward * 6f;
            brain.Initialize(context);
            brain.Tick(0f, new PatrolPerceptionInput { heardNoise = true, noisePosition = target });
            Assert.That(brain.Tick(10f, default).moveSpeed, Is.EqualTo(MoveSpeed.Walk));
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));

            host.transform.position = target;
            AgentIntent look = brain.Tick(0.5f, default);
            Assert.That(look.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(look.hasLookTarget, Is.True);
            Assert.That(look.aim || look.fire, Is.False);
            brain.Tick(2.5f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
            brain.Tick(0.5f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [Test]
        public void Investigation_ResetsLookTimeIfItLeavesTheArrivalArea()
        {
            brain.Initialize(context);
            brain.Tick(0f, new PatrolPerceptionInput { heardNoise = true, noisePosition = Vector3.zero });
            brain.Tick(3f, default);
            host.transform.position = Vector3.right * 5f;
            brain.Tick(0.5f, default);
            host.transform.position = Vector3.zero;
            brain.Tick(0.5f, default);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Investigate));
            brain.Tick(3f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
        }

        [Test]
        public void Search_GoesToLastKnownPositionThenLooksAroundWhileAiming()
        {
            Vector3 target = Vector3.forward * 6f;
            StartSearch(target);
            AgentIntent travel = brain.Tick(0.5f, default);
            Assert.That(travel.moveTarget, Is.EqualTo(target));
            Assert.That(travel.moveSpeed, Is.EqualTo(MoveSpeed.Walk));
            Assert.That(travel.aim, Is.True);

            host.transform.position = target;
            AgentIntent firstLook = brain.Tick(0.5f, default);
            AgentIntent secondLook = brain.Tick(0.5f, default);
            Assert.That(firstLook.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(firstLook.hasLookTarget && firstLook.aim, Is.True);
            Assert.That(firstLook.lookTarget, Is.Not.EqualTo(secondLook.lookTarget));
            Assert.That(firstLook.fire || secondLook.fire, Is.False);
        }

        [Test]
        public void Search_ExpiresAfterFourteenSecondsEvenIfStillTravelling()
        {
            StartSearch(Vector3.forward * 6f);
            brain.Tick(13.5f, default);
            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Search));

            AgentIntent intent = brain.Tick(0.5f, default);

            Assert.That(brain.CurrentState, Is.EqualTo(SecurityPatrolState.Patrol));
            Assert.That(intent.aim || intent.fire, Is.False);
        }

        [Test]
        public void Reinitialize_ResetsRouteProgressAndPause()
        {
            Marker first = MakeMarker("Waypoint_Patrol-1_01", "Patrol-1", Vector3.zero);
            Marker second = MakeMarker("Waypoint_Patrol-1_02", "Patrol-1", Vector3.forward * 6f);
            context.Markers = new[] { first, second };
            brain.Initialize(context);
            brain.Tick(0f, default);
            brain.Tick(2.21f, default);
            brain.Initialize(context);

            Assert.That(brain.Tick(0f, default).moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(brain.Tick(2.21f, default).moveTarget, Is.EqualTo(second.transform.position));
        }

        [Test]
        public void WarmRouteTicksAndNoiseTransitions_DoNotAllocateManagedMemory()
        {
            Marker point = MakeMarker("Waypoint_Patrol-1_01", "Patrol-1", Vector3.forward * 6f);
            context.Markers = new[] { point };
            brain.Initialize(context);
            for (int i = 0; i < 8; i++) NoiseCycle();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) NoiseCycle();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaTime_IsRejected(float deltaTime)
        {
            brain.Initialize(context);
            Assert.Throws<ArgumentOutOfRangeException>(() => brain.Tick(deltaTime, default));
        }

        private void NoiseCycle()
        {
            brain.Tick(0f, new PatrolPerceptionInput { heardNoise = true, noisePosition = host.transform.position });
            brain.Tick(3.5f, default);
            brain.Tick(0.1f, default);
        }

        private void StartSearch(Vector3 target)
        {
            brain.Initialize(context);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.DetectedPlayer, target), Is.True);
            brain.Tick(0f, default);
            Assert.That(brain.TryRequestTransition(SecurityPatrolTrigger.LostPlayer, target), Is.True);
            brain.Tick(0f, default);
        }

        private Marker MakeMarker(string id, string owner, Vector3 position, MarkerType type = MarkerType.Waypoint)
        {
            var markerObject = new GameObject(id);
            markerObject.transform.SetParent(markerRoot.transform);
            markerObject.transform.position = position;
            Marker marker = markerObject.AddComponent<Marker>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("owner").stringValue = owner;
            serialized.FindProperty("type").enumValueIndex = (int)type;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }
    }
}
