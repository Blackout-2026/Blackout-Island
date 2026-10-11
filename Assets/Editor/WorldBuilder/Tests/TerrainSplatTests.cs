using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class TerrainSplatTests
    {
        private const float Flat = 1f;
        private const float NoLitter = 0f;
        private const float Macro = 0.15f; // typical value of TerrainSplat.Macro

        [Test]
        public void Layers_AreTheFourOutdoorSurfaces()
        {
            Assert.That(TerrainSplat.Layers, Is.EqualTo(new[] { "Sand", "Grass", "ForestFloor", "Rock" }));
            foreach (string surface in TerrainSplat.Layers)
            {
                Assert.That(SurfaceLibrary.Find(surface), Is.Not.Null, surface);
            }
        }

        [Test]
        public void Weights_AlwaysAddUpToOne()
        {
            var random = new Mulberry32(42);
            for (int i = 0; i < 2000; i++)
            {
                Vector4 w = TerrainSplat.Weights((float)random.Range(-6, 22), (float)random.Range(0.2, 1), (float)random.Range(-180, 220),
                    (float)random.Next(), (float)random.Next(), (float)random.Range(0, 1.5), (float)random.Range(0.1, 0.2));
                Assert.That(w.x + w.y + w.z + w.w, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(Mathf.Min(Mathf.Min(w.x, w.y), Mathf.Min(w.z, w.w)), Is.GreaterThanOrEqualTo(0f));
            }
        }

        [Test]
        public void Beach_IsSand()
        {
            Vector4 w = TerrainSplat.Weights(1.5f, Flat, -110f, 0f, 0f, NoLitter, Macro);
            Assert.That(w[TerrainSplat.Sand], Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Waterline_IsSandAwayFromTheBeachToo()
        {
            Vector4 w = TerrainSplat.Weights(0.3f, Flat, 40f, 0f, 0f, NoLitter, Macro);
            Assert.That(w[TerrainSplat.Sand], Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void OpenGroundInland_IsGrass()
        {
            Vector4 w = TerrainSplat.Weights(6f, Flat, -40f, 0f, 0f, NoLitter, Macro);
            Assert.That(w[TerrainSplat.Grass], Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void UnderATree_IsMostlyForestFloor()
        {
            float litter = TerrainSplat.LitterFootprint(0f, 3.2f);
            Vector4 w = TerrainSplat.Weights(6f, Flat, -40f, 0f, 0f, litter, Macro);
            Assert.That(litter, Is.EqualTo(0.55f).Within(1e-4f));
            Assert.That(w[TerrainSplat.ForestFloor], Is.GreaterThan(w[TerrainSplat.Grass]));
        }

        [Test]
        public void Trail_IsTheDirtMix()
        {
            Vector4 w = TerrainSplat.Weights(6f, Flat, -40f, TerrainSplat.TrailMask(0f), 0f, NoLitter, Macro);
            Assert.That(Vector4.Distance(w, TerrainSplat.Dirt), Is.LessThan(1e-4f));
        }

        [Test]
        public void TrailMask_IsHalfAtTheTrailsEdge()
        {
            Assert.That(TerrainSplat.TrailMask(0f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(TerrainSplat.TrailMask(1.7f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(TerrainSplat.TrailMask(3f), Is.EqualTo(0f).Within(1e-4f));
        }

        [TestCase(0f, 45f, 1f)] // compound yard
        [TestCase(0f, 26f, 1f)] // gate strip
        [TestCase(-40f, 45f, 0f)] // forest west of the compound
        public void GravelMask_CoversTheCompoundAndGate(float x, float z, float expected)
        {
            Assert.That(TerrainSplat.GravelMask(x, z), Is.EqualTo(expected).Within(1e-4f));
        }

        [Test]
        public void Compound_IsTheGravelMix()
        {
            Vector4 w = TerrainSplat.Weights(10f, Flat, 45f, 0f, TerrainSplat.GravelMask(0f, 45f), NoLitter, Macro);
            Assert.That(Vector4.Distance(w, TerrainSplat.Gravel), Is.LessThan(1e-4f));
        }

        [Test]
        public void SteepSlope_IsRock()
        {
            // normal y 0.5 = 60 degrees, past the 0.48 slope where rock is complete.
            Vector4 w = TerrainSplat.Weights(12f, 0.5f, 60f, 0f, 0f, 1f, Macro);
            Assert.That(w[TerrainSplat.Rock], Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Macro_StaysInThePrototypesRange()
        {
            for (float x = -200f; x <= 200f; x += 7.3f)
            {
                Assert.That(TerrainSplat.Macro(x, x * 0.6f), Is.InRange(0.11f, 0.20f));
            }
        }
    }
}
