using NUnit.Framework;
using UnityEngine;
using Pattern = Blackout.Environment.FlickerLight.Pattern;

namespace Blackout.Environment.Tests
{
    public class FlickerLightTests
    {
        private const float SampleStep = 1f / 240f; // finer than the 1/30 s noise steps
        private const float Tolerance = 1e-5f; // float rounding at the ends of a range

        [Test]
        public void Evaluate_IsTheSameForTheSameTimeAndSeed()
        {
            for (float t = 0f; t < 20f; t += 0.37f)
            {
                Assert.That(FlickerLight.Evaluate(Pattern.FaultyTube, t, 3), Is.EqualTo(FlickerLight.Evaluate(Pattern.FaultyTube, t, 3)));
                Assert.That(FlickerLight.Evaluate(Pattern.Flame, t, 3), Is.EqualTo(FlickerLight.Evaluate(Pattern.Flame, t, 3)));
            }
        }

        [Test]
        public void FaultyTube_HumsBetween95And99PercentOutsideTheFault()
        {
            float faultEnd = FlickerLight.StrobeSeconds + FlickerLight.OutSeconds;
            for (float t = faultEnd + 0.001f; t < FlickerLight.CycleSeconds; t += SampleStep)
            {
                float level = FlickerLight.Evaluate(Pattern.FaultyTube, t, 0);
                Assert.That(level, Is.InRange(0.95f - Tolerance, 0.99f + Tolerance), $"t = {t}");
            }
        }

        [Test]
        public void FaultyTube_StrobesThenGoesOutAtTheStartOfEachCycle()
        {
            bool sawOn = false;
            bool sawLow = false;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                float start = cycle * FlickerLight.CycleSeconds;
                for (float p = 0.001f; p < FlickerLight.StrobeSeconds - 0.001f; p += SampleStep)
                {
                    float level = FlickerLight.Evaluate(Pattern.FaultyTube, start + p, 0);
                    Assert.That(level == 1f || level == FlickerLight.StrobeLow, Is.True, $"t = {start + p}: {level}");
                    sawOn |= level == 1f;
                    sawLow |= level == FlickerLight.StrobeLow;
                }

                for (float p = FlickerLight.StrobeSeconds + 0.001f; p < FlickerLight.StrobeSeconds + FlickerLight.OutSeconds - 0.001f; p += SampleStep)
                {
                    Assert.That(FlickerLight.Evaluate(Pattern.FaultyTube, start + p, 0), Is.EqualTo(FlickerLight.OutLevel));
                }
            }

            Assert.That(sawOn && sawLow, Is.True, "the strobe should switch between on and nearly off");
        }

        [Test]
        public void FaultyTube_IsOnAbout55PercentOfTheStrobe()
        {
            // 18 noise steps per strobe over 1000 cycles.
            int on = 0;
            int total = 0;
            for (int cycle = 0; cycle < 1000; cycle++)
            {
                for (int i = 0; i < 18; i++)
                {
                    float t = cycle * FlickerLight.CycleSeconds + (i + 0.5f) / FlickerLight.NoiseStepsPerSecond;
                    on += FlickerLight.Evaluate(Pattern.FaultyTube, t, 0) == 1f ? 1 : 0;
                    total++;
                }
            }

            Assert.That((float)on / total, Is.InRange(0.52f, 0.58f));
        }

        [Test]
        public void Flame_StaysBetween71And99Percent()
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (float t = 0f; t < 60f; t += SampleStep)
            {
                float level = FlickerLight.Evaluate(Pattern.Flame, t, 5);
                min = Mathf.Min(min, level);
                max = Mathf.Max(max, level);
            }

            Assert.That(min, Is.GreaterThanOrEqualTo(0.71f - Tolerance));
            Assert.That(max, Is.LessThanOrEqualTo(0.99f + Tolerance));
            Assert.That(max - min, Is.GreaterThan(0.15f), "a flame should visibly move");
        }

        [Test]
        public void Seed_MovesTheFaultToAnotherTime()
        {
            // Seed 1 runs 2.3 s ahead: at t = 0.65 s seed 0 is out and seed 1 is humming; seed 1 is out 2.3 s earlier.
            Assert.That(FlickerLight.Evaluate(Pattern.FaultyTube, 0.65f, 0), Is.EqualTo(FlickerLight.OutLevel));
            Assert.That(FlickerLight.Evaluate(Pattern.FaultyTube, 0.65f, 1), Is.InRange(0.95f - Tolerance, 0.99f + Tolerance));
            Assert.That(FlickerLight.Evaluate(Pattern.FaultyTube, 7f - 2.3f + 0.65f, 1), Is.EqualTo(FlickerLight.OutLevel));
        }

        [Test]
        public void Hash01_IsInTheUnitRangeAndChangesWithItsInputs()
        {
            for (int step = -50; step < 50; step++)
            {
                float value = FlickerLight.Hash01(step, 7);
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }

            Assert.That(FlickerLight.Hash01(10, 1), Is.Not.EqualTo(FlickerLight.Hash01(11, 1)));
            Assert.That(FlickerLight.Hash01(10, 1), Is.Not.EqualTo(FlickerLight.Hash01(10, 2)));
        }
    }
}
