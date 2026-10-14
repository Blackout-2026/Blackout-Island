using System.Collections.Generic;
using Blackout.World;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>The look of a particle system's material. Each one becomes Atmosphere_&lt;Kind&gt;.mat.</summary>
    public enum AtmosphereKind
    {
        /// <summary>Unlit, soft where it meets the ground.</summary>
        Steam,

        /// <summary>Simple Lit, so the motes catch the tube light and turn red in the blackout.</summary>
        Dust,

        /// <summary>Unlit, soft at the ground and fading out near the camera.</summary>
        Fog,

        /// <summary>Unlit streaks.</summary>
        Rain,
    }

    /// <summary>One particle system built by Blackout/World/Build Atmosphere Particles.</summary>
    public class AtmosphereSpec
    {
        /// <summary>The system is Volumes/Atmosphere/Atmosphere_&lt;Name&gt;.</summary>
        public string Name;
        public AtmosphereKind Kind;
        public Vector3 Position;

        /// <summary>The emitter's forward: particles leave the shape along it.</summary>
        public Vector3 Direction = Vector3.up;

        public bool StartsActive = true;

        // Emission (the Medium values: QualityPreset scales the rate and the cap).
        public float Rate;
        public int MaxParticles;
        public bool Prewarm;

        // Particles.
        public Vector2 Lifetime;
        public Vector2 Speed;
        public Vector2 Size;

        /// <summary>The size at the end of a particle's life, as a multiple of its start size. 1 keeps it the same.</summary>
        public float EndSizeScale = 1f;

        /// <summary>sRGB colour. Alpha is the opacity before the fades (the prototype's a0); see <see cref="AtmosphereLayout.AlphaAt"/>.</summary>
        public Color Colour;

        /// <summary>Fraction of the life over which a particle fades in.</summary>
        public float FadeIn;

        /// <summary>After that, the opacity follows (1 - t)^FadeOutPower. 0 keeps it constant.</summary>
        public float FadeOutPower;

        // Shape.
        public ParticleSystemShapeType Shape = ParticleSystemShapeType.Cone;

        /// <summary>A box's size, in the emitter's space (z along <see cref="Direction"/>).</summary>
        public Vector3 BoxSize;
        public float ConeAngle;
        public float ConeRadius;

        // Motion.
        /// <summary>Linear drag on the velocity, per second.</summary>
        public float Drag;

        /// <summary>A world-space drift along +X (east, the wind), random between the two values.</summary>
        public Vector2 DriftX;

        public float NoiseStrength;
        public float NoiseFrequency;

        /// <summary>Random start rotation and a random spin up to this many degrees per second either way.</summary>
        public float SpinDegrees;
        public bool RandomRotation;

        // Rendering.
        /// <summary>Stretched along the velocity: the streak is Speed × this long (rain).</summary>
        public float StretchVelocityScale;

        /// <summary>Largest on-screen size, as a fraction of the view (Unity's default is 0.5).</summary>
        public float MaxScreenSize = 0.5f;

        /// <summary>Sorted back to front, for large overlapping puffs.</summary>
        public bool SortByDistance;
    }

    /// <summary>
    /// Numbers for Blackout/World/Build Atmosphere Particles (W9). docs/level-layout.md lists them too: if you change a number
    /// here, change it there.
    /// - Vent steam is the prototype's (src/world/effects.js and bunker_exterior.js): two compound vents at prototype
    ///   (-14, -54) and (13.5, -51), 1.75 m above the yard, 7 puffs per second.
    /// - Corridor dust, beach fog and rain are Unity additions the brief asks for; the prototype has none.
    /// All rates and caps are the Medium preset; <see cref="Blackout.Environment.QualityPreset"/> scales them at run time.
    /// The wind blows toward +X (east) everywhere, so the steam and the fog drift the same way.
    /// </summary>
    public static class AtmosphereLayout
    {
        public const string ContainerName = "Atmosphere";
        public const string TextureFolder = "Assets/Environment/Atmosphere";
        public const string TexturePath = TextureFolder + "/SoftParticle.png";
        public const string MaterialFolder = "Assets/Materials/Environment/Atmosphere";

        /// <summary>The prototype's soft round sprite (textures.js glow), 64 px instead of 128.</summary>
        public const int TextureSize = 64;

        // ---- Vent steam (prototype) -------------------------------------------------------------------------------
        public const float YardHeight = 10f;
        public const float SteamHeight = 1.75f;
        public const float SteamRate = 7f;

        /// <summary>Linear colour (0.5, 0.52, 0.55), at 22 % opacity.</summary>
        public static readonly Color SteamLinear = new Color(0.5f, 0.52f, 0.55f, 0.22f);

        /// <summary>The prototype's puff: up 1.4 … 2.2 m/s, east 0.25 ± 0.15 m/s, north/south ± 0.15 m/s.</summary>
        public const float SteamDriftEast = 0.25f;
        public const float SteamRiseMin = 1.4f;
        public const float SteamRiseMax = 2.2f;
        public const float SteamSpread = 0.15f;

        // ---- Corridor dust -----------------------------------------------------------------------------------------

        /// <summary>The motes stay this far from the corridor's walls, floor and ceiling.</summary>
        public const float DustMargin = 0.2f;

        /// <summary>About one mote per cubic metre: 25 per second, living 8 … 12 s.</summary>
        public const float DustRate = 25f;

        // ---- Beach fog ---------------------------------------------------------------------------------------------

        /// <summary>The beach (docs/level-layout.md) out over the water: x -55 … 55, z -128 … -96.</summary>
        public static readonly Vector3 FogCentre = new Vector3(0f, 1.5f, -112f);
        public static readonly Vector3 FogSize = new Vector3(110f, 1f, 32f);

        // ---- Rain --------------------------------------------------------------------------------------------------

        /// <summary>The rain falls from this high above the camera, over a square this wide around it.</summary>
        public const float RainHeight = 12f;
        public const float RainWidth = 30f;

        /// <summary>RainEmitter stops the rain while anything is this close above the camera (a ceiling, a roof).</summary>
        public const float RainRoofCheck = 30f;

        /// <summary>Above the player start on the dock, where the emitter waits until it finds the camera.</summary>
        public static readonly Vector3 RainStart = new Vector3(0f, 1.3f + RainHeight, -121f);

        /// <summary>The systems, in hierarchy order.</summary>
        public static List<AtmosphereSpec> Build(BunkerLayout layout)
        {
            return new List<AtmosphereSpec>
            {
                Steam("SteamVentWest", -14f, -54f),
                Steam("SteamVentEast", 13.5f, -51f),
                CorridorDust(layout.FindRoom("Corridor")),
                BeachFog(),
                Rain(),
            };
        }

        /// <summary>
        /// A vent at prototype (x, z): Unity (x, 11.75, -z). The cone leans east so the mean velocity is the prototype's
        /// (0.25, 1.8, 0); its 5° opening gives the ± 0.15 m/s spread, and its 0.15 m radius the spawn jitter.
        /// </summary>
        public static AtmosphereSpec Steam(string name, float prototypeX, float prototypeZ)
        {
            float rise = (SteamRiseMin + SteamRiseMax) * 0.5f;
            return new AtmosphereSpec
            {
                Name = name,
                Kind = AtmosphereKind.Steam,
                Position = new Vector3(prototypeX, YardHeight + SteamHeight, -prototypeZ),
                Direction = new Vector3(SteamDriftEast, rise, 0f).normalized,
                Rate = SteamRate,
                MaxParticles = 32, // 7 per second × 3.6 s = 25 alive at most
                Prewarm = true,
                Lifetime = new Vector2(2.6f, 3.6f),
                Speed = new Vector2(SteamRiseMin, SteamRiseMax),
                Size = new Vector2(0.35f, 0.35f),
                EndSizeScale = 2.4f / 0.35f,
                Colour = SteamLinear.gamma,
                FadeIn = 0.2f,
                FadeOutPower = 1.3f,
                Shape = ParticleSystemShapeType.Cone,
                ConeAngle = Mathf.Atan2(SteamSpread, rise) * Mathf.Rad2Deg,
                ConeRadius = SteamSpread,
                Drag = 0.5f,
                SortByDistance = true,
            };
        }

        /// <summary>Slow motes filling the corridor, drifting on a gentle noise field.</summary>
        public static AtmosphereSpec CorridorDust(Room corridor)
        {
            Vector3 size = corridor.size - Vector3.one * (2f * DustMargin);
            return new AtmosphereSpec
            {
                Name = "CorridorDust",
                Kind = AtmosphereKind.Dust,
                Position = corridor.position + Vector3.up * (corridor.size.y * 0.5f),
                Direction = Vector3.forward, // the box's z runs along the corridor (north)
                Rate = DustRate,
                MaxParticles = 300,
                Prewarm = true,
                Lifetime = new Vector2(8f, 12f),
                Speed = Vector2.zero,
                Size = new Vector2(0.015f, 0.035f),
                Colour = new Color(0.85f, 0.82f, 0.75f, 0.6f),
                FadeIn = 0.2f,
                FadeOutPower = 1f,
                Shape = ParticleSystemShapeType.Box,
                BoxSize = size,
                NoiseStrength = 0.05f,
                NoiseFrequency = 0.4f,
            };
        }

        /// <summary>Large faint puffs over the shore, in the moonlit sky colour, drifting east with the wind.</summary>
        public static AtmosphereSpec BeachFog()
        {
            Color colour = LightingLayout.AmbientSky;
            colour.a = 0.1f;
            return new AtmosphereSpec
            {
                Name = "BeachFog",
                Kind = AtmosphereKind.Fog,
                Position = FogCentre,
                Direction = Vector3.forward,
                Rate = 3.2f,
                MaxParticles = 120, // 3.2 per second × 30 s = 96 alive at most
                Prewarm = true,
                Lifetime = new Vector2(20f, 30f),
                Speed = Vector2.zero,
                Size = new Vector2(6f, 10f),
                Colour = colour,
                FadeIn = 0.25f,
                FadeOutPower = 1f,
                Shape = ParticleSystemShapeType.Box,
                BoxSize = FogSize,
                DriftX = new Vector2(0.3f, 0.5f),
                RandomRotation = true,
                SpinDegrees = 3f,
                MaxScreenSize = 2f,
                SortByDistance = true,
            };
        }

        /// <summary>
        /// Light rain, built switched off. When it is turned on, RainEmitter keeps it over the camera and stops it under a
        /// roof. Drops fall 6 … 8 m/s (drizzle) and live 2 s, so they reach 2 m below the eye.
        /// </summary>
        public static AtmosphereSpec Rain()
        {
            return new AtmosphereSpec
            {
                Name = "Rain",
                Kind = AtmosphereKind.Rain,
                Position = RainStart,
                Direction = Vector3.down,
                StartsActive = false,
                Rate = 500f,
                MaxParticles = 1200, // 500 per second × 2 s = 1000 alive
                Prewarm = false, // it starts wherever the camera is
                Lifetime = new Vector2(2f, 2f),
                Speed = new Vector2(6f, 8f),
                Size = new Vector2(0.012f, 0.012f),
                Colour = new Color(0.7f, 0.75f, 0.85f, 0.3f),
                FadeIn = 0f,
                FadeOutPower = 0f,
                Shape = ParticleSystemShapeType.Box,
                BoxSize = new Vector3(RainWidth, RainWidth, 0f),
                StretchVelocityScale = 0.04f, // a 7 m/s drop is a 0.28 m streak
            };
        }

        /// <summary>
        /// A particle's opacity over its life t (0 … 1), as a fraction of its peak: the prototype's
        /// min(1, t / fadeIn) × (1 - t)^power.
        /// </summary>
        public static float AlphaAt(AtmosphereSpec spec, float t)
        {
            float fadeIn = spec.FadeIn > 0f ? Mathf.Min(1f, t / spec.FadeIn) : 1f;
            return fadeIn * Mathf.Pow(1f - t, spec.FadeOutPower);
        }

        /// <summary>Where the opacity curve gets a key: the ends, the fade-in peak and four points on the fade-out.</summary>
        public static float[] AlphaKeyTimes(AtmosphereSpec spec)
        {
            if (spec.FadeIn <= 0f && spec.FadeOutPower <= 0f)
            {
                return new[] { 0f, 1f };
            }

            var times = new List<float> { 0f };
            if (spec.FadeIn > 0f)
            {
                times.Add(spec.FadeIn);
            }

            foreach (float t in new[] { 0.4f, 0.6f, 0.8f, 0.92f })
            {
                if (t > spec.FadeIn)
                {
                    times.Add(t);
                }
            }

            times.Add(1f);
            return times.ToArray();
        }
    }
}
