// Owner: Systems Engineer (Hunter AI).
using System.Collections.Generic;
using NUnit.Framework;

namespace Blackout.AI.Hunter.Tests
{
    public class SequenceTests
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
        public void EveryChildSucceeds_RunsThemInOrder_AndSucceeds()
        {
            var log = new List<string>();
            var sequence = new Sequence("s",
                new ScriptedNode("a", Success, log), new ScriptedNode("b", Success, log), new ScriptedNode("c", Success, log));

            Assert.AreEqual(Success, sequence.Tick(context));
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, log);
        }

        [Test]
        public void StopsAtTheFirstFailure()
        {
            var c = new ScriptedNode("c", Success);
            var sequence = new Sequence("s", new ScriptedNode("a", Success), new ScriptedNode("b", Failure), c);

            Assert.AreEqual(Failure, sequence.Tick(context));
            Assert.AreEqual(0, c.Ticks);
        }

        [Test]
        public void KeepsItsPlace_WhileAChildRuns()
        {
            var a = new ScriptedNode("a", Success);
            var b = new ScriptedNode("b", Running);
            var sequence = new Sequence("s", a, b);

            Assert.AreEqual(Running, sequence.Tick(context));
            Assert.AreEqual(Running, sequence.Tick(context));
            Assert.AreEqual(1, a.Ticks, "a finished, so it is not run again (it is not a guard)");
            Assert.AreEqual(2, b.Ticks);
        }

        [Test]
        public void ContinuesWithTheNextChild_WhenTheRunningOneSucceeds()
        {
            var b = new ScriptedNode("b", Running);
            var c = new ScriptedNode("c", Success);
            var sequence = new Sequence("s", new ScriptedNode("a", Success), b, c);
            sequence.Tick(context);

            b.Result = Success;
            Assert.AreEqual(Success, sequence.Tick(context));
            Assert.AreEqual(1, c.Ticks);
        }

        [Test]
        public void ConditionBeforeTheRunningChild_IsCheckedEveryTick()
        {
            int checks = 0;
            var guard = new Condition("SeesPlayer", _ => { checks++; return true; });
            var sequence = new Sequence("s", guard, new ScriptedNode("act", Running));
            for (int i = 0; i < 3; i++)
            {
                sequence.Tick(context);
            }

            Assert.AreEqual(3, checks);
        }

        [Test]
        public void ConditionTurnsFalse_StopsAndResetsTheRunningChild()
        {
            bool seesPlayer = true;
            var act = new ScriptedNode("act", Running);
            var sequence = new Sequence("s", new Condition("SeesPlayer", _ => seesPlayer), act);
            sequence.Tick(context);

            seesPlayer = false;
            Assert.AreEqual(Failure, sequence.Tick(context));
            Assert.AreEqual(1, act.Ticks, "the action is not ticked once its premise is false");
            Assert.AreEqual(1, act.Resets);
        }

        [Test]
        public void InvertedCondition_IsCheckedEveryTickToo()
        {
            bool seesPlayer = false;
            var act = new ScriptedNode("act", Running);
            var sequence = new Sequence("s",
                new Inverter("NotSeen", new Condition("SeesPlayer", _ => seesPlayer)), act);
            sequence.Tick(context);

            seesPlayer = true;
            Assert.AreEqual(Failure, sequence.Tick(context));
            Assert.AreEqual(1, act.Resets);
        }

        [Test]
        public void StartsOver_AfterSucceeding()
        {
            var a = new ScriptedNode("a", Success);
            var sequence = new Sequence("s", a, new ScriptedNode("b", Success));
            sequence.Tick(context);
            sequence.Tick(context);

            Assert.AreEqual(2, a.Ticks);
        }

        [Test]
        public void StartsOver_AfterFailing()
        {
            var a = new ScriptedNode("a", Success);
            var b = new ScriptedNode("b", Running);
            var sequence = new Sequence("s", a, b);
            sequence.Tick(context);

            b.Result = Failure;
            sequence.Tick(context);
            b.Result = Running;
            sequence.Tick(context);

            Assert.AreEqual(2, a.Ticks);
        }

        [Test]
        public void Reset_StartsOverFromTheFirstChild()
        {
            var a = new ScriptedNode("a", Success);
            var b = new ScriptedNode("b", Running);
            var sequence = new Sequence("s", a, b);
            sequence.Tick(context);
            sequence.Reset();
            sequence.Tick(context);

            Assert.AreEqual(2, a.Ticks);
            Assert.AreEqual(1, b.Resets);
        }

        [Test]
        public void NoChildren_Succeeds()
        {
            Assert.AreEqual(Success, new Sequence("s").Tick(context));
        }
    }
}
