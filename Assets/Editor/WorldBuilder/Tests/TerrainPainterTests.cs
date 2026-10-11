using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class TerrainPainterTests
    {
        private const float OutdoorFogDensity = 0.0135f; // reference section 4

        [TestCase(VegetationKind.Pine, 1f, VegetationPrototype.Pine)]
        [TestCase(VegetationKind.Palm, 1f, VegetationPrototype.Palm)]
        [TestCase(VegetationKind.Rock, 0.71f, VegetationPrototype.Rock)]
        [TestCase(VegetationKind.Rock, 0.7f, VegetationPrototype.RockSmall)]
        public void TreePrototypeFor_GivesBigRocksTheCollider(VegetationKind kind, float scale, VegetationPrototype expected)
        {
            var instance = new VegetationInstance(kind, 0, 0, scale, 0, 0, 0);
            Assert.That(TerrainPainter.TreePrototypeFor(instance), Is.EqualTo(expected));
        }

        [Test]
        public void MakeTreeInstance_NormalisesPositionAndSinksIt()
        {
            // A pine at the forest checkpoint (0, -90) on ground y 1.77, sunk 0.15 m, prototype rotation 1 rad.
            var pine = new VegetationInstance(VegetationKind.Pine, 0, -90, 1.2, 1, 0.15, 0);
            TreeInstance tree = TerrainPainter.MakeTreeInstance(pine, 1.77f, 0);
            Assert.That(tree.position.x, Is.EqualTo(0.5f).Within(1e-5f)); // (0 + 200) / 400
            Assert.That(tree.position.z, Is.EqualTo(0.225f).Within(1e-5f)); // (-90 + 180) / 400
            Assert.That(tree.position.y, Is.EqualTo((1.77f - 0.15f + 8f) / 32f).Within(1e-5f));
            Assert.That(tree.widthScale, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(tree.heightScale, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(tree.rotation, Is.EqualTo(-1f).Within(1e-5f)); // z is flipped, so the turn is negated
        }

        [TestCase(-200f, -180f, 0, 0)]
        [TestCase(0f, 20f, 256, 256)]
        [TestCase(199.9f, 219.9f, 511, 511)]
        [TestCase(500f, -500f, 511, 0)] // clamped to the terrain
        public void DetailCell_MapsWorldToTheDetailGrid(float x, float z, int cellX, int cellZ)
        {
            Assert.That(TerrainPainter.DetailCell(x, z, TerrainPainter.DetailResolution), Is.EqualTo(new Vector2Int(cellX, cellZ)));
        }

        [Test]
        public void FlatLayer_IsWhiteTintedWithTheLinearPaletteColour()
        {
            SurfaceSpec sand = SurfaceLibrary.Find("Sand");
            var white = new Texture2D(4, 4);
            var layer = new TerrainLayer();
            TerrainPainter.Configure(layer, sand, default, white);

            Color expected = QualitySettings.activeColorSpace == ColorSpace.Linear ? sand.FlatColour.linear : sand.FlatColour;
            Assert.That(layer.diffuseTexture, Is.SameAs(white));
            Assert.That(layer.diffuseRemapMin, Is.EqualTo(Vector4.zero));
            Assert.That(layer.diffuseRemapMax.x, Is.EqualTo(expected.r).Within(1e-5f));
            Assert.That(layer.diffuseRemapMax.y, Is.EqualTo(expected.g).Within(1e-5f));
            Assert.That(layer.diffuseRemapMax.z, Is.EqualTo(expected.b).Within(1e-5f));
            Assert.That(layer.tileSize.x, Is.EqualTo(1f / 0.22f).Within(1e-4f)); // 0.22 repeats per metre
            Assert.That(layer.smoothness, Is.EqualTo(0.08f).Within(1e-5f));
            Object.DestroyImmediate(layer);
            Object.DestroyImmediate(white);
        }

        [Test]
        public void TexturedLayer_UsesTheTextureAndTint()
        {
            SurfaceSpec rock = SurfaceLibrary.Find("Rock");
            var baseColor = new Texture2D(4, 4);
            var layer = new TerrainLayer();
            TerrainPainter.Configure(layer, rock, new SurfaceMaps { BaseColor = baseColor }, null);

            Color expected = QualitySettings.activeColorSpace == ColorSpace.Linear ? rock.TextureTint.linear : rock.TextureTint;
            Assert.That(layer.diffuseTexture, Is.SameAs(baseColor));
            Assert.That(layer.diffuseRemapMax.x, Is.EqualTo(expected.r).Within(1e-5f)); // #B8B8B4, not the flat colour
            Object.DestroyImmediate(layer);
            Object.DestroyImmediate(baseColor);
        }

        [Test]
        public void DrawDistances_EndWhereTheFogHasHiddenThings()
        {
            float treeVisible = Mathf.Exp(-Mathf.Pow(OutdoorFogDensity * TerrainPainter.TreeDistance, 2f));
            float detailVisible = Mathf.Exp(-Mathf.Pow(OutdoorFogDensity * TerrainPainter.DetailDistance, 2f));
            Assert.That(treeVisible, Is.LessThan(0.02f));
            Assert.That(detailVisible, Is.InRange(0.4f, 0.6f));
        }

        [Test]
        public void SplatAndDetailGrids_MatchTheHeightmapSpacing()
        {
            float heightmapSpacing = IslandLayout.TerrainSize.x / (IslandLayout.HeightmapResolution - 1);
            Assert.That(IslandLayout.TerrainSize.x / (TerrainPainter.AlphamapResolution - 1), Is.EqualTo(heightmapSpacing).Within(0.01f));
            Assert.That(IslandLayout.TerrainSize.x / TerrainPainter.DetailResolution, Is.EqualTo(heightmapSpacing).Within(0.01f));
            Assert.That(TerrainPainter.DetailResolution % TerrainPainter.DetailResolutionPerPatch, Is.EqualTo(0));
        }
    }
}
