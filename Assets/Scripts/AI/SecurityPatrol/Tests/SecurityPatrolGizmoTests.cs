// Owner: World Builder (Security Patrol AI).
using System;
using System.Collections.Generic;
using Blackout.Editor;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.AI.SecurityPatrol.Tests
{
    public sealed class SecurityPatrolGizmoTests
    {
        private sealed class Context : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health => null;
            public Blackboard Blackboard { get; } = new Blackboard();
            public Transform Player => null;
            public readonly List<Marker> Markers = new List<Marker>();
            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null) => Markers;
        }

        private sealed class ClearSight : IPerceptionRaycast
        {
            public bool IsBlocked(Vector3 origin, Vector3 target) => false;
        }

        private readonly List<GameObject> objects = new List<GameObject>();
        private SecurityPatrolBrain brain;
        private SecurityPatrolConfig config;
        private Context context;

        [SetUp]
        public void SetUp()
        {
            GameObject host = MakeObject("S4 gizmo brain host");
            GameObject self = MakeObject("S4 gizmo patrol");
            self.transform.position = new Vector3(8f, 2.5f, 30f);
            self.transform.rotation = Quaternion.Euler(20f, 90f, 0f);
            brain = host.AddComponent<SecurityPatrolBrain>();
            config = ScriptableObject.CreateInstance<SecurityPatrolConfig>();
            var fields = new SerializedObject(brain);
            fields.FindProperty("config").objectReferenceValue = config;
            fields.ApplyModifiedPropertiesWithoutUndo();
            context = new Context { Self = self.transform };
            SecurityPatrolGizmos.InvalidatePreviewCache();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            Object.DestroyImmediate(config);
            SecurityPatrolGizmos.InvalidatePreviewCache();
        }

        [Test]
        public void VisionSector_MatchesTheRealSensorsHorizontalCone()
        {
            brain.Initialize(context, new ClearSight());
            Assert.That(SecurityPatrolGizmos.TryGetVisionSector(brain, out var cone), Is.True);
            Assert.That(cone.Origin, Is.EqualTo(context.Self.position + Vector3.up * config.eyeHeight));
            Assert.That(cone.Left.y, Is.Zero);
            Assert.That(cone.Right.y, Is.Zero);
            var sensor = new Perception(config.visionRange, config.visionAngle, new ClearSight());

            // Compare just inside/outside both drawn edges against the actual perception implementation.
            AssertSight(sensor, cone, Quaternion.AngleAxis(0.1f, Vector3.up) * cone.Left, true);
            AssertSight(sensor, cone, Quaternion.AngleAxis(-0.1f, Vector3.up) * cone.Left, false);
            AssertSight(sensor, cone, Quaternion.AngleAxis(-0.1f, Vector3.up) * cone.Right, true);
            AssertSight(sensor, cone, Quaternion.AngleAxis(0.1f, Vector3.up) * cone.Right, false);
        }

        [Test]
        public void RuntimeVision_UsesCachedTuningAndAbsoluteAlertRange()
        {
            brain.Initialize(context, new ClearSight());
            float angle = config.visionAngle;
            config.eyeHeight = 4f;
            config.visionRange = 2f;
            config.visionAngle = 10f;
            brain.OnAlert(new AgentAlert(1.25f, 1.4f, 0.9f));
            brain.OnAlert(new AgentAlert(1.25f, 1.4f, 0.9f));

            Assert.That(SecurityPatrolGizmos.TryGetVisionSector(brain, out var cone), Is.True);
            Assert.That(cone.Range, Is.EqualTo(30.8f).Within(0.0001f));
            Assert.That(cone.Angle, Is.EqualTo(angle));
            Assert.That(cone.Origin, Is.EqualTo(context.Self.position + Vector3.up * 1.62f));
        }

        [Test]
        public void EditPreview_UsesExactOwnerAndOrdinalIdsWithoutInitializingTheBrain()
        {
            string owner = brain.name;
            Marker tenth = MakeMarker("Waypoint_10", owner);
            Marker second = MakeMarker("Waypoint_02", owner);
            MakeMarker("Waypoint_01", owner + "-other");
            MakeMarker("Cover_01", owner, MarkerType.CoverPoint);
            SecurityPatrolGizmos.InvalidatePreviewCache();

            IReadOnlyList<Marker> route = SecurityPatrolGizmos.GetWaypoints(brain);

            Assert.That(route, Is.EqualTo(new[] { second, tenth }));
            Assert.That(SecurityPatrolGizmos.GetWaypoints(brain), Is.SameAs(route));
            Assert.That(brain.IsInitialized, Is.False);
            Assert.That(SecurityPatrolGizmos.TryGetVisionSector(brain, out var cone), Is.True);
            Assert.That(cone.Range, Is.EqualTo(config.visionRange));
        }

        [Test]
        public void InvalidatedPreview_ReflectsAnEditedOwner()
        {
            Marker point = MakeMarker("Waypoint_01", brain.name);
            SecurityPatrolGizmos.InvalidatePreviewCache();
            Assert.That(SecurityPatrolGizmos.GetWaypoints(brain).Count, Is.EqualTo(1));
            var fields = new SerializedObject(point);
            fields.FindProperty("owner").stringValue = "another patrol";
            fields.ApplyModifiedPropertiesWithoutUndo();

            SecurityPatrolGizmos.InvalidatePreviewCache();

            Assert.That(SecurityPatrolGizmos.GetWaypoints(brain), Is.Empty);
        }

        [Test]
        public void InitializedRoute_UsesTheFsmCacheRatherThanOtherSceneMarkers()
        {
            Marker point = MakeMarker("Waypoint_02", context.Self.name);
            context.Markers.Add(point);
            MakeMarker("Waypoint_01", context.Self.name);
            brain.Initialize(context, new ClearSight());

            IReadOnlyList<Marker> route = SecurityPatrolGizmos.GetWaypoints(brain);

            Assert.That(route, Is.SameAs(brain.Waypoints));
            Assert.That(route, Is.EqualTo(new[] { point }));
        }

        [Test]
        public void ZeroRangeAndDestroyedSelf_SkipInvalidGizmos()
        {
            config.visionRange = 0f;
            Assert.That(SecurityPatrolGizmos.TryGetVisionSector(brain, out _), Is.False);
            brain.Initialize(context, new ClearSight());
            Object.DestroyImmediate(context.Self.gameObject);

            Assert.That(SecurityPatrolGizmos.TryGetVisionSector(brain, out _), Is.False);
            Assert.That(SecurityPatrolGizmos.GetWaypoints(brain), Is.Empty);
        }

        [Test]
        public void WarmEditorReads_AllocateNoManagedMemory()
        {
            MakeMarker("Waypoint_01", brain.name);
            SecurityPatrolGizmos.InvalidatePreviewCache();
            for (int i = 0; i < 8; i++) ReadDiagnostics();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int points = 0;
            for (int i = 0; i < 128; i++) points += ReadDiagnostics();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(points, Is.EqualTo(128));
            Assert.That(allocated, Is.Zero);
        }

        private int ReadDiagnostics() => SecurityPatrolGizmos.TryGetVisionSector(brain, out _) ?
            SecurityPatrolGizmos.GetWaypoints(brain).Count : 0;

        private void AssertSight(Perception sensor, SecurityPatrolGizmos.VisionSector cone,
            Vector3 direction, bool expected)
        {
            sensor.UpdateVision(cone.Origin, context.Self.forward, cone.Origin + direction * 5f);
            Assert.That(sensor.CanSeePlayer, Is.EqualTo(expected));
        }

        private GameObject MakeObject(string name)
        {
            var item = new GameObject(name);
            objects.Add(item);
            return item;
        }

        private Marker MakeMarker(string id, string owner, MarkerType type = MarkerType.Waypoint)
        {
            Marker marker = MakeObject(id).AddComponent<Marker>();
            var fields = new SerializedObject(marker);
            fields.FindProperty("id").stringValue = id;
            fields.FindProperty("owner").stringValue = owner;
            fields.FindProperty("type").enumValueIndex = (int)type;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }
    }
}
