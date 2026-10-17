// Owner: Core Developer (Tactical Guard AI).
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.TacticalGuard.Tests
{
    public sealed class GuardIntentBuilderTests
    {
        private TacticalGuardConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<TacticalGuardConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Attack_StandsAimsFiresAndLooksAtTheChest()
        {
            Vector3 chest = new Vector3(1f, 1.2f, 15f);

            AgentIntent intent = GuardIntentBuilder.Attack(chest);

            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
            Assert.IsTrue(intent.aim);
            Assert.IsTrue(intent.fire);
            Assert.IsTrue(intent.hasLookTarget);
            Assert.AreEqual(chest, intent.lookTarget);
        }

        [Test]
        public void MoveToCover_RunsToTheCoverAndFiresOnTheWayWhileTheGuardCanSeeThePlayer()
        {
            Vector3 cover = new Vector3(5f, 0f, 0f);
            Vector3 chest = new Vector3(0f, 1.2f, 15f);

            AgentIntent intent = GuardIntentBuilder.MoveToCover(cover, 5f, true, chest, true, config);

            Assert.AreEqual(MoveSpeed.Run, intent.moveSpeed);
            Assert.AreEqual(cover, intent.moveTarget);
            Assert.IsTrue(intent.aim);
            Assert.IsTrue(intent.fire);
            Assert.AreEqual(chest, intent.lookTarget);
        }

        [Test]
        public void MoveToCover_DoesNotFireWhenThePlayerIsHidden()
        {
            AgentIntent intent = GuardIntentBuilder.MoveToCover(Vector3.right, 5f, false, Vector3.zero, false, config);

            Assert.AreEqual(MoveSpeed.Run, intent.moveSpeed);
            Assert.IsFalse(intent.aim);
            Assert.IsFalse(intent.fire);
            Assert.IsFalse(intent.hasLookTarget);
        }

        [Test]
        public void MoveToCover_StopsOnceWithinTheArriveDistance()
        {
            AgentIntent intent = GuardIntentBuilder.MoveToCover(Vector3.right, config.coverArriveDistance, true, Vector3.forward, true, config);

            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
            Assert.IsTrue(intent.fire, "keeps shooting from cover");
        }

        [Test]
        public void Reposition_RunsToTheTargetFacingTheWayItRuns()
        {
            Vector3 target = new Vector3(10f, 0f, 10f);

            AgentIntent intent = GuardIntentBuilder.Reposition(target, 14f, Vector3.one, true, config);

            Assert.AreEqual(MoveSpeed.Run, intent.moveSpeed);
            Assert.AreEqual(target, intent.moveTarget);
            Assert.IsFalse(intent.hasLookTarget);
            Assert.IsFalse(intent.aim);
            Assert.IsFalse(intent.fire);
        }

        [Test]
        public void Reposition_OnArrivalStopsAndLooksAtTheLastKnownPosition()
        {
            Vector3 lastKnown = new Vector3(20f, 0f, 20f);

            AgentIntent intent = GuardIntentBuilder.Reposition(Vector3.zero, 0.5f, lastKnown, true, config);

            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
            Assert.IsTrue(intent.hasLookTarget);
            Assert.AreEqual(lastKnown, intent.lookTarget);
        }

        [Test]
        public void HoldPost_WalksBackWhenAwayFromThePost()
        {
            Vector3 post = new Vector3(3f, 2.5f, 3f);

            AgentIntent intent = GuardIntentBuilder.HoldPost(post, 6f, 0f, config);

            Assert.AreEqual(MoveSpeed.Walk, intent.moveSpeed);
            Assert.AreEqual(post, intent.moveTarget);
            Assert.IsFalse(intent.fire);
        }

        [Test]
        public void HoldPost_AtThePostStopsAndScansWithTheHead()
        {
            Vector3 post = new Vector3(3f, 2.5f, 3f);

            AgentIntent intent = GuardIntentBuilder.HoldPost(post, 0.2f, 0f, config);

            Assert.AreEqual(MoveSpeed.Stop, intent.moveSpeed);
            Assert.IsTrue(intent.hasLookTarget);
            Assert.AreEqual(GuardMath.ScanPoint(post, 0f, config), intent.lookTarget);
            Assert.IsFalse(intent.aim);
        }
    }
}
