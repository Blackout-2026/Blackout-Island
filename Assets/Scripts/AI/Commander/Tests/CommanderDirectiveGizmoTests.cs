// Owner: Agent Controller (Commander diagnostics).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Commander.Tests
{
    public sealed class CommanderDirectiveGizmoTests
    {
        private sealed class Context : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health { get; set; }
            public Blackboard Blackboard { get; set; }
            public Transform Player => null;
            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null) => Array.Empty<Marker>();
        }

        private sealed class Receiver : IDirectiveReceiver
        {
            public int Calls;
            public void OnDirective(Directive directive) => Calls++;
        }

        private readonly List<GameObject> objects = new List<GameObject>();
        private CommanderConfig config;
        private CommanderBrain brain;
        private CommanderDirectiveGizmos renderer;
        private Blackboard board;
        private BlackboardAgent commander;
        private BlackboardAgent unit;
        private Receiver receiver;

        [SetUp]
        public void SetUp()
        {
            board = new Blackboard();
            GameObject self = MakeObject("G4-Commander");
            commander = board.RegisterAgent(self.name, self.transform, self.GetComponent<Health>(), null);
            brain = self.AddComponent<CommanderBrain>();
            renderer = self.AddComponent<CommanderDirectiveGizmos>();
            config = ScriptableObject.CreateInstance<CommanderConfig>();
            brain.SetConfig(config);
            brain.Initialize(new Context { Self = self.transform, Health = commander.Health, Blackboard = board });
            GameObject other = MakeObject("G4-Unit");
            other.transform.position = new Vector3(10f, 0f, 20f);
            receiver = new Receiver();
            unit = board.RegisterAgent(other.name, other.transform, other.GetComponent<Health>(), receiver);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(objects[i]);
            UnityEngine.Object.DestroyImmediate(config);
            objects.Clear();
        }

        [TestCase(DirectiveType.Converge, 0f)]
        [TestCase(DirectiveType.SearchZone, 9f)]
        [TestCase(DirectiveType.Regroup, 10f)]
        public void KnownDirective_ProducesExactGeometryAndDistinctStyle(DirectiveType type, float radius)
        {
            Vector3 target = new Vector3(30f, 2f, -15f);
            unit.Issue(new Directive(type, target, radius));

            Assert.That(renderer.TryGetDirective(1, out CommanderDirectiveGizmo gizmo), Is.True);

            Assert.That(gizmo.Type, Is.EqualTo(type));
            Assert.That(gizmo.Source, Is.EqualTo(unit.Transform.position));
            Assert.That(gizmo.Target, Is.EqualTo(target));
            Assert.That(gizmo.Radius, Is.EqualTo(radius));
            Assert.That(gizmo.DrawRadius, Is.EqualTo(radius == 0f ? 0.25f : radius));
            Color color = type == DirectiveType.Converge ? Color.cyan :
                type == DirectiveType.SearchZone ? Color.yellow : Color.magenta;
            Assert.That(gizmo.Color, Is.EqualTo(color));
        }

        [Test]
        public void UninitializedBrain_HasNoGeometry()
        {
            GameObject uninitialized = MakeObject("G4-Uninitialized");
            var view = uninitialized.AddComponent<CommanderDirectiveGizmos>();

            Assert.That(view.TryGetDirective(0, out _), Is.False);
        }

        [Test]
        public void UnissuedSelfAndInvalidIndices_AreSkipped()
        {
            commander.Issue(new Directive(DirectiveType.Converge, Vector3.one, 0f));

            Assert.That(renderer.TryGetDirective(0, out _), Is.False);
            Assert.That(renderer.TryGetDirective(1, out _), Is.False);
            Assert.That(renderer.TryGetDirective(-1, out _), Is.False);
            Assert.That(renderer.TryGetDirective(2, out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeadOrDestroyedRecipient_IsSkipped(bool destroy)
        {
            unit.Issue(new Directive(DirectiveType.SearchZone, Vector3.one, 9f));
            if (destroy) UnityEngine.Object.DestroyImmediate(unit.Transform.gameObject);
            else Kill(unit.Health);

            Assert.That(renderer.TryGetDirective(1, out _), Is.False);
        }

        [TestCase(float.NaN, 9f)]
        [TestCase(float.PositiveInfinity, 9f)]
        [TestCase(0f, float.NaN)]
        [TestCase(0f, float.PositiveInfinity)]
        [TestCase(0f, -1f)]
        public void InvalidGeometry_IsSkippedWithoutChangingTheOrder(float targetX, float radius)
        {
            unit.Issue(new Directive(DirectiveType.SearchZone, new Vector3(targetX, 0f, 1f), radius));

            Assert.That(renderer.TryGetDirective(1, out _), Is.False);
            Assert.That(unit.HasDirective, Is.True);
            Assert.That(receiver.Calls, Is.EqualTo(1));
        }

        [Test]
        public void UnknownDirective_IsSkipped()
        {
            unit.Issue(new Directive((DirectiveType)99, Vector3.one, 1f));

            Assert.That(renderer.TryGetDirective(1, out _), Is.False);
        }

        [Test]
        public void RetainedOrderWithoutReceiver_StillHasGeometry()
        {
            board.RegisterAgent(unit.Name, unit.Transform, unit.Health, null);
            unit.Issue(new Directive(DirectiveType.Regroup, Vector3.one, 10f));

            Assert.That(renderer.TryGetDirective(1, out _), Is.True);
            Assert.That(receiver.Calls, Is.Zero);
        }

        [Test]
        public void GeometryRead_DoesNotDeliverOrdersReplanOrMutateTheBlackboard()
        {
            unit.Issue(new Directive(DirectiveType.Converge, Vector3.one, 0f));
            int version = brain.PlanVersion;

            renderer.TryGetDirective(1, out _);
            renderer.TryGetDirective(1, out _);

            Assert.That(receiver.Calls, Is.EqualTo(1));
            Assert.That(brain.PlanVersion, Is.EqualTo(version));
            Assert.That(board.cureTaken, Is.False);
            Assert.That(board.alertLevel, Is.Zero);
            Assert.That(unit.LastDirective.position, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void NewOrder_ReplacesOldGeometry()
        {
            unit.Issue(new Directive(DirectiveType.Converge, Vector3.one, 0f));
            unit.Issue(new Directive(DirectiveType.SearchZone, Vector3.right * 20f, 9f));

            Assert.That(renderer.TryGetDirective(1, out CommanderDirectiveGizmo gizmo), Is.True);
            Assert.That(gizmo.Type, Is.EqualTo(DirectiveType.SearchZone));
            Assert.That(gizmo.Target, Is.EqualTo(Vector3.right * 20f));
            Assert.That(gizmo.Radius, Is.EqualTo(9f));
        }

        [Test]
        public void CommanderDeath_DoesNotHideSurvivorsRetainedOrders()
        {
            unit.Issue(new Directive(DirectiveType.Regroup, Vector3.one, 10f));
            Kill(commander.Health);

            Assert.That(renderer.TryGetDirective(1, out _), Is.True);
        }

        [TestCase(DirectiveType.Converge)]
        [TestCase(DirectiveType.SearchZone)]
        [TestCase(DirectiveType.Regroup)]
        public void ThreeRecipientsOfSameType_ProduceThreeSeparateDescriptors(DirectiveType type)
        {
            unit.Issue(new Directive(type, Vector3.one, 9f));
            for (int i = 0; i < 2; i++)
            {
                GameObject extra = MakeObject("G4-Extra-" + i);
                extra.transform.position = new Vector3(i + 30f, 0f, 0f);
                board.RegisterAgent(extra.name, extra.transform, extra.GetComponent<Health>(), null)
                    .Issue(new Directive(type, Vector3.one, 9f));
            }

            int count = 0;
            for (int i = 0; i < board.Agents.Count; i++)
                if (renderer.TryGetDirective(i, out _)) count++;

            Assert.That(count, Is.EqualTo(3));
        }

        private GameObject MakeObject(string name)
        {
            var obj = new GameObject(name);
            objects.Add(obj);
            obj.AddComponent<Health>().ResetHealth(0f);
            return obj;
        }

        private static void Kill(Health health) => health.TakeDamage(
            new DamageInfo(1000f, Vector3.zero, Vector3.zero, null, DamageType.Generic));
    }
}
