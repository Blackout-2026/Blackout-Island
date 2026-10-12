using UnityEngine;

namespace Blackout.Environment
{
    /// <summary>
    /// Flickers a light, and the emissive lamp mesh that goes with it, like the prototype's two faulty fluorescent
    /// tubes and the boat lantern (docs/reference/world-builder.md, section 4).
    /// - FaultyTube: a 7 s cycle. The tube strobes for 0.6 s (each 1/30 s step is fully on 55 % of the time, else at
    ///   4 %), is out (5 %) for 0.15 s, then hums at 97 % +- 2 % for the rest of the cycle.
    /// - Flame: 85 % +- 10 % from two sine waves, plus +- 4 % noise.
    /// The prototype calls Math.random() every frame. Here the random part is a hash of the time step and the seed, so
    /// the flicker is the same on every run and at any frame rate. Nothing is allocated per frame.
    /// Owner: World Builder. Blackout/World/Setup Lighting adds it to a few lights. Whatever switches the light off
    /// later (the W8 blackout) should disable this component first; disabling it puts the light back to full strength.
    /// </summary>
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public class FlickerLight : MonoBehaviour
    {
        public enum Pattern
        {
            FaultyTube,
            Flame,
        }

        // Faulty tube (prototype: world/bunker_interior.js).
        public const float CycleSeconds = 7f;
        public const float StrobeSeconds = 0.6f;
        public const float OutSeconds = 0.15f;
        public const float StrobeOnChance = 0.55f;
        public const float StrobeLow = 0.04f;
        public const float OutLevel = 0.05f;
        public const float HumLevel = 0.97f;
        public const float HumDepth = 0.02f;
        private const float HumSpeed = 50f; // radians per second

        // Flame (prototype: world/beach.js, the boat lantern).
        public const float FlameLevel = 0.85f;
        public const float FlameWave = 0.05f; // amplitude of each of the two sine waves
        public const float FlameNoise = 0.08f; // full width of the noise, so +- 0.04
        private const float FlameSpeedA = 13f;
        private const float FlameSpeedB = 29.7f;

        /// <summary>New random values per second: the prototype draws one per frame.</summary>
        public const float NoiseStepsPerSecond = 30f;

        /// <summary>Each unit of seed moves the pattern this far in time, so two faulty tubes never strobe together.</summary>
        public const float SeedOffsetSeconds = 2.3f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Pattern pattern = Pattern.FaultyTube;

        [Tooltip("Shifts the pattern in time. Give each flickering light its own seed.")]
        [SerializeField] private int seed;

        [Tooltip("Optional: the lamp mesh whose emission follows the light.")]
        [SerializeField] private Renderer lamp;

        private Light target;
        private float baseIntensity;
        private MaterialPropertyBlock lampBlock;
        private Color baseEmissionLinear;

        public Pattern Mode => pattern;
        public int Seed => seed;
        public Renderer Lamp => lamp;

        /// <summary>Sets the serialized fields. Used by the Editor tool that places the light.</summary>
        public void Configure(Pattern newPattern, int newSeed, Renderer newLamp)
        {
            pattern = newPattern;
            seed = newSeed;
            lamp = newLamp;
        }

        /// <summary>The light's level at this time, as a fraction of its authored intensity.</summary>
        public static float Evaluate(Pattern pattern, float time, int seed)
        {
            float t = time + seed * SeedOffsetSeconds;
            float noise = Hash01(Mathf.FloorToInt(t * NoiseStepsPerSecond), seed);
            if (pattern == Pattern.Flame)
            {
                return FlameLevel + FlameWave * (Mathf.Sin(t * FlameSpeedA) + Mathf.Sin(t * FlameSpeedB))
                    + (noise - 0.5f) * FlameNoise;
            }

            float phase = Mathf.Repeat(t, CycleSeconds);
            if (phase < StrobeSeconds)
            {
                return noise < StrobeOnChance ? 1f : StrobeLow;
            }

            if (phase < StrobeSeconds + OutSeconds)
            {
                return OutLevel;
            }

            return HumLevel + HumDepth * Mathf.Sin(t * HumSpeed);
        }

        /// <summary>
        /// A repeatable "random" number in [0, 1) for this step and seed: an integer hash (Chris Wellons' lowbias32),
        /// so the same inputs always give the same value.
        /// </summary>
        public static float Hash01(int step, int seed)
        {
            unchecked
            {
                uint h = (uint)step * 0x9E3779B1u + (uint)seed * 0x85EBCA77u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (h >> 8) * (1f / 16777216f);
            }
        }

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            if (lamp != null)
            {
                lampBlock = new MaterialPropertyBlock();

                // Colour properties are sRGB; scale the emission in linear space so the lamp dims like the light.
                baseEmissionLinear = lamp.sharedMaterial.GetColor(EmissionColorId).linear;
            }
        }

        private void Update()
        {
            float level = Evaluate(pattern, Time.time, seed);
            target.intensity = baseIntensity * level;
            if (lamp != null)
            {
                lampBlock.SetColor(EmissionColorId, (baseEmissionLinear * level).gamma);
                lamp.SetPropertyBlock(lampBlock);
            }
        }

        private void OnDisable()
        {
            // Leave the light and lamp as they were authored, for whatever takes over (for example the W8 blackout).
            if (target != null)
            {
                target.intensity = baseIntensity;
            }

            if (lamp != null)
            {
                lamp.SetPropertyBlock(null);
            }
        }
    }
}
