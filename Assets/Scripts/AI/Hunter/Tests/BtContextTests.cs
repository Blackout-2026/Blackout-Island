// Owner: Systems Engineer (Hunter AI).
using System;
using NUnit.Framework;

namespace Blackout.AI.Hunter.Tests
{
    public class BtContextTests
    {
        [Test]
        public void Advance_AddsUpTime()
        {
            var context = new BtContext();
            context.Advance(0.1f);
            context.Advance(0.25f);

            Assert.AreEqual(0.35f, context.Time, 1e-6f);
            Assert.AreEqual(0.25f, context.DeltaTime);
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Advance_BadStep_CountsAsZero(float step)
        {
            var context = new BtContext();
            context.Advance(1f);
            context.Advance(step);

            Assert.AreEqual(1f, context.Time);
            Assert.AreEqual(0f, context.DeltaTime);
        }

        [Test]
        public void Blackboard_IsTheOneGiven()
        {
            var blackboard = new LocalBlackboard();

            Assert.AreSame(blackboard, new BtContext(blackboard).Blackboard);
            Assert.IsNotNull(new BtContext().Blackboard);
        }

        [Test]
        public void NullBlackboard_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new BtContext(null));
        }
    }
}
