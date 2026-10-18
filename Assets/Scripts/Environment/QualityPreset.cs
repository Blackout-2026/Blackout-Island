using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Blackout.Environment
{
    /// <summary>The prototype's three quality presets.</summary>
    public enum QualityLevel
    {
        Low,
        Medium,
        High,
    }

    /// <summary>
    /// Switches the World Builder's part of the quality settings between Low, Medium and High (W9), as the prototype's
    /// presets do:
    /// - Shadow distance on the active URP asset: 40, 50 or 80 m. W11 reduces Medium from W9's 60 m.
    /// - Post-processing on the main camera, with FXAA: off on Low, on Medium and High. On Low the prototype skips its
    ///   whole post chain (bloom, vignette, grain and FXAA).
    /// - Particle counts: the atmosphere particle systems' emission rate and particle cap, × 0.5, 1 or 1.5. The scale
    ///   always applies to the values the systems were built with (Medium), so switching levels never compounds.
    /// The main camera can live in another scene and can be replaced, so it is found by its MainCamera tag
    /// (Camera.main, which Unity caches) and configured once per camera.
    /// The URP asset belongs to the Systems Engineer. The shadow distance is changed only in memory, only in Play mode, and
    /// is put back when this component is destroyed, so the asset file never changes.
    /// Blackout/World/Build Atmosphere Particles adds it to the Volumes root and lists the particle systems. A settings
    /// menu can call <see cref="Apply"/>. Nothing is allocated per frame.
    /// Owner: World Builder.
    /// </summary>
    [DisallowMultipleComponent]
    public class QualityPreset : MonoBehaviour
    {
        // W11: retain the saved PC asset's 50 m reach instead of extending Medium to 60 m at startup.
        public const float MediumShadowDistance = 50f;

        /// <summary>What one level sets.</summary>
        public readonly struct Settings
        {
            public readonly float ShadowDistance;
            public readonly bool PostProcessing;
            public readonly float ParticleScale;

            public Settings(float shadowDistance, bool postProcessing, float particleScale)
            {
                ShadowDistance = shadowDistance;
                PostProcessing = postProcessing;
                ParticleScale = particleScale;
            }
        }

        [Tooltip("Applied in Start. Change it in Play mode from the component's context menu.")]
        [SerializeField] private QualityLevel level = QualityLevel.Medium;

        [Tooltip("The atmosphere particle systems. Their saved emission rate and cap are the Medium values.")]
        [SerializeField] private ParticleSystem[] particles = new ParticleSystem[0];

        private float[] authoredRates;
        private int[] authoredCaps;
        private UniversalRenderPipelineAsset pipeline;
        private float pipelineShadowDistance;
        private Camera configuredCamera;

        /// <summary>The level applied last.</summary>
        public QualityLevel Level => level;

        /// <summary>
        /// The settings of a level.
        /// - Shadow distance: outdoor fog (exponential squared, 0.0135) leaves 75 % visible at 40 m, 63 % at 50 m and
        ///   31 % at 80 m. Medium's shorter reach still needs a visual cutoff check; W11 does not claim an FPS gain.
        /// - Particles: Medium is the prototype's own numbers (steam 7 per second). Low halves them, like the prototype's
        ///   grass (0.35 against 0.7). High is 1.5 times, close to its 1.0 against 0.7.
        /// </summary>
        public static Settings For(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Low:
                    return new Settings(40f, false, 0.5f);
                case QualityLevel.High:
                    return new Settings(80f, true, 1.5f);
                default:
                    return new Settings(MediumShadowDistance, true, 1f);
            }
        }

        /// <summary>A particle cap scaled for a level, never below one particle.</summary>
        public static int ScaledCap(int authored, float scale)
        {
            return Mathf.Max(1, Mathf.RoundToInt(authored * scale));
        }

        /// <summary>Sets the particle systems it scales. Used by the Editor tool that builds them.</summary>
        public void SetParticles(ParticleSystem[] systems)
        {
            particles = systems;
            authoredRates = null; // captured again from the new systems
        }

        /// <summary>
        /// Applies a level. The particles change at once. In Play mode the shadow distance changes too, and the main
        /// camera is configured on the next frame. Outside Play mode the URP asset and the camera are left alone.
        /// </summary>
        public void Apply(QualityLevel newLevel)
        {
            level = newLevel;
            Settings settings = For(level);
            ScaleParticles(settings.ParticleScale);
            if (!Application.isPlaying)
            {
                return;
            }

            SetShadowDistance(settings.ShadowDistance);
            configuredCamera = null; // Update configures the main camera for the new level
        }

        [ContextMenu("Apply Low")]
        private void ApplyLow()
        {
            ApplyFromMenu(QualityLevel.Low);
        }

        [ContextMenu("Apply Medium")]
        private void ApplyMedium()
        {
            ApplyFromMenu(QualityLevel.Medium);
        }

        [ContextMenu("Apply High")]
        private void ApplyHigh()
        {
            ApplyFromMenu(QualityLevel.High);
        }

        private void Start()
        {
            Apply(level);
        }

        private void Update()
        {
            Camera main = Camera.main;
            if (main != null && main != configuredCamera)
            {
                ConfigureCamera(main);
            }
        }

        private void OnDestroy()
        {
            if (pipeline != null)
            {
                pipeline.shadowDistance = pipelineShadowDistance; // leave the Systems Engineer's asset as it was
            }
        }

        private void ScaleParticles(float scale)
        {
            if (authoredRates == null)
            {
                authoredRates = new float[particles.Length];
                authoredCaps = new int[particles.Length];
                for (int i = 0; i < particles.Length; i++)
                {
                    if (particles[i] != null)
                    {
                        authoredRates[i] = particles[i].emission.rateOverTimeMultiplier;
                        authoredCaps[i] = particles[i].main.maxParticles;
                    }
                }
            }

            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = particles[i].emission;
                emission.rateOverTimeMultiplier = authoredRates[i] * scale;
                ParticleSystem.MainModule main = particles[i].main;
                main.maxParticles = ScaledCap(authoredCaps[i], scale);
            }
        }

        private void SetShadowDistance(float distance)
        {
            if (pipeline == null)
            {
                pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (pipeline == null)
                {
                    return;
                }

                pipelineShadowDistance = pipeline.shadowDistance;
            }

            pipeline.shadowDistance = distance;
        }

        // URP's per-camera switch. A camera without URP camera data gets it here, with post-processing off by default.
        private void ConfigureCamera(Camera main)
        {
            configuredCamera = main;
            bool post = For(level).PostProcessing;
            UniversalAdditionalCameraData data = main.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = post;
            data.antialiasing = post ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
        }

        // The context menu also shows outside Play mode, where it would scale the scene's saved particle values.
        private void ApplyFromMenu(QualityLevel newLevel)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[World] QualityPreset: Apply Low / Medium / High work only in Play mode.", this);
                return;
            }

            Apply(newLevel);
        }
    }
}
