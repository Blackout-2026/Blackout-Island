using System.Collections.Generic;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class AtmosphereLayoutTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";

        private static BunkerLayout Layout => AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);

        private static AtmosphereSpec Find(string name)
        {
            return AtmosphereLayout.Build(Layout).Find(spec => spec.Name == name);
        }

        // ---- Layout -----------------------------------------------------------------------------------------------

        [Test]
        public void Build_GivesFiveUniquelyNamedSystems()
        {
            List<AtmosphereSpec> specs = AtmosphereLayout.Build(Layout);
            Assert.That(specs.ConvertAll(spec => spec.Name),
                Is.EqualTo(new[] { "SteamVentWest", "SteamVentEast", "CorridorDust", "BeachFog", "Rain" }));
        }

        [Test]
        public void Steam_IsThePrototypesVentSteam()
        {
            AtmosphereSpec west = Find("SteamVentWest");
            AtmosphereSpec east = Find("SteamVentEast");
            Assert.That(west.Position, Is.EqualTo(new Vector3(-14f, 11.75f, 54f)), "prototype (-14, -54), 1.75 m above the yard");
            Assert.That(east.Position, Is.EqualTo(new Vector3(13.5f, 11.75f, 51f)), "prototype (13.5, -51)");

            Assert.That(west.Rate, Is.EqualTo(7f));
            Assert.That(west.Lifetime, Is.EqualTo(new Vector2(2.6f, 3.6f)));
            Assert.That(west.Speed, Is.EqualTo(new Vector2(1.4f, 2.2f)));
            Assert.That(west.Size.x * west.EndSizeScale, Is.EqualTo(2.4f).Within(1e-5f), "0.35 m grows to 2.4 m");
            Assert.That(west.Drag, Is.EqualTo(0.5f));
            Assert.That(west.Colour.linear.r, Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(west.Colour.linear.b, Is.EqualTo(0.55f).Within(1e-3f));
            Assert.That(west.Colour.a, Is.EqualTo(0.22f));

            // The mean velocity is the prototype's (0.25, 1.8, 0) m/s: up, drifting east.
            Assert.That(west.Direction.x / west.Direction.y, Is.EqualTo(0.25f / 1.8f).Within(1e-5f));
            Assert.That(west.Direction.z, Is.EqualTo(0f));
            Assert.That(1.8f * Mathf.Tan(west.ConeAngle * Mathf.Deg2Rad), Is.EqualTo(0.15f).Within(1e-4f), "± 0.15 m/s sideways");
        }

        [Test]
        public void AlphaAt_IsThePrototypesFade()
        {
            AtmosphereSpec steam = Find("SteamVentWest");
            Assert.That(AtmosphereLayout.AlphaAt(steam, 0f), Is.EqualTo(0f));
            Assert.That(AtmosphereLayout.AlphaAt(steam, 0.1f), Is.EqualTo(0.5f * Mathf.Pow(0.9f, 1.3f)).Within(1e-5f), "fading in");
            Assert.That(AtmosphereLayout.AlphaAt(steam, 0.5f), Is.EqualTo(Mathf.Pow(0.5f, 1.3f)).Within(1e-5f));
            Assert.That(AtmosphereLayout.AlphaAt(steam, 1f), Is.EqualTo(0f));

            AtmosphereSpec rain = Find("Rain");
            Assert.That(AtmosphereLayout.AlphaAt(rain, 0f), Is.EqualTo(1f));
            Assert.That(AtmosphereLayout.AlphaAt(rain, 1f), Is.EqualTo(1f), "drops do not fade");
        }

        [Test]
        public void AlphaKeyTimes_FitAUnityGradient()
        {
            foreach (AtmosphereSpec spec in AtmosphereLayout.Build(Layout))
            {
                float[] times = AtmosphereLayout.AlphaKeyTimes(spec);
                Assert.That(times.Length, Is.InRange(2, 8), spec.Name + ": a gradient holds at most 8 alpha keys");
                Assert.That(times[0], Is.EqualTo(0f), spec.Name);
                Assert.That(times[times.Length - 1], Is.EqualTo(1f), spec.Name);
                Assert.That(times, Is.Ordered.Ascending, spec.Name);
                Assert.That(times, Is.Unique, spec.Name);
            }
        }

        [Test]
        public void Caps_HoldEveryParticleAtMedium()
        {
            foreach (AtmosphereSpec spec in AtmosphereLayout.Build(Layout))
            {
                Assert.That(spec.MaxParticles, Is.GreaterThanOrEqualTo(Mathf.CeilToInt(spec.Rate * spec.Lifetime.y)),
                    spec.Name + ": the cap never cuts the emission short");
            }
        }

        [Test]
        public void CorridorDust_StaysInsideTheCorridor()
        {
            Room corridor = Layout.FindRoom("Corridor");
            AtmosphereSpec dust = Find("CorridorDust");
            Assert.That(dust.Direction, Is.EqualTo(Vector3.forward), "box axes are world axes");

            Vector3 min = dust.Position - dust.BoxSize * 0.5f;
            Vector3 max = dust.Position + dust.BoxSize * 0.5f;
            Vector3 roomMin = corridor.position - new Vector3(corridor.size.x, 0f, corridor.size.z) * 0.5f;
            Vector3 roomMax = corridor.position + new Vector3(corridor.size.x * 0.5f, corridor.size.y, corridor.size.z * 0.5f);
            for (int axis = 0; axis < 3; axis++)
            {
                Assert.That(min[axis], Is.EqualTo(roomMin[axis] + AtmosphereLayout.DustMargin).Within(1e-4f), $"min, axis {axis}");
                Assert.That(max[axis], Is.EqualTo(roomMax[axis] - AtmosphereLayout.DustMargin).Within(1e-4f), $"max, axis {axis}");
            }
        }

        [Test]
        public void BeachFog_CoversTheBeachAndTheWaterInFront()
        {
            AtmosphereSpec fog = Find("BeachFog");
            Vector3 min = fog.Position - fog.BoxSize * 0.5f;
            Vector3 max = fog.Position + fog.BoxSize * 0.5f;
            Assert.That(min.x, Is.EqualTo(-55f).Within(1e-4f));
            Assert.That(max.x, Is.EqualTo(55f).Within(1e-4f));
            Assert.That(min.z, Is.EqualTo(-128f).Within(1e-4f), "out to the end of the dock");
            Assert.That(max.z, Is.LessThan(-92f), "inside the beach zone");
            Assert.That(min.y, Is.GreaterThan(0f), "above the water");
            Assert.That(fog.DriftX.x, Is.GreaterThan(0f), "drifting east, with the steam");
        }

        [Test]
        public void Rain_IsBuiltSwitchedOffAndFalls()
        {
            AtmosphereSpec rain = Find("Rain");
            Assert.That(rain.StartsActive, Is.False);
            Assert.That(rain.Direction, Is.EqualTo(Vector3.down));
            Assert.That(rain.StretchVelocityScale, Is.GreaterThan(0f), "streaks");
            float meanFall = (rain.Speed.x + rain.Speed.y) * 0.5f * rain.Lifetime.y;
            Assert.That(meanFall, Is.GreaterThan(AtmosphereLayout.RainHeight), "drops reach below the eye");
        }

        // ---- Builder ----------------------------------------------------------------------------------------------

        [Test]
        public void EmitterRotation_PointsAlongTheDirectionAndKeepsBoxesOnWorldAxes()
        {
            Quaternion up = AtmosphereParticlesBuilder.EmitterRotation(Vector3.up);
            Assert.That(Vector3.Distance(up * Vector3.forward, Vector3.up), Is.LessThan(1e-5f));

            Quaternion down = AtmosphereParticlesBuilder.EmitterRotation(Vector3.down);
            Assert.That(Vector3.Distance(down * Vector3.forward, Vector3.down), Is.LessThan(1e-5f));
            Assert.That(Mathf.Abs((down * Vector3.right).y), Is.LessThan(1e-5f), "the rain box lies flat");
            Assert.That(Mathf.Abs((down * Vector3.up).y), Is.LessThan(1e-5f), "the rain box lies flat");
        }

        [Test]
        public void Configure_BuildsTheSystemFromItsSpec()
        {
            var go = new GameObject("Atmosphere_SteamVentWest");
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            try
            {
                AtmosphereSpec spec = Find("SteamVentWest");
                ParticleSystem particles = AtmosphereParticlesBuilder.Configure(go, spec, material, 42);

                Assert.That(go.transform.position, Is.EqualTo(spec.Position));
                Assert.That(Vector3.Distance(go.transform.forward, spec.Direction), Is.LessThan(1e-5f));
                Assert.That(particles.useAutoRandomSeed, Is.False, "a fixed seed");
                Assert.That(particles.randomSeed, Is.EqualTo(42u));
                Assert.That(particles.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                Assert.That(particles.main.maxParticles, Is.EqualTo(spec.MaxParticles));
                Assert.That(particles.main.duration, Is.EqualTo(spec.Lifetime.y));
                Assert.That(particles.emission.rateOverTimeMultiplier, Is.EqualTo(spec.Rate));
                Assert.That(particles.shape.shapeType, Is.EqualTo(ParticleSystemShapeType.Cone));
                Assert.That(particles.limitVelocityOverLifetime.enabled, Is.True, "drag");
                Assert.That(particles.limitVelocityOverLifetime.drag.constant, Is.EqualTo(spec.Drag));
                Assert.That(particles.sizeOverLifetime.enabled, Is.True);
                Assert.That(particles.noise.enabled || particles.velocityOverLifetime.enabled, Is.False);

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                Assert.That(renderer.sharedMaterial, Is.EqualTo(material));
                Assert.That(renderer.renderMode, Is.EqualTo(ParticleSystemRenderMode.Billboard));
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));

                // A re-run as another kind switches off what it no longer uses.
                AtmosphereSpec fog = Find("BeachFog");
                AtmosphereParticlesBuilder.Configure(go, fog, material, 42);
                Assert.That(particles.limitVelocityOverLifetime.enabled || particles.sizeOverLifetime.enabled, Is.False);
                Assert.That(particles.velocityOverLifetime.enabled, Is.True, "the wind drift");
                Assert.That(particles.shape.shapeType, Is.EqualTo(ParticleSystemShapeType.Box));
                Assert.That(particles.shape.scale, Is.EqualTo(fog.BoxSize));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SoftParticlePixels_FallFromTheCentreToAClearEdge()
        {
            const int size = AtmosphereLayout.TextureSize;
            Color32[] pixels = AtmosphereParticlesBuilder.SoftParticlePixels(size);
            Assert.That(pixels[(size / 2) * size + size / 2].a, Is.GreaterThanOrEqualTo(240), "centre");
            Assert.That(pixels[(size / 2) * size + size / 2 + size / 8].a, Is.InRange(130, 145), "a quarter of the radius out: 0.55");
            Assert.That(pixels[0].a, Is.EqualTo(0), "corner");
            Assert.That(pixels[(size / 2) * size].a, Is.LessThan(8), "edge");
            foreach (Color32 pixel in pixels)
            {
                Assert.That(pixel.r == 255 && pixel.g == 255 && pixel.b == 255, Is.True, "white: the particle colour tints it");
            }
        }
    }
}
