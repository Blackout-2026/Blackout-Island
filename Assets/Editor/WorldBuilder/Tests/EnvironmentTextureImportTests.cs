using NUnit.Framework;

namespace Blackout.Editor.Tests
{
    public class EnvironmentTextureImportTests
    {
        private const string Folder = "Assets/Textures/Environment/Concrete/";

        [Test]
        public void Applies_OnlyInsideTheEnvironmentTextureFolder()
        {
            Assert.That(EnvironmentTextureImport.Applies(Folder + "Concrete_BaseColor.jpg"), Is.True);
            Assert.That(EnvironmentTextureImport.Applies("Assets/Textures/Models/Robot_BaseColor.png"), Is.False);
            Assert.That(EnvironmentTextureImport.Applies("Assets/Textures/EnvironmentOld/Concrete_BaseColor.jpg"), Is.False);
        }

        [TestCase("Concrete_BaseColor.jpg", TextureMap.BaseColor)]
        [TestCase("Concrete_Normal_Hero.png", TextureMap.Normal)]
        [TestCase("ConcreteStained_roughness.jpg", TextureMap.Roughness)]
        [TestCase("MetalPlate_Metallic.jpg", TextureMap.Metallic)]
        [TestCase("MetalPlate_MetallicSmoothness.png", TextureMap.MetallicSmoothness)]
        [TestCase("Rock_AO.jpg", TextureMap.AO)]
        [TestCase("Rock_2_AO.jpg", TextureMap.AO)]
        [TestCase("HazardStripe.png", TextureMap.Unknown)]
        public void MapOf_ReadsTheMapWordFromTheFileName(string fileName, TextureMap expected)
        {
            Assert.That(EnvironmentTextureImport.MapOf(Folder + fileName), Is.EqualTo(expected));
        }

        [Test]
        public void RuleFor_BaseColourIsSrgbAt1024()
        {
            TextureImportRule rule = EnvironmentTextureImport.RuleFor(Folder + "Concrete_BaseColor.jpg");

            Assert.That(rule.Srgb, Is.True);
            Assert.That(rule.NormalMap, Is.False);
            Assert.That(rule.MaxSize, Is.EqualTo(1024));
        }

        [Test]
        public void RuleFor_NormalIsANormalMapAndLinear()
        {
            TextureImportRule rule = EnvironmentTextureImport.RuleFor(Folder + "Concrete_Normal.jpg");

            Assert.That(rule.NormalMap, Is.True);
            Assert.That(rule.Srgb, Is.False);
        }

        [TestCase("Concrete_Roughness.jpg")]
        [TestCase("Concrete_Metallic.jpg")]
        [TestCase("Concrete_AO.jpg")]
        [TestCase("Concrete_MetallicSmoothness.png")]
        public void RuleFor_DataMapsAreLinearAndNotNormalMaps(string fileName)
        {
            TextureImportRule rule = EnvironmentTextureImport.RuleFor(Folder + fileName);

            Assert.That(rule.Srgb, Is.False);
            Assert.That(rule.NormalMap, Is.False);
        }

        [Test]
        public void RuleFor_OnlyNamesEndingInHeroGet2048()
        {
            Assert.That(EnvironmentTextureImport.RuleFor(Folder + "Concrete_BaseColor_Hero.jpg").MaxSize, Is.EqualTo(2048));
            Assert.That(EnvironmentTextureImport.RuleFor(Folder + "Hero_BaseColor.jpg").MaxSize, Is.EqualTo(1024));
            Assert.That(EnvironmentTextureImport.RuleFor(Folder + "Concrete_HeroWall_BaseColor.jpg").MaxSize, Is.EqualTo(1024));
        }
    }
}
