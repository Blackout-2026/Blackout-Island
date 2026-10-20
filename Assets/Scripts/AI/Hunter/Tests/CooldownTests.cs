// Owner: Systems Engineer (Hunter AI).
using System;
using NUnit.Framework;

namespace Blackout.AI.Hunter.Tests
{
    public class CooldownTests
    {
        // Steps are powers of two, so the clock adds up exactly.
        private const float Seconds = 2f;

        private BtContext context;

        [SetUp]
        public void SetUp()
        {
            context = new BtContext();
        }

        [Test]
        public void IsReadyAtTheStart()
        {
            var child = new ScriptedNode("ambush", NodeStatus.Success);

            Assert.AreEqual(NodeStatus.Success, new Cooldown("cd", Seconds, child).Tick(context));
            Assert.AreEqual(1, child.Ticks);
        }

        [Test]
        public void AfterTheChildSucceeds_FailsWithoutTickingItUntilTheTimeIsUp()
        {
            var child = new ScriptedNode("ambush", NodeStatus.Success);
            var cooldown = new Cooldown("cd", Seconds, child);
            cooldown.Tick(context);

            context.Advance(1.5f);
            Assert.AreEqual(NodeStatus.Failure, cooldown.Tick(context));
            Assert.AreEqual(1, child.Ticks);

            context.Advance(0.5f);
            Assert.AreEqual(NodeStatus.Success, cooldown.Tick(context), "ready again exactly 2 s after it finished");
            Assert.AreEqual(2, child.Ticks);
        }

        [Test]
        public void AfterTheChildFails_CoolsDownToo()
        {
            var child = new ScriptedNode("ambush", NodeStatus.Failure);
            var cooldown = new Cooldown("cd", Seconds, child);
            cooldown.Tick(context);

            context.Advance(1f);
            cooldown.Tick(context);
            Assert.AreEqual(1, child.Ticks);
        }

        [Test]
        public void WhileTheChildRuns_NoCooldownStarts()
        {
            var child = new ScriptedNode("ambush", NodeStatus.Running);
            var cooldown = new Cooldown("cd", Seconds, child);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(NodeStatus.Running, cooldown.Tick(context));
                context.Advance(1f);
            }

            Assert.AreEqual(3, child.Ticks);
            Assert.IsFalse(cooldown.IsCoolingDown(context.Time));
        }

        [Test]
        public void CooldownStartsWhenTheChildFinishes_NotWhenItStarted()
        {
            var child = new ScriptedNode("ambush", NodeStatus.Running);
            var cooldown = new Cooldown("cd", Seconds, child);
            cooldown.Tick(context);
            context.Advance(4f);
            child.Result = NodeStatus.Success;
            cooldown.Tick(context);

            Assert.AreEqual(6f, cooldown.ReadyAt);
        }

        [Test]
        public void Reset_ResetsTheChildButKeepsTheCooldown()
        {
            var child = new ScriptedNode("ambush", NodeStatus.Success);
            var cooldown = new Cooldown("cd", Seconds, child);
            cooldown.Tick(context);
            cooldown.Reset();

            Assert.AreEqual(1, child.Resets);
            Assert.AreEqual(NodeStatus.Failure, cooldown.Tick(context), "an interrupt must not let it fire again");
        }

        [Test]
        public void InASelector_TheNextBranchRunsWhileCoolingDown()
        {
            var ambush = new ScriptedNode("ambush", NodeStatus.Success);
            var prowl = new ScriptedNode("prowl", NodeStatus.Running);
            var root = new Selector("root", new Cooldown("cd", Seconds, ambush), prowl);
            root.Tick(context);

            context.Advance(1f);
            Assert.AreEqual(NodeStatus.Running, root.Tick(context));
            Assert.AreEqual(1, ambush.Ticks);
            Assert.AreEqual(1, prowl.Ticks);
        }

        [Test]
        public void ZeroSeconds_IsReadyOnTheNextTick()
        {
            var child = new ScriptedNode("ambush", NodeStatus.Success);
            var cooldown = new Cooldown("cd", 0f, child);
            cooldown.Tick(context);
            cooldown.Tick(context);

            Assert.AreEqual(2, child.Ticks);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void BadDuration_Throws(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Cooldown("cd", seconds, new ScriptedNode("c", NodeStatus.Success)));
        }
    }
}
