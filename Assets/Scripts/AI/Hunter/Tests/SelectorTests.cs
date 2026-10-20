// Owner: Systems Engineer (Hunter AI).
using System;
using NUnit.Framework;

namespace Blackout.AI.Hunter.Tests
{
    public class SelectorTests
    {
        private const NodeStatus Running = NodeStatus.Running;
        private const NodeStatus Success = NodeStatus.Success;
        private const NodeStatus Failure = NodeStatus.Failure;

        private BtContext context;

        [SetUp]
        public void SetUp()
        {
            context = new BtContext();
        }

        [Test]
        public void ReturnsTheFirstChildThatDoesNotFail_AndSkipsTheRest()
        {
            var a = new ScriptedNode("a", Failure);
            var b = new ScriptedNode("b", Success);
            var c = new ScriptedNode("c", Success);

            Assert.AreEqual(Success, new Selector("s", a, b, c).Tick(context));
            Assert.AreEqual(1, a.Ticks);
            Assert.AreEqual(1, b.Ticks);
            Assert.AreEqual(0, c.Ticks);
        }

        [Test]
        public void EveryChildFails_ReturnsFailure()
        {
            var a = new ScriptedNode("a", Failure);
            var b = new ScriptedNode("b", Failure);

            Assert.AreEqual(Failure, new Selector("s", a, b).Tick(context));
            Assert.AreEqual(1, a.Ticks);
            Assert.AreEqual(1, b.Ticks);
        }

        [Test]
        public void RunningChild_ReturnsRunning_AndSkipsTheRest()
        {
            var a = new ScriptedNode("a", Failure);
            var b = new ScriptedNode("b", Running);
            var c = new ScriptedNode("c", Success);

            Assert.AreEqual(Running, new Selector("s", a, b, c).Tick(context));
            Assert.AreEqual(0, c.Ticks);
        }

        [Test]
        public void StartsFromTheFirstChildEveryTick()
        {
            var a = new ScriptedNode("a", Failure);
            var b = new ScriptedNode("b", Running);
            var selector = new Selector("s", a, b);
            selector.Tick(context);
            selector.Tick(context);

            Assert.AreEqual(2, a.Ticks, "the higher-priority child is asked again while b runs");
            Assert.AreEqual(2, b.Ticks);
        }

        [Test]
        public void HigherPriorityChild_InterruptsTheRunningOne_AndResetsIt()
        {
            var a = new ScriptedNode("a", Failure);
            var b = new ScriptedNode("b", Running);
            var selector = new Selector("s", a, b);
            selector.Tick(context);

            a.Result = Running;
            Assert.AreEqual(Running, selector.Tick(context));
            Assert.AreEqual(1, b.Resets);
            Assert.AreEqual(1, b.Ticks, "b is not ticked once a has taken over");
        }

        [Test]
        public void SameChildRunningAgain_IsNotReset()
        {
            var b = new ScriptedNode("b", Running);
            var selector = new Selector("s", new ScriptedNode("a", Failure), b);
            for (int i = 0; i < 3; i++)
            {
                selector.Tick(context);
            }

            Assert.AreEqual(0, b.Resets);
            Assert.AreEqual(3, b.Ticks);
        }

        [Test]
        public void RunningChildThatFinishes_IsNotResetByTheSelector()
        {
            var b = new ScriptedNode("b", Running);
            var a = new ScriptedNode("a", Failure);
            var selector = new Selector("s", a, b);
            selector.Tick(context);

            b.Result = Success;
            Assert.AreEqual(Success, selector.Tick(context));
            a.Result = Success;
            selector.Tick(context);

            Assert.AreEqual(0, b.Resets, "b finished on its own, so nothing was running when a won");
        }

        [Test]
        public void EverythingFailsAfterAChildWasRunning_ResetsThatChild()
        {
            var b = new ScriptedNode("b", Running);
            var selector = new Selector("s", new ScriptedNode("a", Failure), b);
            selector.Tick(context);

            b.Result = Failure;
            Assert.AreEqual(Failure, selector.Tick(context));
            Assert.AreEqual(1, b.Resets);
        }

        [Test]
        public void Reset_ResetsEveryChild_AndForgetsTheRunningOne()
        {
            var a = new ScriptedNode("a", Failure);
            var b = new ScriptedNode("b", Running);
            var selector = new Selector("s", a, b);
            selector.Tick(context);
            selector.Reset();

            Assert.AreEqual(1, a.Resets);
            Assert.AreEqual(1, b.Resets);

            a.Result = Success;
            selector.Tick(context);
            Assert.AreEqual(1, b.Resets, "b was already reset, so a winning does not reset it again");
        }

        [Test]
        public void NoChildren_Fails()
        {
            Assert.AreEqual(Failure, new Selector("s").Tick(context));
        }

        [Test]
        public void ChildrenAreCopied_WhenBuilt()
        {
            var a = new ScriptedNode("a", Success);
            Node[] children = { a };
            var selector = new Selector("s", children);
            children[0] = new ScriptedNode("b", Success);
            selector.Tick(context);

            Assert.AreEqual(1, a.Ticks);
            Assert.AreSame(a, selector.Children[0]);
        }

        [Test]
        public void NullChildren_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new Selector("s", null));
            Assert.Throws<ArgumentException>(() => new Selector("s", new ScriptedNode("a", Success), null));
        }
    }
}
