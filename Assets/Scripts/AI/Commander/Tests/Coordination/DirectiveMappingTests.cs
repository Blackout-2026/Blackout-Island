// Owner: Agent Controller (Commander coordination tests).
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Commander.Coordination.Tests
{
    public sealed class DirectiveMappingTests
    {
        private static readonly Vector3 Target = new Vector3(11f, 2f, -17f);

        [TestCase(DirectiveType.Converge, "Pursue", MoveSpeed.Run)]
        [TestCase(DirectiveType.SearchZone, "Search", MoveSpeed.Walk)]
        [TestCase(DirectiveType.Regroup, "Investigate", MoveSpeed.Walk)]
        public void Patrol_MapsOrderToItsOwnFsmState(DirectiveType type, string state, MoveSpeed speed)
        {
            var harness = new SceneFreeDirectiveHarness();
            var directive = new Directive(type, Target, 9f);

            harness.Broadcast(directive);
            harness.Tick(0.1f);

            Assert.That(harness.Patrol.State.ToString(), Is.EqualTo(state));
            AssertOrder(harness.Patrol, directive);
            AssertMovement(harness.PatrolIntent, Target, speed);
        }

        [TestCase(DirectiveType.Converge, "Converge", MoveSpeed.Run)]
        [TestCase(DirectiveType.SearchZone, "Search", MoveSpeed.Run)]
        [TestCase(DirectiveType.Regroup, "Regroup", MoveSpeed.Walk)]
        public void Hunter_UsesFollowOrdersWithItsOwnMovementLeaf(DirectiveType type, string task, MoveSpeed speed)
        {
            var harness = new SceneFreeDirectiveHarness();
            var directive = new Directive(type, Target, 9f);

            harness.Broadcast(directive);
            harness.Tick(0.1f);

            Assert.That(harness.Hunter.Branch, Is.EqualTo(HunterBranch.FollowOrders));
            Assert.That(harness.Hunter.Task.ToString(), Is.EqualTo(task));
            AssertOrder(harness.Hunter, directive);
            Assert.That(harness.HunterIntent.moveSpeed, Is.EqualTo(speed));
            Assert.That(harness.HunterIntent.fire, Is.False);
            Assert.That(harness.HunterIntent.aim, Is.False);
            if (type == DirectiveType.SearchZone)
            {
                Assert.That(Vector3.Distance(harness.HunterIntent.moveTarget, Target), Is.GreaterThan(0f));
                Assert.That(Vector3.Distance(harness.HunterIntent.moveTarget, Target), Is.LessThanOrEqualTo(9f));
                Assert.That(harness.HunterIntent.moveTarget.y, Is.EqualTo(Target.y));
            }
            else
                Assert.That(harness.HunterIntent.moveTarget, Is.EqualTo(Target));
        }

        [TestCase(DirectiveType.Converge)]
        [TestCase(DirectiveType.SearchZone)]
        [TestCase(DirectiveType.Regroup)]
        public void Guard_UpdatesPostWithoutReplacingItsUtilityDecision(DirectiveType type)
        {
            var harness = new SceneFreeDirectiveHarness();
            var directive = new Directive(type, Target, 9f);

            harness.Broadcast(directive);
            harness.Tick(0.1f);

            Assert.That(harness.Guard.Post, Is.EqualTo(Target));
            Assert.That(harness.Guard.SelectedAction, Is.EqualTo(GuardDecision.HoldPost));
            AssertOrder(harness.Guard, directive);
            AssertMovement(harness.GuardIntent, Target, MoveSpeed.Walk);
        }

        [Test]
        public void Assembly_ReferencesOnlySharedGameContracts()
        {
            int sharedReferences = 0;
            foreach (var reference in typeof(SceneFreeDirectiveHarness).Assembly.GetReferencedAssemblies())
            {
                if (!reference.Name.StartsWith("Blackout.")) continue;
                Assert.That(reference.Name, Is.EqualTo("Blackout.Shared"));
                sharedReferences++;
            }
            Assert.That(sharedReferences, Is.EqualTo(1));
        }

        private static void AssertOrder(FakeDirectiveReceiver receiver, Directive expected)
        {
            Assert.That(receiver.HasOrder, Is.True);
            Assert.That(receiver.LastOrder.type, Is.EqualTo(expected.type));
            Assert.That(receiver.LastOrder.position, Is.EqualTo(expected.position));
            Assert.That(receiver.LastOrder.radius, Is.EqualTo(expected.radius));
        }

        private static void AssertMovement(AgentIntent intent, Vector3 target, MoveSpeed speed)
        {
            Assert.That(intent.moveTarget, Is.EqualTo(target));
            Assert.That(intent.moveSpeed, Is.EqualTo(speed));
            Assert.That(intent.aim, Is.False);
            Assert.That(intent.fire, Is.False);
        }
    }
}
