using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Blackout.Environment.Tests
{
    public class QualityPresetTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void For_GivesThePrototypesThreePresets()
        {
            AssertSettings(QualityLevel.Low, 40f, false, 0.5f);
            AssertSettings(QualityLevel.Medium, 60f, true, 1f);
            AssertSettings(QualityLevel.High, 80f, true, 1.5f);
        }

        [Test]
        public void For_Medium_KeepsTheW7ShadowDistanceAndTheBuiltParticleCounts()
        {
            QualityPreset.Settings medium = QualityPreset.For(QualityLevel.Medium);
            Assert.That(medium.ShadowDistance, Is.EqualTo(60f), "the W7 medium preset");
            Assert.That(medium.ParticleScale, Is.EqualTo(1f), "the tool builds the Medium (prototype) numbers");
        }

        [Test]
        public void ScaledCap_RoundsAndNeverReachesZero()
        {
            Assert.That(QualityPreset.ScaledCap(32, 0.5f), Is.EqualTo(16));
            Assert.That(QualityPreset.ScaledCap(32, 1.5f), Is.EqualTo(48));
            Assert.That(QualityPreset.ScaledCap(1, 0.5f), Is.EqualTo(1));
        }

        [Test]
        public void Apply_ScalesParticlesFromTheirBuiltValuesWithoutCompounding()
        {
            ParticleSystem steam = NewParticles("Steam", 7f, 32);
            ParticleSystem dust = NewParticles("Dust", 25f, 300);
            QualityPreset preset = root.AddComponent<QualityPreset>();
            preset.SetParticles(new[] { steam, dust, null });

            preset.Apply(QualityLevel.Low);
            AssertParticles(steam, 3.5f, 16);
            AssertParticles(dust, 12.5f, 150);

            preset.Apply(QualityLevel.High);
            AssertParticles(steam, 10.5f, 48);
            AssertParticles(dust, 37.5f, 450);

            preset.Apply(QualityLevel.Medium);
            AssertParticles(steam, 7f, 32);
            AssertParticles(dust, 25f, 300);
            Assert.That(preset.Level, Is.EqualTo(QualityLevel.Medium));
        }

        [Test]
        public void Apply_OutsidePlayMode_LeavesTheUrpAssetAlone()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assume.That(pipeline, Is.Not.Null, "the project renders with URP");
            float before = pipeline.shadowDistance;

            root = new GameObject("Volumes");
            root.AddComponent<QualityPreset>().Apply(QualityLevel.High);

            Assert.That(pipeline.shadowDistance, Is.EqualTo(before), "the Systems Engineer's asset is only changed in Play mode");
        }

        private ParticleSystem NewParticles(string name, float rate, int cap)
        {
            if (root == null)
            {
                root = new GameObject("Volumes");
            }

            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var particles = go.AddComponent<ParticleSystem>();
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTimeMultiplier = rate;
            ParticleSystem.MainModule main = particles.main;
            main.maxParticles = cap;
            return particles;
        }

        private static void AssertParticles(ParticleSystem particles, float rate, int cap)
        {
            Assert.That(particles.emission.rateOverTimeMultiplier, Is.EqualTo(rate).Within(1e-4f), particles.name + " rate");
            Assert.That(particles.main.maxParticles, Is.EqualTo(cap), particles.name + " cap");
        }

        private static void AssertSettings(QualityLevel level, float shadowDistance, bool post, float particleScale)
        {
            QualityPreset.Settings settings = QualityPreset.For(level);
            Assert.That(settings.ShadowDistance, Is.EqualTo(shadowDistance), level + " shadow distance");
            Assert.That(settings.PostProcessing, Is.EqualTo(post), level + " post-processing");
            Assert.That(settings.ParticleScale, Is.EqualTo(particleScale), level + " particle scale");
        }
    }
}
