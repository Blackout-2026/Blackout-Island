using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>One post-processing look: the overrides Setup Post Processing writes to a Volume profile.</summary>
    public struct PostLook
    {
        public float PostExposure;
        public float Contrast;
        public float Saturation;
        public float BloomThreshold;
        public float BloomIntensity;
        public float BloomScatter;
        public int BloomMaxIterations;
        public float VignetteIntensity;
        public float VignetteSmoothness;
        public float GrainIntensity;
        public float GrainResponse;
    }

    /// <summary>
    /// Numbers for Blackout/World/Setup Post Processing (W9), from the prototype's composite pass (src/core/engine.js):
    /// exposure 1.0 into three.js's ACES fit, UnrealBloomPass(0.75, 0.5, 0.85), vignette 0.55 and film grain 0.035. The
    /// prototype's post chain is the same before and after the blackout (docs/reference/world-builder.md section 4).
    /// URP's effects are built differently, so each value is converted, not copied:
    /// - Tonemapping: three.js scales the colour by exposure / 0.6 before its ACES fit; URP's ACES has no such scale, so
    ///   Post Exposure is log2(1 / 0.6) = +0.74 EV.
    /// - Bloom threshold: three.js compares linear luminance with 0.85; URP's threshold is a gamma-space value, so 0.85 is
    ///   converted to 0.93.
    /// - Bloom intensity: three.js adds its 5 blurred mips, each weighted mix(f, 1.2 - f, radius) = 0.6 at radius 0.5,
    ///   times the strength: 0.75 × 3.0 = 2.25 times the bright pass. URP's mip chain adds up to 1, so Intensity 2.25.
    /// - Vignette: fitted to the prototype's curve (see <see cref="PrototypeVignetteFactor"/>).
    /// - Film grain: the prototype adds ±0.0175 to the sRGB value, about ±7.7 % of mid-grey's light. URP multiplies the
    ///   colour by grain × intensity × 0.63 at mid-grey (Response 0.8), so Intensity 0.12.
    /// The Blackout look is a Unity addition (the brief asks for two profiles): the same chain with darker corners, a
    /// little more contrast and more grain. The red still comes from the lights.
    /// docs/level-layout.md lists these numbers too: if you change one here, change it there.
    /// </summary>
    public static class PostProcessLayout
    {
        public const string VolumesFolder = "Assets/Environment/Volumes";
        public const string PoweredProfilePath = VolumesFolder + "/Powered.asset";
        public const string BlackoutProfilePath = VolumesFolder + "/Blackout.asset";
        public const string VolumesRootName = "Volumes";

        /// <summary>Volumes_Powered: always on, under the Blackout volume.</summary>
        public const string PoweredPart = "Powered";

        /// <summary>Volumes_Blackout: one priority higher, its weight driven by PowerStateLighting.</summary>
        public const string BlackoutPart = "Blackout";

        public const float PoweredPriority = 0f;
        public const float BlackoutPriority = 1f;

        // ---- The prototype's composite pass (engine.js) -----------------------------------------------------------
        public const float PrototypeExposure = 1f;

        /// <summary>three.js r128's ACES: <c>c *= exposure / 0.6</c> before the fit.</summary>
        public const float PrototypeAcesScale = 0.6f;

        public const float PrototypeBloomStrength = 0.75f;
        public const float PrototypeBloomRadius = 0.5f;

        /// <summary>Linear luminance: the bright pass keeps a whole pixel brighter than this.</summary>
        public const float PrototypeBloomThreshold = 0.85f;

        /// <summary>UnrealBloomPass's weight per mip before the radius mix, smallest blur first.</summary>
        public static readonly float[] PrototypeBloomFactors = { 1f, 0.8f, 0.6f, 0.4f, 0.2f };

        /// <summary><c>c *= mix(1, smoothstep(1.05, 0.25, r), 0.55)</c>, on the sRGB colour.</summary>
        public const float PrototypeVignette = 0.55f;
        public const float PrototypeVignetteOuter = 1.05f;
        public const float PrototypeVignetteInner = 0.25f;

        /// <summary><c>c += (hash - 0.5) * 0.035</c>, on the sRGB colour.</summary>
        public const float PrototypeGrain = 0.035f;

        // ---- URP values -------------------------------------------------------------------------------------------

        /// <summary>URP's default spread: the glow is shared over the mips, as the prototype's radius 0.5 shares it equally.</summary>
        public const float BloomScatter = 0.7f;

        /// <summary>The prototype blurs 5 mips.</summary>
        public const int BloomMaxIterations = 5;

        /// <summary>
        /// Fitted: matches the prototype's corners (17 % of the light) and top edge (74 %) within 0.01. In between, URP's
        /// curve is up to 0.12 lighter; no setting matches both curves everywhere.
        /// </summary>
        public const float VignetteIntensity = 0.25f;
        public const float VignetteSmoothness = 0.4f;

        public const float GrainIntensity = 0.12f;

        /// <summary>URP's default: the grain fades on bright pixels.</summary>
        public const float GrainResponse = 0.8f;

        // ---- Blackout additions -----------------------------------------------------------------------------------

        /// <summary>The corners keep 10 % of their light instead of 17 %: the bunker closes in.</summary>
        public const float BlackoutVignetteIntensity = 0.27f;

        /// <summary>Deeper shadows between the red pools of the emergency lights.</summary>
        public const float BlackoutContrast = 10f;

        /// <summary>Low light shows more grain, as on a camera at high ISO.</summary>
        public const float BlackoutGrainIntensity = 0.2f;

        /// <summary>log2(1 / 0.6) = 0.74 EV: three.js's brighter ACES input.</summary>
        public static float PostExposure => Mathf.Log(PrototypeExposure / PrototypeAcesScale, 2f);

        /// <summary>The prototype's linear threshold, as the gamma-space value URP expects: 0.93.</summary>
        public static float BloomThreshold => Mathf.LinearToGammaSpace(PrototypeBloomThreshold);

        /// <summary>The sum of UnrealBloomPass's mip weights at the prototype's radius: 5 × 0.6 = 3.0.</summary>
        public static float BloomMipWeightSum()
        {
            float sum = 0f;
            foreach (float factor in PrototypeBloomFactors)
            {
                sum += Mathf.Lerp(factor, 1.2f - factor, PrototypeBloomRadius);
            }

            return sum;
        }

        /// <summary>2.25: the prototype's bloom energy, strength × the mip weights.</summary>
        public static float BloomIntensity => PrototypeBloomStrength * BloomMipWeightSum();

        public static PostLook Powered => new PostLook
        {
            PostExposure = PostExposure,
            Contrast = 0f,
            Saturation = 0f,
            BloomThreshold = BloomThreshold,
            BloomIntensity = BloomIntensity,
            BloomScatter = BloomScatter,
            BloomMaxIterations = BloomMaxIterations,
            VignetteIntensity = VignetteIntensity,
            VignetteSmoothness = VignetteSmoothness,
            GrainIntensity = GrainIntensity,
            GrainResponse = GrainResponse,
        };

        public static PostLook Blackout
        {
            get
            {
                PostLook look = Powered;
                look.VignetteIntensity = BlackoutVignetteIntensity;
                look.Contrast = BlackoutContrast;
                look.GrainIntensity = BlackoutGrainIntensity;
                return look;
            }
        }

        // ---- Vignette fit -----------------------------------------------------------------------------------------

        /// <summary>
        /// Distance from the screen centre as both vignettes measure it: the x offset is stretched by the aspect ratio, so
        /// the shape is round (URP's Rounded option). A 16:9 corner is at 1.02, the top edge at 0.5.
        /// </summary>
        public static float ScreenRadius(Vector2 uv, float aspect)
        {
            return new Vector2((uv.x - 0.5f) * aspect, uv.y - 0.5f).magnitude;
        }

        /// <summary>
        /// The prototype's vignette at radius r, as a factor on linear light. It multiplies the sRGB colour, so the
        /// light is multiplied by about the factor to the power 2.2.
        /// </summary>
        public static float PrototypeVignetteFactor(float radius)
        {
            float t = Mathf.Clamp01((radius - PrototypeVignetteOuter) / (PrototypeVignetteInner - PrototypeVignetteOuter));
            float smooth = t * t * (3f - 2f * t); // GLSL smoothstep(1.05, 0.25, r)
            float srgb = Mathf.Lerp(1f, smooth, PrototypeVignette);
            return Mathf.Pow(srgb, 2.2f);
        }

        /// <summary>
        /// URP's rounded vignette at radius r (Vignette in URP's post-processing shaders): the offset is scaled by
        /// intensity × 3 and the falloff raised to smoothness × 5.
        /// </summary>
        public static float UrpVignetteFactor(float radius, float intensity, float smoothness)
        {
            float d = radius * intensity * 3f;
            return Mathf.Pow(Mathf.Clamp01(1f - d * d), smoothness * 5f);
        }
    }
}
