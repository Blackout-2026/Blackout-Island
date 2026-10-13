using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class LightingSetupTests
    {
        [Test]
        public void NightSkyShader_CompilesWithoutErrors()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(LightingSetup.SkyShaderPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.name, Is.EqualTo("Blackout/NightSky"));
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        [Test]
        public void Configure_BakesOnlyTheBounceOfMixedLights()
        {
            var settings = new LightingSettings();
            try
            {
                LightingSetup.Configure(settings);
                Assert.That(settings.bakedGI, Is.True);
                Assert.That(settings.realtimeGI, Is.False);
                Assert.That(settings.mixedBakeMode, Is.EqualTo(MixedLightingMode.IndirectOnly));
                Assert.That(settings.lightmapper, Is.EqualTo(LightingSettings.Lightmapper.ProgressiveGPU));
                Assert.That(settings.lightmapResolution, Is.EqualTo(LightingSetup.LightmapResolution));
                Assert.That(settings.lightmapMaxSize, Is.EqualTo(LightingSetup.LightmapMaxSize));
                Assert.That(settings.indirectSampleCount, Is.EqualTo(LightingSetup.IndirectSamples));
                Assert.That(settings.maxBounces, Is.EqualTo(LightingSetup.Bounces));
                Assert.That(settings.denoiserTypeIndirect, Is.EqualTo(LightingSettings.DenoiserType.OpenImage));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ShadowAdvice_IsEmptyForTheMediumPreset()
        {
            List<string> advice = LightingSetup.ShadowAdvice(LightingLayout.ShadowDistance, LightingLayout.ShadowCascades,
                LightingLayout.ShadowCascadeSplit, LightingLayout.MainShadowResolution, true);
            Assert.That(advice, Is.Empty);
        }

        [Test]
        public void ShadowAdvice_ListsEachSettingThatDiffers()
        {
            // The URP template's PC asset: 50 m, 4 cascades, 2048 px, soft shadows on.
            List<string> advice = LightingSetup.ShadowAdvice(50f, 4, 0.25f, 2048, true);
            Assert.That(advice, Is.EqualTo(new[] { "Max Distance 60", "Cascade Count 2 (split 25 %)" }));

            advice = LightingSetup.ShadowAdvice(60f, 2, 0.25f, 1024, false);
            Assert.That(advice, Is.EqualTo(new[] { "Main Light Shadow Resolution 2048", "Soft Shadows on" }));
        }
    }
}
