using NUnit.Framework;

namespace Blackout.Interaction.Tests
{
    public class HoldTimerTests
    {
        private const float Tolerance = 0.001f;
        private const float Frame = 1f / 60f;
        private const float GeneratorHold = 2f;

        [Test]
        public void Hold_CompletesOnceAfterTwoSeconds()
        {
            var timer = new HoldTimer();
            int completions = 0;
            int frames = 0;
            while (completions == 0 && frames < 1000)
            {
                if (timer.Update(true, GeneratorHold, Frame))
                {
                    completions++;
                }

                frames++;
            }

            Assert.AreEqual(1, completions);
            Assert.AreEqual(2f, frames * Frame, 0.02f, "about 120 frames at 60 fps");
        }

        [Test]
        public void Hold_NotCompleteJustBeforeTwoSeconds()
        {
            var timer = new HoldTimer();
            Assert.IsFalse(timer.Update(true, GeneratorHold, 1.99f));
            Assert.AreEqual(0.995f, timer.Fraction(GeneratorHold), Tolerance);
        }

        [Test]
        public void Release_ResetsProgress()
        {
            var timer = new HoldTimer();
            timer.Update(true, GeneratorHold, 1.5f);
            timer.Update(false, GeneratorHold, Frame);
            Assert.AreEqual(0f, timer.Elapsed);

            Assert.IsFalse(timer.Update(true, GeneratorHold, 1f), "must start again from zero");
        }

        [Test]
        public void KeepHolding_AfterCompleting_DoesNotFireAgainUntilReleased()
        {
            var timer = new HoldTimer();
            Assert.IsTrue(timer.Update(true, GeneratorHold, 2f));
            Assert.IsFalse(timer.Update(true, GeneratorHold, 5f));

            timer.Update(false, GeneratorHold, Frame);
            Assert.IsTrue(timer.Update(true, GeneratorHold, 2f));
        }

        [Test]
        public void Reset_NewTarget_StartsFromZero()
        {
            var timer = new HoldTimer();
            timer.Update(true, GeneratorHold, 1.2f);
            timer.Reset();
            Assert.AreEqual(0f, timer.Fraction(GeneratorHold));
        }
    }
}
