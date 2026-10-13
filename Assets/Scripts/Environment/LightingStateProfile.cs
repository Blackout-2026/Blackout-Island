using System;
using UnityEngine;

namespace Blackout.Environment
{
    /// <summary>
    /// The bunker's two lighting moods, powered and blackout (docs/reference/world-builder.md, section 4), and the
    /// timing of the switch. <see cref="PowerStateLighting"/> plays it when the generator goes off.
    /// - <see cref="powered"/> and <see cref="blackout"/>: fog, ambient, the moon and the bunker's reflections.
    ///   Blackout/World/Setup Blackout Lighting writes them, and the two lamp emission colours, from the reference on
    ///   every run. The powered state is the W7 scene's values.
    /// - The timings below default to the W8 brief and the prototype. Tune them on the asset: the tool leaves them alone.
    /// Every method is a pure function of the seconds since the event, so EditMode tests can check the whole sequence.
    /// Owner: World Builder.
    /// </summary>
    [CreateAssetMenu(menuName = "Blackout/World/Lighting State Profile", fileName = "LightingStateProfile")]
    public class LightingStateProfile : ScriptableObject
    {
        /// <summary>The global lighting values that change with the power.</summary>
        [Serializable]
        public struct State
        {
            [Tooltip("Exponential squared fog.")]
            public Color fogColor;
            public float fogDensity;

            [Tooltip("Three-colour (Trilight) ambient: from above, from the side and from below.")]
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;

            [Tooltip("Intensity of the moon, the scene's directional light.")]
            public float directionalIntensity;

            [Tooltip("Intensity of the bunker's reflection probes, which were captured with the lights on.")]
            public float reflectionIntensity;

            /// <summary>
            /// The state a fraction <paramref name="t"/> (clamped to 0..1) of the way from <paramref name="a"/> to
            /// <paramref name="b"/>. Colours are mixed in linear space, where light adds up, so the halfway colour is
            /// not darker than both ends.
            /// </summary>
            public static State Lerp(State a, State b, float t)
            {
                t = Mathf.Clamp01(t);
                return new State
                {
                    fogColor = LerpLinear(a.fogColor, b.fogColor, t),
                    fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
                    ambientSky = LerpLinear(a.ambientSky, b.ambientSky, t),
                    ambientEquator = LerpLinear(a.ambientEquator, b.ambientEquator, t),
                    ambientGround = LerpLinear(a.ambientGround, b.ambientGround, t),
                    directionalIntensity = Mathf.Lerp(a.directionalIntensity, b.directionalIntensity, t),
                    reflectionIntensity = Mathf.Lerp(a.reflectionIntensity, b.reflectionIntensity, t),
                };
            }

            // Colour fields are sRGB: convert to linear, mix, and convert back.
            private static Color LerpLinear(Color a, Color b, float t)
            {
                return Color.Lerp(a.linear, b.linear, t).gamma;
            }
        }

        // Keeps a fixture's jitter apart from the strobe noise, which uses the fixture's index as its seed.
        private const int JitterSeed = 8;

        [Header("The two states (written by Setup Blackout Lighting)")]
        public State powered;
        public State blackout;

        [Header("Powered fixtures: a cascade outward from the generator")]
        [Tooltip("Seconds from the event until the farthest fixture is out.")]
        [Min(0.1f)] public float cascadeSeconds = 1.5f;

        [Tooltip("Each fixture strobes for this long before it dies.")]
        [Min(0f)] public float strobeSeconds = 0.3f;

        [Tooltip("Up to this much extra delay per fixture, so fixtures at the same distance do not die together.")]
        [Min(0f)] public float jitterSeconds = 0.12f;

        [Tooltip("During the strobe each 1/30 s step is at the high level (a flash) or the low one.")]
        public float strobeHigh = 1.4f;
        public float strobeLow = 0.05f;
        [Range(0f, 1f)] public float strobeHighChance = 0.5f;

        [Tooltip("Screens fade out over this long, from the event.")]
        [Min(0.01f)] public float screenFadeSeconds = 0.4f;

        [Header("Fog, ambient, moon and reflections")]
        [Tooltip("Seconds to go from the powered state to the blackout state, from the event.")]
        [Min(0.01f)] public float transitionSeconds = 2f;

        [Header("Emergency lights")]
        [Tooltip("Seconds from the event until they start to fade in: after the cascade, with a moment of darkness.")]
        [Min(0f)] public float emergencyDelaySeconds = 2f;

        [Min(0.01f)] public float emergencyFadeSeconds = 1.2f;

        [Tooltip("The lights pulse between 1 - 2 x depth and 1.")]
        [Range(0f, 0.5f)] public float emergencyPulseDepth = 0.25f;

        [Tooltip("Radians per second.")]
        public float emergencyPulseSpeed = 3.2f;

        [Tooltip("A lamp's emission at full strength (written by Setup Blackout Lighting). Its material holds the unlit glow.")]
        [ColorUsage(false, true)] public Color emergencyLampEmission;

        [Header("Alarm beacons (they start with the emergency lights)")]
        public float alarmDegreesPerSecond = 206.3f;

        [Tooltip("The dome's emission at full strength (written by Setup Blackout Lighting).")]
        [ColorUsage(false, true)] public Color alarmDomeEmission;

        [Tooltip("The dome pulses between 1 - depth and 1 + depth.")]
        [Range(0f, 1f)] public float alarmDomePulseDepth = 0.32f;

        [Tooltip("Radians per second.")]
        public float alarmDomePulseSpeed = 7.2f;

        /// <summary>The global state at <paramref name="blend"/>: 0 is powered, 1 is blackout.</summary>
        public State Evaluate(float blend)
        {
            return State.Lerp(powered, blackout, blend);
        }

        /// <summary>
        /// How far fog, ambient, moon and reflections have gone toward the blackout, <paramref name="seconds"/> after the
        /// event: an S-curve from 0 to 1 over <see cref="transitionSeconds"/>, so it starts and settles gently.
        /// </summary>
        public float TransitionAt(float seconds)
        {
            return Mathf.SmoothStep(0f, 1f, seconds / transitionSeconds);
        }

        /// <summary>
        /// When a powered fixture goes out, in seconds after the event. The wave starts at the generator: a fixture
        /// <paramref name="distance"/> metres from it dies after its strobe plus its share of the cascade, so the farthest
        /// one (<paramref name="farthest"/> metres) is out by <see cref="cascadeSeconds"/>. A repeatable jitter from
        /// <paramref name="index"/> splits fixtures at the same distance.
        /// </summary>
        public float FixtureOffTime(float distance, float farthest, int index)
        {
            float spread = Mathf.Max(0f, cascadeSeconds - strobeSeconds - jitterSeconds);
            float share = farthest > 0f ? Mathf.Clamp01(distance / farthest) : 0f;
            return strobeSeconds + spread * share + jitterSeconds * FlickerLight.Hash01(index, JitterSeed);
        }

        /// <summary>
        /// A powered fixture's level <paramref name="seconds"/> after the event: 1 until its strobe starts, then
        /// <see cref="strobeHigh"/> or <see cref="strobeLow"/> per 1/30 s step (repeatable for this
        /// <paramref name="index"/>), then 0 from <paramref name="offTime"/>.
        /// </summary>
        public float FixtureLevel(float seconds, float offTime, int index)
        {
            if (seconds >= offTime)
            {
                return 0f;
            }

            if (seconds < offTime - strobeSeconds)
            {
                return 1f;
            }

            float noise = FlickerLight.Hash01(Mathf.FloorToInt(seconds * FlickerLight.NoiseStepsPerSecond), index);
            return noise < strobeHighChance ? strobeHigh : strobeLow;
        }

        /// <summary>The screens' emission: from 1 down to 0 over <see cref="screenFadeSeconds"/> after the event.</summary>
        public float ScreenLevel(float seconds)
        {
            return 1f - Mathf.Clamp01(seconds / screenFadeSeconds);
        }

        /// <summary>
        /// How far the emergency lights and alarms have faded in: 0 until <see cref="emergencyDelaySeconds"/>, then up
        /// to 1 over <see cref="emergencyFadeSeconds"/>.
        /// </summary>
        public float EmergencyLevel(float seconds)
        {
            return Mathf.Clamp01((seconds - emergencyDelaySeconds) / emergencyFadeSeconds);
        }

        /// <summary>The emergency lights' pulse: 0.75 + 0.25 sin(3.2 t) with the defaults, so between 0.5 and 1.</summary>
        public float EmergencyPulse(float seconds)
        {
            return 1f - emergencyPulseDepth + emergencyPulseDepth * Mathf.Sin(seconds * emergencyPulseSpeed);
        }

        /// <summary>How far the alarm beacons have turned, in degrees: they spin from the moment the emergency lights start.</summary>
        public float AlarmAngle(float seconds)
        {
            return alarmDegreesPerSecond * Mathf.Max(0f, seconds - emergencyDelaySeconds);
        }

        /// <summary>The alarm domes' pulse: 2.5 +- 0.8 in the prototype, so 1 +- 0.32 times the full emission.</summary>
        public float AlarmDomePulse(float seconds)
        {
            return 1f + alarmDomePulseDepth * Mathf.Sin(seconds * alarmDomePulseSpeed);
        }
    }
}
