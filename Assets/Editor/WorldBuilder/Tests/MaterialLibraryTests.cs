using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.Editor.Tests
{
    public class MaterialLibraryTests
    {
        private const float ByteTolerance = 1.5f / 255f;
        private const string TempParent = "Assets/Editor/WorldBuilder/Tests";
        private const string TempFolder = TempParent + "/TempSurfaceTextures";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        [Test]
        public void Library_HasTheTwelveSurfacesOfTheBriefAndTheGeneratorGrating()
        {
            string[] expected =
            {
                "Sand", "Grass", "ForestFloor", "Rock", "Concrete", "ConcreteStained", "MetalPlate", "Rust", "Tile",
                "HazardStripe", "Glass", "Fabric", "MetalGrating",
            };

            Assert.That(SurfaceLibrary.All.Select(spec => spec.Name), Is.EqualTo(expected));
        }

        [Test]
        public void FlatColour_IsTheMiddleOfTheAlbedoRangeTimesTheTint()
        {
            // concrete #5F5E59 → #86857D: middle (114.5, 113.5, 107) / 255
            AssertColour(SurfaceLibrary.Find("Concrete").FlatColour, new Color(114.5f / 255f, 113.5f / 255f, 107f / 255f));

            // tiles #B9C0C1 → #D8DFE0 times tint #D8DCDC
            Color tileMiddle = new Color(200.5f / 255f, 207.5f / 255f, 208.5f / 255f);
            AssertColour(SurfaceLibrary.Find("Tile").FlatColour, tileMiddle * new Color(216f / 255f, 220f / 255f, 220f / 255f));
        }

        [Test]
        public void Smoothness_IsOneMinusTheReferenceRoughness()
        {
            Assert.That(SurfaceLibrary.Find("Sand").Smoothness, Is.EqualTo(0.08f).Within(1e-5f));
            Assert.That(SurfaceLibrary.Find("HazardStripe").Smoothness, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(SurfaceLibrary.Find("Glass").Smoothness, Is.EqualTo(0.94f).Within(1e-5f));
        }

        [Test]
        public void Pack_PutsMetallicInRedAndOneMinusRoughnessInAlpha()
        {
            Texture2D roughness = Solid(4, 4, 0.2f);
            Texture2D packed = MetallicSmoothnessPacker.Pack(roughness, 0f, null, 0.65f, 1024);

            Color pixel = packed.GetPixel(1, 2);
            Assert.That(pixel.r, Is.EqualTo(0.65f).Within(ByteTolerance), "constant metallic");
            Assert.That(pixel.a, Is.EqualTo(0.8f).Within(ByteTolerance), "1 - roughness");
            Assert.That(pixel.g + pixel.b, Is.EqualTo(0f));
            Object.DestroyImmediate(roughness);
            Object.DestroyImmediate(packed);
        }

        [Test]
        public void Pack_WithoutRoughnessUsesTheConstantSmoothness()
        {
            Texture2D metallic = Solid(2, 2, 1f);
            Texture2D packed = MetallicSmoothnessPacker.Pack(null, 0.25f, metallic, 0f, 1024);

            Assert.That(packed.GetPixel(0, 0).r, Is.EqualTo(1f).Within(ByteTolerance));
            Assert.That(packed.GetPixel(0, 0).a, Is.EqualTo(0.25f).Within(ByteTolerance));
            Object.DestroyImmediate(metallic);
            Object.DestroyImmediate(packed);
        }

        [Test]
        public void Pack_ShrinksTheLongerSideToTheMaxSizeAndKeepsTheAspect()
        {
            Texture2D roughness = Solid(16, 8, 0.5f);
            Texture2D packed = MetallicSmoothnessPacker.Pack(roughness, 0f, null, 0f, 4);

            Assert.That(packed.width, Is.EqualTo(4));
            Assert.That(packed.height, Is.EqualTo(2));
            Object.DestroyImmediate(roughness);
            Object.DestroyImmediate(packed);
        }

        [Test]
        public void Configure_WithoutTexturesUsesTheFlatColourAndConstants()
        {
            SurfaceSpec spec = SurfaceLibrary.Find("MetalPlate");
            Material material = NewLit();

            MaterialLibraryBuilder.Configure(material, spec, new SurfaceMaps());

            AssertColour(material.GetColor("_BaseColor"), spec.FlatColour);
            Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(0.65f).Within(1e-5f));
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(0.6f, 0.6f)));
            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.False);
            Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.False);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void Configure_WithTexturesAssignsEveryMapAndTintsTheBaseColour()
        {
            SurfaceSpec spec = SurfaceLibrary.Find("Rock");
            Material material = NewLit();
            var maps = new SurfaceMaps
            {
                BaseColor = Solid(2, 2, 0.5f), Normal = Solid(2, 2, 0.5f), MetallicSmoothness = Solid(2, 2, 0.5f), AO = Solid(2, 2, 1f),
            };

            MaterialLibraryBuilder.Configure(material, spec, maps);

            AssertColour(material.GetColor("_BaseColor"), spec.TextureTint);
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(maps.BaseColor));
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1f), "the packed map's alpha is used as it is");
            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True);
            Assert.That(material.IsKeywordEnabled("_OCCLUSIONMAP"), Is.True);
            Object.DestroyImmediate(material);
            foreach (Texture2D texture in new[] { maps.BaseColor, maps.Normal, maps.MetallicSmoothness, maps.AO })
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Configure_GlassIsTransparentAndDoubleSided()
        {
            Material material = NewLit();

            MaterialLibraryBuilder.Configure(material, SurfaceLibrary.Find("Glass"), new SurfaceMaps());

            Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo(0f));
            Assert.That(material.GetColor("_BaseColor").a, Is.EqualTo(0.12f).Within(1e-5f));
            Object.DestroyImmediate(material);
        }

        [Test]
        public void FindMaps_PacksRoughnessOnceAndReportsUnnamedFiles()
        {
            AssetDatabase.CreateFolder(TempParent, "TempSurfaceTextures");
            WritePng("Rust_BaseColor.png", 0.5f);
            WritePng("Rust_Roughness.png", 0.2f);
            WritePng("Rust_Notes.png", 0f);
            SurfaceSpec spec = SurfaceLibrary.Find("Rust");
            var report = new StringBuilder();

            SurfaceMaps maps = MaterialLibraryBuilder.FindMaps(spec, TempFolder, report);
            string packedPath = TempFolder + "/Rust_MetallicSmoothness.png";
            byte[] firstRun = File.ReadAllBytes(packedPath);
            MaterialLibraryBuilder.FindMaps(spec, TempFolder, new StringBuilder());

            Assert.That(maps.BaseColor, Is.Not.Null);
            Assert.That(maps.Normal, Is.Null);
            Assert.That(maps.MetallicSmoothness, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(maps.MetallicSmoothness), Is.EqualTo(packedPath));
            Assert.That(report.ToString(), Does.Contain("Rust_Notes.png"));
            Assert.That(File.ReadAllBytes(packedPath), Is.EqualTo(firstRun), "a re-run writes the same bytes");

            var packed = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            packed.LoadImage(firstRun);
            Assert.That(packed.GetPixel(0, 0).r, Is.EqualTo(0.3f).Within(ByteTolerance), "rust metallic 0.3");
            Assert.That(packed.GetPixel(0, 0).a, Is.EqualTo(0.8f).Within(ByteTolerance), "1 - roughness 0.2");
            Object.DestroyImmediate(packed);
        }

        private static Material NewLit()
        {
            return new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }

        private static Texture2D Solid(int width, int height, float value)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            texture.SetPixels(Enumerable.Repeat(new Color(value, value, value, 1f), width * height).ToArray());
            texture.Apply(false);
            return texture;
        }

        private static void WritePng(string fileName, float value)
        {
            Texture2D texture = Solid(4, 4, value);
            string path = TempFolder + "/" + fileName;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        private static void AssertColour(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-4f), "r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-4f), "g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-4f), "b");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-4f), "a");
        }
    }
}
