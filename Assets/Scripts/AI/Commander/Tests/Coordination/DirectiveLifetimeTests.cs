// Owner: Agent Controller (Commander coordination tests).
using System;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Commander.Coordination.Tests
{
    public sealed class DirectiveLifetimeTests
    {
        private static readonly Vector3 FirstTarget = new Vector3(10f, 2f, -20f);
        private static readonly Vector3 LatestTarget = new Vector3(-4f, 2f, 15f);

        [TestCase(DirectiveType.Converge)]
        [TestCase(DirectiveType.SearchZone)]
        [TestCase(DirectiveType.Regroup)]
        public void SameTypeWithNewPayload_ReplacesTargetAndRadiusForEveryReceiver(DirectiveType type)
        {
            var harness = new SceneFreeDirectiveHarness();
            harness.Broadcast(new Directive(type, FirstTarget, 9f));
            harness.Tick(1f);
            var latest = new Directive(type, LatestTarget, 4f);

            harness.Broadcast(latest);
            harness.Tick(0.1f);

            AssertLatest(harness, latest);
            Assert.That(harness.PatrolIntent.moveTarget, Is.EqualTo(LatestTarget));
            Assert.That(harness.GuardIntent.moveTarget, Is.EqualTo(LatestTarget));
            Vector3 hunterTarget = type == DirectiveType.SearchZone ? LatestTarget + Vector3.right * 2f : LatestTarget;
            Assert.That(harness.HunterIntent.moveTarget, Is.EqualTo(hunterTarget));
        }

        [TestCase(DirectiveType.Converge)]
        [TestCase(DirectiveType.SearchZone)]
        [TestCase(DirectiveType.Regroup)]
        public void IdenticalRepeat_PreservesElapsedTimeAndDoesNotEnableFire(DirectiveType type)
        {
            var harness = new SceneFreeDirectiveHarness();
            var directive = new Directive(type, FirstTarget, 9f);
            harness.Broadcast(directive);
            harness.Tick(2f);

            harness.Broadcast(directive);

            AssertElapsed(harness, 2f);
            harness.Tick(0.5f);
            AssertElapsed(harness, 2.5f);
            Assert.That(harness.PatrolIntent.fire, Is.False);
            Assert.That(harness.HunterIntent.fire, Is.False);
            Assert.That(harness.GuardIntent.fire, Is.False);
        }

        [Test]
        public void DifferentType_ReplacesPreviousTaskAndStartsItsOwnElapsedTime()
        {
            var harness = new SceneFreeDirectiveHarness();
            harness.Broadcast(new Directive(DirectiveType.SearchZone, FirstTarget, 9f));
            harness.Tick(2f);
            var regroup = new Directive(DirectiveType.Regroup, LatestTarget, 10f);

            harness.Broadcast(regroup);

            AssertLatest(harness, regroup);
            AssertElapsed(harness, 0f);
            harness.Tick(0.1f);
            Assert.That(harness.Patrol.State, Is.EqualTo(PatrolState.Investigate));
            Assert.That(harness.Hunter.Task, Is.EqualTo(HunterTask.Regroup));
        }

        [Test]
        public void LocalBehavior_InterruptsOrdersAndThenResumesTheLatestTarget()
        {
            var harness = new SceneFreeDirectiveHarness();
            harness.Broadcast(new Directive(DirectiveType.SearchZone, FirstTarget, 9f));
            harness.Tick(1f);
            harness.Patrol.LocalCombat = true;
            harness.Hunter.LocalCombat = true;
            harness.Guard.SelectedAction = GuardDecision.MoveToCover;
            var latest = new Directive(DirectiveType.Converge, LatestTarget, 0f);

            harness.Broadcast(latest);
            harness.Tick(3f);

            AssertLatest(harness, latest);
            Assert.That(harness.Patrol.State, Is.EqualTo(PatrolState.Attack));
            Assert.That(harness.Hunter.Branch, Is.EqualTo(HunterBranch.Engage));
            Assert.That(harness.Guard.SelectedAction, Is.EqualTo(GuardDecision.MoveToCover));
            AssertElapsed(harness, 0f); // this example counts active order execution, not time in combat

            harness.Patrol.LocalCombat = false;
            harness.Hunter.LocalCombat = false;
            harness.Guard.SelectedAction = GuardDecision.HoldPost;
            harness.Tick(0.1f);

            Assert.That(harness.Patrol.State, Is.EqualTo(PatrolState.Pursue));
            Assert.That(harness.Hunter.Branch, Is.EqualTo(HunterBranch.FollowOrders));
            Assert.That(harness.PatrolIntent.moveTarget, Is.EqualTo(LatestTarget));
            Assert.That(harness.HunterIntent.moveTarget, Is.EqualTo(LatestTarget));
            Assert.That(harness.GuardIntent.moveTarget, Is.EqualTo(LatestTarget));
        }

        [Test]
        public void WithoutFurtherDelivery_ReceiversKeepTheirLastOrder()
        {
            var harness = new SceneFreeDirectiveHarness();
            var last = new Directive(DirectiveType.Regroup, FirstTarget, 10f);
            harness.Broadcast(last);

            for (int i = 0; i < 5; i++) harness.Tick(0.1f);

            AssertLatest(harness, last);
            AssertElapsed(harness, 0.5f);
            Assert.That(harness.Patrol.State, Is.EqualTo(PatrolState.Investigate));
            Assert.That(harness.Hunter.Task, Is.EqualTo(HunterTask.Regroup));
        }

        [Test]
        public void OwnerCompletion_ResumesPatrolAndProwlWhileGuardKeepsItsNewPost()
        {
            var harness = new SceneFreeDirectiveHarness();
            harness.Broadcast(new Directive(DirectiveType.Converge, LatestTarget, 0f));
            harness.Tick(1f);

            harness.Patrol.CompleteOrder();
            harness.Hunter.CompleteOrder();
            harness.Guard.CompleteOrder();
            harness.Tick(0.1f);

            Assert.That(harness.Patrol.HasOrder, Is.False);
            Assert.That(harness.Hunter.HasOrder, Is.False);
            Assert.That(harness.Guard.HasOrder, Is.False);
            Assert.That(harness.Patrol.State, Is.EqualTo(PatrolState.Patrol));
            Assert.That(harness.Hunter.Branch, Is.EqualTo(HunterBranch.Prowl));
            Assert.That(harness.Guard.Post, Is.EqualTo(LatestTarget));
            Assert.That(harness.GuardIntent.moveTarget, Is.EqualTo(LatestTarget));
        }

        [TestCase(-1)]
        [TestCase(99)]
        public void UnknownDirective_PreservesPreviousOrderAndElapsedTime(int invalidType)
        {
            var harness = new SceneFreeDirectiveHarness();
            var valid = new Directive(DirectiveType.SearchZone, FirstTarget, 9f);
            harness.Broadcast(valid);
            harness.Tick(2f);

            harness.Broadcast(new Directive((DirectiveType)invalidType, LatestTarget, 1f));

            AssertLatest(harness, valid);
            AssertElapsed(harness, 2f);
            Assert.That(harness.Guard.Post, Is.EqualTo(FirstTarget));
        }

        [Test]
        public void OnDirective_OnlyChangesDecisionDataUntilTheNormalThinkTick()
        {
            var harness = new SceneFreeDirectiveHarness();
            harness.Guard.SelectedAction = GuardDecision.Attack;

            harness.Broadcast(new Directive(DirectiveType.Converge, LatestTarget, 0f));

            Assert.That(harness.Patrol.State, Is.EqualTo(PatrolState.Patrol));
            Assert.That(harness.Hunter.Branch, Is.EqualTo(HunterBranch.Prowl));
            Assert.That(harness.Guard.SelectedAction, Is.EqualTo(GuardDecision.Attack));
            Assert.That(harness.Guard.Post, Is.EqualTo(LatestTarget));
            Assert.That(harness.PatrolIntent.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(harness.HunterIntent.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(harness.GuardIntent.moveSpeed, Is.EqualTo(MoveSpeed.Stop));
            Assert.That(harness.PatrolIntent.fire || harness.HunterIntent.fire || harness.GuardIntent.fire, Is.False);
        }

        [TestCase("Attack")]
        [TestCase("MoveToCover")]
        [TestCase("Reposition")]
        public void GuardOrder_DoesNotOverrideAnExistingUtilityChoice(string decision)
        {
            var harness = new SceneFreeDirectiveHarness();
            var localChoice = (GuardDecision)Enum.Parse(typeof(GuardDecision), decision);
            harness.Guard.SelectedAction = localChoice;

            harness.Broadcast(new Directive(DirectiveType.Regroup, LatestTarget, 10f));
            harness.Tick(0.1f);

            Assert.That(harness.Guard.Post, Is.EqualTo(LatestTarget));
            Assert.That(harness.Guard.SelectedAction, Is.EqualTo(localChoice));
            Assert.That(harness.GuardIntent.moveSpeed, Is.EqualTo(MoveSpeed.Stop),
                "The fake leaves non-post execution to the owner's local utility action.");
        }

        private static void AssertLatest(SceneFreeDirectiveHarness harness, Directive expected)
        {
            AssertOrder(harness.Patrol, expected);
            AssertOrder(harness.Hunter, expected);
            AssertOrder(harness.Guard, expected);
        }

        private static void AssertOrder(FakeDirectiveReceiver receiver, Directive expected)
        {
            Assert.That(receiver.HasOrder, Is.True);
            Assert.That(receiver.LastOrder.type, Is.EqualTo(expected.type));
            Assert.That(receiver.LastOrder.position, Is.EqualTo(expected.position));
            Assert.That(receiver.LastOrder.radius, Is.EqualTo(expected.radius));
        }

        private static void AssertElapsed(SceneFreeDirectiveHarness harness, float expected)
        {
            Assert.That(harness.Patrol.OrderElapsed, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(harness.Hunter.OrderElapsed, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(harness.Guard.OrderElapsed, Is.EqualTo(expected).Within(0.0001f));
        }
    }
}
