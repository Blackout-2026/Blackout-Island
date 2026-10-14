using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Blackout.Editor.Tests
{
    public class PostProcessLayoutTests
    {
        private const float Aspect = 16f / 9f;

        // ---- Conversions from the prototype ---------------------------------------------------------------------

        [Test]
        public void PostExposure_GivesUrpsAcesThePrototypesBrighterInput()
        {
            Assert.That(Mathf.Pow(2f, PostProcessLayout.PostExposure), Is.EqualTo(1f / 0.6f).Within(1e-5f), "exposure / 0.6");
            Assert.That(PostProcessLayout.PostExposure, Is.EqualTo(0.737f).Within(1e-3f));
        }

        [Test]
        public void BloomThreshold_IsThePrototypesLinearThresholdInGammaSpace()
        {
            Assert.That(PostProcessLayout.BloomThreshold, Is.EqualTo(0.931f).Within(1e-3f));
            Assert.That(Mathf.GammaToLinearSpace(PostProcessLayout.BloomThreshold), Is.EqualTo(0.85f).Within(1e-4f),
                "URP turns its threshold back into linear light");
        }

        [Test]
        public void BloomIntensity_IsTheStrengthTimesTheFiveMipWeights()
        {
            Assert.That(PostProcessLayout.BloomMipWeightSum(), Is.EqualTo(3f).Within(1e-5f), "5 mips × 0.6 at radius 0.5");
            Assert.That(PostProcessLayout.BloomIntensity, Is.EqualTo(2.25f).Within(1e-5f));
            Assert.That(PostProcessLayout.BloomMaxIterations, Is.EqualTo(PostProcessLayout.PrototypeBloomFactors.Length));
        }

        // ---- Vignette fit ---------------------------------------------------------------------------------------

        [Test]
        public void ScreenRadius_IsRoundOnA16By9Screen()
        {
            Assert.That(PostProcessLayout.ScreenRadius(new Vector2(0.5f, 0.5f), Aspect), Is.EqualTo(0f));
            Assert.That(PostProcessLayout.ScreenRadius(new Vector2(0.5f, 1f), Aspect), Is.EqualTo(0.5f).Within(1e-5f), "top edge");
            Assert.That(PostProcessLayout.ScreenRadius(new Vector2(1f, 1f), Aspect), Is.EqualTo(1.0198f).Within(1e-4f), "corner");
        }

        [Test]
        public void Vignette_MatchesThePrototypeAtTheCornerAndTheTopEdge()
        {
            AssertFit(new Vector2(1f, 1f), 0.01f, "corner");
            AssertFit(new Vector2(0.5f, 1f), 0.01f, "top edge");
            Assert.That(Powered(new Vector2(1f, 1f)), Is.EqualTo(0.17f).Within(0.01f), "the corners keep 17 % of their light");
        }

        [Test]
        public void Vignette_StaysCloseToThePrototypeAcrossTheScreen()
        {
            // URP's curve is a power of a parabola and the prototype's a smoothstep, so no setting matches everywhere: the
            // best all-round fit is still 0.087 off, and 0.08 too dark at the corners. This one is exact at the corners and
            // the top edge and at most 0.12 lighter in between (worst at radius 0.76: 0.45 against 0.34).
            for (float x = 0.5f; x <= 1f; x += 0.05f)
            {
                AssertFit(new Vector2(x, 0.5f), 0.12f, $"along the middle, x {x:0.00}");
                AssertFit(new Vector2(x, 1f), 0.12f, $"along the top, x {x:0.00}");
                AssertFit(new Vector2(x, 0.5f + (x - 0.5f)), 0.12f, $"along the diagonal, x {x:0.00}");
            }
        }

        [Test]
        public void Vignette_IsFullStrengthInTheMiddle()
        {
            Assert.That(PostProcessLayout.PrototypeVignetteFactor(0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Powered(new Vector2(0.5f, 0.5f)), Is.EqualTo(1f));
        }

        // ---- Looks ----------------------------------------------------------------------------------------------

        [Test]
        public void Blackout_IsThePoweredLookWithDarkerCornersMoreContrastAndMoreGrain()
        {
            PostLook powered = PostProcessLayout.Powered;
            PostLook blackout = PostProcessLayout.Blackout;
            Assert.That(blackout.VignetteIntensity, Is.GreaterThan(powered.VignetteIntensity));
            Assert.That(blackout.Contrast, Is.GreaterThan(powered.Contrast));
            Assert.That(blackout.GrainIntensity, Is.GreaterThan(powered.GrainIntensity));

            float corner = PostProcessLayout.ScreenRadius(new Vector2(1f, 1f), Aspect);
            float blackoutCorner = PostProcessLayout.UrpVignetteFactor(corner, blackout.VignetteIntensity, blackout.VignetteSmoothness);
            Assert.That(blackoutCorner, Is.EqualTo(0.10f).Within(0.01f), "the corners keep 10 % of their light");

            // Everything that sets the brightness of the lights stays the same, so the W8 lights still read as matched.
            Assert.That(blackout.PostExposure, Is.EqualTo(powered.PostExposure));
            Assert.That(blackout.Saturation, Is.EqualTo(powered.Saturation));
            Assert.That(blackout.BloomThreshold, Is.EqualTo(powered.BloomThreshold));
            Assert.That(blackout.BloomIntensity, Is.EqualTo(powered.BloomIntensity));
            Assert.That(blackout.BloomScatter, Is.EqualTo(powered.BloomScatter));
            Assert.That(blackout.VignetteSmoothness, Is.EqualTo(powered.VignetteSmoothness));
        }

        [Test]
        public void ApplyLook_WritesEveryOverrideAndARerunAddsNothing()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            try
            {
                PostLook look = PostProcessLayout.Powered;
                PostProcessSetup.ApplyLook(profile, look);
                Assert.That(profile.components.Count, Is.EqualTo(5));

                Assert.That(profile.TryGet(out Tonemapping tonemapping), Is.True);
                Assert.That(tonemapping.mode.overrideState, Is.True);
                Assert.That(tonemapping.mode.value, Is.EqualTo(TonemappingMode.ACES));

                Assert.That(profile.TryGet(out ColorAdjustments colour), Is.True);
                Assert.That(colour.postExposure.overrideState && colour.contrast.overrideState && colour.saturation.overrideState, Is.True);
                Assert.That(colour.postExposure.value, Is.EqualTo(look.PostExposure));

                Assert.That(profile.TryGet(out Bloom bloom), Is.True);
                Assert.That(bloom.threshold.value, Is.EqualTo(look.BloomThreshold));
                Assert.That(bloom.intensity.value, Is.EqualTo(look.BloomIntensity));
                Assert.That(bloom.scatter.value, Is.EqualTo(look.BloomScatter));
                Assert.That(bloom.maxIterations.value, Is.EqualTo(look.BloomMaxIterations));

                Assert.That(profile.TryGet(out Vignette vignette), Is.True);
                Assert.That(vignette.intensity.value, Is.EqualTo(look.VignetteIntensity));
                Assert.That(vignette.smoothness.value, Is.EqualTo(look.VignetteSmoothness));
                Assert.That(vignette.rounded.overrideState && vignette.rounded.value, Is.True, "round, like the prototype's");

                Assert.That(profile.TryGet(out FilmGrain grain), Is.True);
                Assert.That(grain.type.value, Is.EqualTo(FilmGrainLookup.Thin1));
                Assert.That(grain.intensity.value, Is.EqualTo(look.GrainIntensity));

                // A re-run with another look reuses the same effects: nothing is added, the values change.
                PostProcessSetup.ApplyLook(profile, PostProcessLayout.Blackout);
                Assert.That(profile.components.Count, Is.EqualTo(5));
                Assert.That(profile.TryGet(out Vignette again) && again == vignette, Is.True);
                Assert.That(vignette.intensity.value, Is.EqualTo(PostProcessLayout.BlackoutVignetteIntensity));
            }
            finally
            {
                foreach (VolumeComponent component in profile.components)
                {
                    Object.DestroyImmediate(component);
                }

                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ApplyLook_KeepsAnEffectSomeoneAddedByHand()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            try
            {
                ChromaticAberration added = profile.Add<ChromaticAberration>();
                added.intensity.Override(0.3f);

                PostProcessSetup.ApplyLook(profile, PostProcessLayout.Powered);
                Assert.That(profile.components.Count, Is.EqualTo(6));
                Assert.That(profile.TryGet(out ChromaticAberration kept) && kept.intensity.value == 0.3f, Is.True);
            }
            finally
            {
                foreach (VolumeComponent component in profile.components)
                {
                    Object.DestroyImmediate(component);
                }

                Object.DestroyImmediate(profile);
            }
        }

        private static float Powered(Vector2 uv)
        {
            float radius = PostProcessLayout.ScreenRadius(uv, Aspect);
            return PostProcessLayout.UrpVignetteFactor(radius, PostProcessLayout.VignetteIntensity, PostProcessLayout.VignetteSmoothness);
        }

        private static void AssertFit(Vector2 uv, float tolerance, string where)
        {
            float radius = PostProcessLayout.ScreenRadius(uv, Aspect);
            Assert.That(Powered(uv), Is.EqualTo(PostProcessLayout.PrototypeVignetteFactor(radius)).Within(tolerance), where);
        }
    }
}
