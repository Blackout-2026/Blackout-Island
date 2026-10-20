// Owner: Systems Engineer (Hunter AI).
using System;
using NUnit.Framework;

namespace Blackout.AI.Hunter.Tests
{
    public class NodeTests
    {
        /// <summary>Returns whatever the test sets, and counts its resets.</summary>
        private sealed class FixedNode : Node
        {
            public NodeStatus Result = NodeStatus.Success;
            public int Resets;

            public FixedNode(string name = null) : base(name)
            {
            }

            protected override NodeStatus OnTick(BtContext context)
            {
                return Result;
            }

            protected override void OnReset()
            {
                Resets++;
            }
        }

        [Test]
        public void Name_DefaultsToTheTypeName()
        {
            Assert.AreEqual("FixedNode", new FixedNode().Name);
            Assert.AreEqual("Prowl", new FixedNode("Prowl").Name);
        }

        [Test]
        public void LastStatus_IsNullUntilTicked()
        {
            Assert.IsNull(new FixedNode().LastStatus);
        }

        [TestCase(NodeStatus.Running)]
        [TestCase(NodeStatus.Success)]
        [TestCase(NodeStatus.Failure)]
        public void Tick_ReturnsAndRemembersTheStatus(NodeStatus result)
        {
            var node = new FixedNode { Result = result };

            Assert.AreEqual(result, node.Tick(new BtContext()));
            Assert.AreEqual(result, node.LastStatus);
        }

        [Test]
        public void Reset_ClearsLastStatusAndCallsOnReset()
        {
            var node = new FixedNode { Result = NodeStatus.Running };
            node.Tick(new BtContext());
            node.Reset();

            Assert.IsNull(node.LastStatus);
            Assert.AreEqual(1, node.Resets);
        }

        [Test]
        public void IsGuard_IsFalseByDefault()
        {
            Assert.IsFalse(new FixedNode().IsGuard);
        }

        [Test]
        public void Tick_WithoutAContext_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new FixedNode().Tick(null));
        }
    }
}
