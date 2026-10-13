using NUnit.Framework;
using UnityEngine;
using State = Blackout.Environment.LightingStateProfile.State;

namespace Blackout.Environment.Tests
{
    public class LightingStateProfileTests
    {
        private const float Tolerance = 1e-4f;

        private LightingStateProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<LightingStateProfile>();
            profile.powered = Powered();
            profile.blackout = Blackout();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(profile);
        }

        // ---- Interpolation ----------------------------------------------------------------------------------------

        [Test]
        public void Evaluate_GivesEachStateAtItsEnd()
        {
            AssertStates(profile.Evaluate(0f), Powered());
            AssertStates(profile.Evaluate(1f), Blackout());
        }

        [Test]
        public void Evaluate_ClampsTheBlendToZeroToOne()
        {
            AssertStates(profile.Evaluate(-2f), Powered());
            AssertStates(profile.Evaluate(3f), Blackout());
        }

        [Test]
        public void Lerp_MixesNumbersHalfway()
        {
            State half = profile.Evaluate(0.5f);
            Assert.That(half.fogDensity, Is.EqualTo((0.0135f + 0.05f) * 0.5f).Within(Tolerance));
            Assert.That(half.directionalIntensity, Is.EqualTo(0.45f).Within(Tolerance));
            Assert.That(half.reflectionIntensity, Is.EqualTo(0.6f).Within(Tolerance));
        }

        [Test]
        public void Lerp_MixesColoursInLinearSpace()
        {
            // Halfway between black and white is 0.5 in linear light, which is about 0.735 as an sRGB value.
            State black = default;
            black.fogColor = Color.black;
            State white = default;
            white.fogColor = Color.white;

            Color half = State.Lerp(black, white, 0.5f).fogColor;
            Assert.That(half.linear.r, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(half.r, Is.EqualTo(Mathf.LinearToGammaSpace(0.5f)).Within(1e-3f));
            Assert.That(half.r, Is.GreaterThan(0.7f), "an sRGB mix would give 0.5, darker than the light really is");
        }

        [Test]
        public void Lerp_MovesEveryValueSteadilyFromPoweredToBlackout()
        {
            State previous = profile.Evaluate(0f);
            for (float t = 0.1f; t <= 1.0001f; t += 0.1f)
            {
                State next = profile.Evaluate(t);
                Assert.That(next.fogDensity, Is.GreaterThan(previous.fogDensity), $"t = {t}");
                Assert.That(next.directionalIntensity, Is.LessThan(previous.directionalIntensity), $"t = {t}");
                Assert.That(next.fogColor.linear.r, Is.GreaterThan(previous.fogColor.linear.r - Tolerance), $"t = {t}");
                Assert.That(next.ambientSky.linear.b, Is.LessThan(previous.ambientSky.linear.b), $"t = {t}");
                previous = next;
            }
        }

        // ---- Fog, ambient, moon and reflections over time ---------------------------------------------------------

        [Test]
        public void TransitionAt_RisesFrom0To1OverTwoSeconds()
        {
            Assert.That(profile.TransitionAt(0f), Is.EqualTo(0f));
            Assert.That(profile.TransitionAt(1f), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(profile.TransitionAt(2f), Is.EqualTo(1f));
            Assert.That(profile.TransitionAt(5f), Is.EqualTo(1f));

            float previous = 0f;
            for (float s = 0.05f; s <= 2f; s += 0.05f)
            {
                float next = profile.TransitionAt(s);
                Assert.That(next, Is.GreaterThan(previous), $"{s} s");
                previous = next;
            }
        }

        // ---- Fixture cascade --------------------------------------------------------------------------------------

        [Test]
        public void FixtureOffTime_EveryFixtureIsOutWithinTheCascade()
        {
            const float farthest = 53f;
            for (int i = 0; i < 31; i++)
            {
                for (float d = 0f; d <= farthest; d += 1f)
                {
                    float off = profile.FixtureOffTime(d, farthest, i);
                    Assert.That(off, Is.InRange(profile.strobeSeconds, profile.cascadeSeconds), $"fixture {i} at {d} m");
                }
            }
        }

        [Test]
        public void FixtureOffTime_NearerFixturesGoFirst()
        {
            const float farthest = 53f;
            for (int i = 0; i < 31; i++)
            {
                Assert.That(profile.FixtureOffTime(10f, farthest, i), Is.LessThan(profile.FixtureOffTime(40f, farthest, i)), $"fixture {i}");
            }

            // Fixtures more than the jitter's worth of distance apart always go in order (1.08 s spread over 53 m).
            float jitterDistance = profile.jitterSeconds / (profile.cascadeSeconds - profile.strobeSeconds - profile.jitterSeconds) * farthest;
            Assert.That(profile.FixtureOffTime(20f, farthest, 1), Is.LessThan(profile.FixtureOffTime(20f + jitterDistance + 0.1f, farthest, 2)));
        }

        [Test]
        public void FixtureOffTime_IsRepeatableAndDiffersBetweenFixtures()
        {
            Assert.That(profile.FixtureOffTime(25f, 53f, 4), Is.EqualTo(profile.FixtureOffTime(25f, 53f, 4)));

            var distinct = new System.Collections.Generic.HashSet<float>();
            for (int i = 0; i < 31; i++)
            {
                distinct.Add(profile.FixtureOffTime(25f, 53f, i));
            }

            Assert.That(distinct.Count, Is.GreaterThan(25), "fixtures at the same distance should not all die together");
        }

        [Test]
        public void FixtureLevel_IsOnThenStrobesThenOff()
        {
            const float offTime = 1.2f;
            bool sawHigh = false;
            bool sawLow = false;
            for (float s = 0f; s < 2f; s += 1f / 240f)
            {
                float level = profile.FixtureLevel(s, offTime, 7);
                if (s < offTime - profile.strobeSeconds)
                {
                    Assert.That(level, Is.EqualTo(1f), $"{s} s");
                }
                else if (s < offTime)
                {
                    Assert.That(level == profile.strobeHigh || level == profile.strobeLow, Is.True, $"{s} s: {level}");
                    sawHigh |= level == profile.strobeHigh;
                    sawLow |= level == profile.strobeLow;
                }
                else
                {
                    Assert.That(level, Is.EqualTo(0f), $"{s} s");
                }
            }

            Assert.That(sawHigh && sawLow, Is.True, "the strobe should flash and drop");
        }

        [Test]
        public void ScreenLevel_FadesOutOverScreenFadeSeconds()
        {
            Assert.That(profile.ScreenLevel(0f), Is.EqualTo(1f));
            Assert.That(profile.ScreenLevel(0.2f), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(profile.ScreenLevel(0.4f), Is.EqualTo(0f));
            Assert.That(profile.ScreenLevel(3f), Is.EqualTo(0f));
        }

        // ---- Emergency lights and alarms --------------------------------------------------------------------------

        [Test]
        public void EmergencyLevel_StartsAfterTheCascadeAndFadesIn()
        {
            Assert.That(profile.emergencyDelaySeconds, Is.GreaterThan(profile.cascadeSeconds), "a moment of darkness first");
            Assert.That(profile.EmergencyLevel(0f), Is.EqualTo(0f));
            Assert.That(profile.EmergencyLevel(profile.emergencyDelaySeconds), Is.EqualTo(0f));
            Assert.That(profile.EmergencyLevel(profile.emergencyDelaySeconds + profile.emergencyFadeSeconds * 0.5f), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(profile.EmergencyLevel(profile.emergencyDelaySeconds + profile.emergencyFadeSeconds), Is.EqualTo(1f));
            Assert.That(profile.EmergencyLevel(60f), Is.EqualTo(1f));
        }

        [Test]
        public void EmergencyPulse_StaysBetweenHalfAndFull()
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (float s = 0f; s < 10f; s += 0.01f)
            {
                float pulse = profile.EmergencyPulse(s);
                min = Mathf.Min(min, pulse);
                max = Mathf.Max(max, pulse);
            }

            Assert.That(min, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(max, Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void AlarmAngle_TurnsOnlyOnceTheEmergencyLightsStart()
        {
            Assert.That(profile.AlarmAngle(0f), Is.EqualTo(0f));
            Assert.That(profile.AlarmAngle(profile.emergencyDelaySeconds), Is.EqualTo(0f));
            Assert.That(profile.AlarmAngle(profile.emergencyDelaySeconds + 1f), Is.EqualTo(206.3f).Within(Tolerance), "3.6 rad/s");
        }

        [Test]
        public void AlarmDomePulse_IsTheReferences2Point5PlusOrMinus0Point8()
        {
            for (float s = 0f; s < 5f; s += 0.01f)
            {
                Assert.That(profile.AlarmDomePulse(s) * 2.5f, Is.InRange(1.7f - 1e-3f, 3.3f + 1e-3f), $"{s} s");
            }
        }

        // ---- Helpers ----------------------------------------------------------------------------------------------

        // About what Setup Blackout Lighting writes: the W7 scene values, and the reference's bunker blackout values
        // (fog #0B0203, hemisphere #3A0907 / #060202 x 0.2) converted from linear to sRGB.
        private static State Powered()
        {
            return new State
            {
                fogColor = Hex("#3B4B63"),
                fogDensity = 0.0135f,
                ambientSky = Hex("#8998B1"),
                ambientEquator = Hex("#6E798D"),
                ambientGround = Hex("#464D58"),
                directionalIntensity = 0.9f,
                reflectionIntensity = 1f,
            };
        }

        private static State Blackout()
        {
            return new State
            {
                fogColor = Hex("#3B161C"),
                fogDensity = 0.05f,
                ambientSky = Hex("#3C1411"),
                ambientEquator = Hex("#2C0E0C"),
                ambientGround = Hex("#0F0505"),
                directionalIntensity = 0f,
                reflectionIntensity = 0.2f,
            };
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color colour);
            return colour;
        }

        private static void AssertStates(State actual, State expected)
        {
            AssertColour(actual.fogColor, expected.fogColor, "fog colour");
            Assert.That(actual.fogDensity, Is.EqualTo(expected.fogDensity).Within(Tolerance), "fog density");
            AssertColour(actual.ambientSky, expected.ambientSky, "ambient sky");
            AssertColour(actual.ambientEquator, expected.ambientEquator, "ambient equator");
            AssertColour(actual.ambientGround, expected.ambientGround, "ambient ground");
            Assert.That(actual.directionalIntensity, Is.EqualTo(expected.directionalIntensity).Within(Tolerance), "moon");
            Assert.That(actual.reflectionIntensity, Is.EqualTo(expected.reflectionIntensity).Within(Tolerance), "reflections");
        }

        private static void AssertColour(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-3f), what + " r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-3f), what + " g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-3f), what + " b");
        }
    }
}
