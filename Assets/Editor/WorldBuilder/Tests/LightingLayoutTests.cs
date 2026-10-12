using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class LightingLayoutTests
    {
        private const float ColourTolerance = 2e-3f;

        [Test]
        public void FromLinearHex_IsTheHexNumbersOnceUnityConvertsItToLinear()
        {
            Color linear = LightingLayout.FromLinearHex("#0B1220").linear;
            Assert.That(linear.r, Is.EqualTo(11f / 255f).Within(ColourTolerance));
            Assert.That(linear.g, Is.EqualTo(18f / 255f).Within(ColourTolerance));
            Assert.That(linear.b, Is.EqualTo(32f / 255f).Within(ColourTolerance));
        }

        [Test]
        public void FromLinearHex_IsBrighterThanTheSameHexReadAsSrgb()
        {
            // Read as sRGB, #0B1220 would be about 4 times darker than the prototype's fog.
            Color asSrgb = LightingLayout.Linear("#0B1220");
            Color converted = LightingLayout.FromLinearHex("#0B1220");
            Assert.That(converted.b, Is.GreaterThan(asSrgb.b * 2f));
        }

        [Test]
        public void MoonRotation_IsTheReferenceRotation()
        {
            // Reference section 4: Directional Light rotation (24.8, 157.4, 0), shining away from the moon.
            Vector3 euler = LightingLayout.MoonRotation.eulerAngles;
            Assert.That(Mathf.DeltaAngle(euler.x, 24.8f), Is.EqualTo(0f).Within(0.1f));
            Assert.That(Mathf.DeltaAngle(euler.y, 157.4f), Is.EqualTo(0f).Within(0.1f));
            Assert.That(Mathf.DeltaAngle(euler.z, 0f), Is.EqualTo(0f).Within(0.1f));
            Vector3 forward = LightingLayout.MoonRotation * Vector3.forward;
            Assert.That(Vector3.Dot(forward, -LightingLayout.TowardMoon), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Ambient_IsTheHemisphereLightAtSkyEquatorAndGround()
        {
            Color sky = LightingLayout.Linear(LightingLayout.AmbientSkyHex) * LightingLayout.AmbientIntensity;
            Color ground = LightingLayout.Linear(LightingLayout.AmbientGroundHex) * LightingLayout.AmbientIntensity;
            AssertLinear(LightingLayout.AmbientSky, sky);
            AssertLinear(LightingLayout.AmbientGround, ground);
            AssertLinear(LightingLayout.AmbientEquator, (sky + ground) * 0.5f);
        }

        [Test]
        public void FogFadesTreesOutBeforeTheTreeDrawDistance()
        {
            // W6 draws trees to 150 m because the fog leaves under 2 % of them visible there.
            float visible = Mathf.Exp(-Mathf.Pow(LightingLayout.FogDensity * TerrainPainter.TreeDistance, 2f));
            Assert.That(visible, Is.LessThan(0.02f));
        }

        private static void AssertLinear(Color srgb, Color expectedLinear)
        {
            Color linear = srgb.linear;
            Assert.That(linear.r, Is.EqualTo(expectedLinear.r).Within(ColourTolerance));
            Assert.That(linear.g, Is.EqualTo(expectedLinear.g).Within(ColourTolerance));
            Assert.That(linear.b, Is.EqualTo(expectedLinear.b).Within(ColourTolerance));
        }
    }
}
