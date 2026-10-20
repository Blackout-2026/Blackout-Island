// Owner: Systems Engineer (Hunter AI).
using System;
using NUnit.Framework;

namespace Blackout.AI.Hunter.Tests
{
    public class LeafAndDecoratorTests
    {
        private static readonly BlackboardKey<bool> HeardNoise = new BlackboardKey<bool>("HeardNoise");

        private BtContext context;

        [SetUp]
        public void SetUp()
        {
            context = new BtContext();
        }

        [TestCase(true, NodeStatus.Success)]
        [TestCase(false, NodeStatus.Failure)]
        public void Condition_MapsTrueToSuccessAndFalseToFailure(bool answer, NodeStatus expected)
        {
            Assert.AreEqual(expected, new Condition("c", _ => answer).Tick(context));
        }

        [Test]
        public void Condition_ReadsTheBlackboard_AndIsAGuard()
        {
            var condition = new Condition("HeardNoise", c => c.Blackboard.Get(HeardNoise));
            Assert.AreEqual(NodeStatus.Failure, condition.Tick(context));

            context.Blackboard.Set(HeardNoise, true);
            Assert.AreEqual(NodeStatus.Success, condition.Tick(context));
            Assert.IsTrue(condition.IsGuard);
        }

        [TestCase(NodeStatus.Running)]
        [TestCase(NodeStatus.Success)]
        [TestCase(NodeStatus.Failure)]
        public void ActionNode_ReturnsWhatTheActionReturns(NodeStatus result)
        {
            var action = new ActionNode("a", _ => result);

            Assert.AreEqual(result, action.Tick(context));
            Assert.IsFalse(action.IsGuard, "an action has side effects, so a Sequence never re-runs it as a guard");
        }

        [Test]
        public void ActionNode_Reset_CallsItsResetCallback()
        {
            int resets = 0;
            var action = new ActionNode("a", _ => NodeStatus.Running, () => resets++);
            action.Tick(context);
            action.Reset();

            Assert.AreEqual(1, resets);
            Assert.DoesNotThrow(() => new ActionNode("b", _ => NodeStatus.Success).Reset());
        }

        [TestCase(NodeStatus.Success, NodeStatus.Failure)]
        [TestCase(NodeStatus.Failure, NodeStatus.Success)]
        [TestCase(NodeStatus.Running, NodeStatus.Running)]
        public void Inverter_SwapsSuccessAndFailure(NodeStatus child, NodeStatus expected)
        {
            Assert.AreEqual(expected, new Inverter("not", new ScriptedNode("c", child)).Tick(context));
        }

        [Test]
        public void Inverter_IsAGuardOnlyIfItsChildIs_AndResetsItsChild()
        {
            var leaf = new ScriptedNode("c", NodeStatus.Running);
            var inverter = new Inverter("not", leaf);
            inverter.Reset();

            Assert.IsTrue(new Inverter("not", new Condition("c", _ => true)).IsGuard);
            Assert.IsFalse(inverter.IsGuard);
            Assert.AreEqual(1, leaf.Resets);
        }

        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new Condition("c", null));
            Assert.Throws<ArgumentNullException>(() => new ActionNode("a", null));
            Assert.Throws<ArgumentNullException>(() => new Inverter("not", null));
            Assert.Throws<ArgumentNullException>(() => new Cooldown("cd", 1f, null));
        }
    }
}
