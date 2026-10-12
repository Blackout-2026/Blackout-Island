using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Lighting numbers for Blackout/World/Setup Lighting, from docs/reference/world-builder.md section 4 (the "outside"
    /// column of the global values: the player starts on the beach). docs/level-layout.md lists them too: if you change
    /// a number here, change it there.
    /// Colours: the prototype runs three.js r128 without colour management, so its hex colours for lights, fog, ambient
    /// and sky are used as linear values. Unity's colour fields are sRGB, so <see cref="FromLinearHex"/> converts each
    /// one; the light reaching a surface is then the same as in the prototype. (The W5 surface colours are textures and
    /// albedo, and stay sRGB.)
    /// </summary>
    public static class LightingLayout
    {
        // ---- Fog: exponential squared, the same formula as three.js FogExp2, so the density copies across ----------
        public const string FogHex = "#0B1220";
        public const float FogDensity = 0.0135f;

        // ---- Ambient: the prototype's hemisphere light becomes Unity's three-colour (Trilight) ambient -------------
        public const string AmbientSkyHex = "#52668F";
        public const string AmbientGroundHex = "#141820";
        public const float AmbientIntensity = 0.78f;

        // ---- Moon: a directional light from the north-west, 24.8 degrees above the horizon ------------------------
        public const string MoonHex = "#A9BDE6";
        public const float MoonIntensity = 0.9f;

        /// <summary>Direction toward the moon: the prototype's (-0.349, 0.419, -0.838) with z negated.</summary>
        public static readonly Vector3 TowardMoon = new Vector3(-0.349f, 0.419f, 0.838f).normalized;

        // ---- Sky (the prototype's sky dome) -----------------------------------------------------------------------
        public const string SkyHorizonHex = "#0E1626";
        public const string SkyZenithHex = "#02040A";
        public const float CloudCover = 0.5f;

        /// <summary>Reflections of the sky on outdoor metal and water, until the per-room probes take over indoors.</summary>
        public const int SkyReflectionResolution = 128;

        // ---- URP shadows recommended for a medium preset (the URP asset is not World Builder's to edit) -----------
        // The prototype's medium preset is one 2048 px moon shadow map over an 80 x 80 m box. Two cascades over 60 m
        // give about the same detail near the player (the first cascade covers 15 m) and draw the shadow casters twice
        // instead of four times. At 60 m the fog leaves 52 % of a shadow visible, so a longer distance is wasted.
        public const float ShadowDistance = 60f;
        public const int ShadowCascades = 2;
        public const float ShadowCascadeSplit = 0.25f;
        public const int MainShadowResolution = 2048;

        public static Color FogColour => FromLinearHex(FogHex);
        public static Color MoonColour => FromLinearHex(MoonHex);
        public static Color SkyHorizon => FromLinearHex(SkyHorizonHex);
        public static Color SkyZenith => FromLinearHex(SkyZenithHex);

        /// <summary>Ambient from straight above: the hemisphere's sky colour times its intensity.</summary>
        public static Color AmbientSky => FromLinear(Linear(AmbientSkyHex) * AmbientIntensity);

        /// <summary>Ambient from the side: a hemisphere light gives a horizontal surface half sky and half ground.</summary>
        public static Color AmbientEquator => FromLinear((Linear(AmbientSkyHex) + Linear(AmbientGroundHex)) * 0.5f * AmbientIntensity);

        /// <summary>Ambient from straight below: the hemisphere's ground colour times its intensity.</summary>
        public static Color AmbientGround => FromLinear(Linear(AmbientGroundHex) * AmbientIntensity);

        /// <summary>The moon light's rotation: it shines away from the moon.</summary>
        public static Quaternion MoonRotation => Quaternion.LookRotation(-TowardMoon);

        /// <summary>
        /// The sRGB colour for a Unity colour field whose linear value is the hex's numbers, as three.js r128 uses them.
        /// For example #0B1220 becomes #3B4B63: Unity converts it back to linear (0.043, 0.071, 0.125).
        /// </summary>
        public static Color FromLinearHex(string hex)
        {
            return FromLinear(Linear(hex));
        }

        /// <summary>The hex's numbers as a linear colour (0 - 1 per channel).</summary>
        public static Color Linear(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color colour))
            {
                throw new System.ArgumentException($"Not a colour: {hex}");
            }

            return colour;
        }

        private static Color FromLinear(Color linear)
        {
            Color srgb = linear.gamma;
            srgb.a = 1f;
            return srgb;
        }
    }
}
