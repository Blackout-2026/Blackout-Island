using NUnit.Framework;
using UnityEditor;

namespace Blackout.Editor.Tests
{
    public class ModelTextureRulesTests
    {
        [SetUp]
        public void SetUp()
        {
            TestAssetFolders.Create();
        }

        [TearDown]
        public void TearDown()
        {
            TestAssetFolders.Delete();
        }

        [TestCase("Assets/Textures/Models/T_Generator_BaseColor.png", ModelTextureMap.BaseColor)]
        [TestCase("Assets/Textures/Models/T_Generator_Normal.png", ModelTextureMap.Normal)]
        [TestCase("Assets/Textures/Models/T_Generator_MetalSmooth.tga", ModelTextureMap.MetalSmooth)]
        [TestCase("Assets/Textures/Models/T_Generator_Emission.png", ModelTextureMap.Emission)]
        [TestCase("Assets/Textures/Models/T_Generator_normal.png", ModelTextureMap.Normal)]
        [TestCase("Assets/Textures/Models/T_Generator_Albedo.png", ModelTextureMap.Unknown)]
        [TestCase("Assets/Textures/Models/Generator.png", ModelTextureMap.Unknown)]
        [TestCase("", ModelTextureMap.Unknown)]
        public void MapOf_ReadsTheSuffix(string path, ModelTextureMap expected)
        {
            Assert.AreEqual(expected, ModelTextureRules.MapOf(path));
        }

        [Test]
        public void IsManaged_OnlyInsideTexturesModels()
        {
            Assert.IsTrue(ModelTextureRules.IsManaged("Assets/Textures/Models/T_A_Normal.png"));
            Assert.IsTrue(ModelTextureRules.IsManaged("Assets\\Textures\\Models\\T_A_Normal.png"));
            Assert.IsFalse(ModelTextureRules.IsManaged("Assets/Textures/Environment/T_A_Normal.png"));
            Assert.IsFalse(ModelTextureRules.IsManaged(null));
        }

        [Test]
        public void Import_NormalMapIsATangentSpaceNormalMap()
        {
            string path = TestAssetFolders.WriteTexture("T_RuleTest_Normal.png");

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            Assert.AreEqual(TextureImporterType.NormalMap, importer.textureType);
            Assert.AreEqual(ModelTextureRules.MaxSize, importer.maxTextureSize);
            Assert.IsTrue(importer.mipmapEnabled);
        }

        [Test]
        public void Import_MetalSmoothIsLinearWithSmoothnessInAlpha()
        {
            string path = TestAssetFolders.WriteTexture("T_RuleTest_MetalSmooth.png");

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            Assert.AreEqual(TextureImporterType.Default, importer.textureType);
            Assert.IsFalse(importer.sRGBTexture);
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, importer.alphaSource);
            Assert.IsFalse(importer.alphaIsTransparency);
        }

        [Test]
        public void Import_BaseColorIsSrgb()
        {
            string path = TestAssetFolders.WriteTexture("T_RuleTest_BaseColor.png");

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);

            Assert.IsTrue(importer.sRGBTexture);
            Assert.AreEqual(TextureImporterType.Default, importer.textureType);
        }
    }
}
