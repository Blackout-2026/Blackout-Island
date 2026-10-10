using NUnit.Framework;

namespace Blackout.Editor.Tests
{
    public class ModelImportRulesTests
    {
        [TestCase("Assets/Models/Custom/SM_Generator.fbx", true)]
        [TestCase("Assets/Models/Custom/Sub/SM_Prop_Desk.fbx", true)]
        [TestCase("Assets\\Models\\Custom\\SM_Generator.fbx", true)]
        [TestCase("assets/models/custom/SM_Generator.fbx", true)]
        [TestCase("Assets/Models/CustomOther/SM_Generator.fbx", false)]
        [TestCase("Assets/Models/Free/Tree.fbx", false)]
        [TestCase("Assets/Environment/Rock.fbx", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsManaged_OnlyMatchesModelsCustom(string path, bool expected)
        {
            Assert.That(ModelImportRules.IsManaged(path), Is.EqualTo(expected));
        }

        [TestCase("Assets/Models/Custom/SM_Generator.blend", true)]
        [TestCase("Assets/Models/Free/Tree.blend", true)]
        [TestCase("Assets/Models/Custom/SM_Generator.fbx", false)]
        [TestCase("Assets/Environment/Rock.blend", false)]
        [TestCase(null, false)]
        public void IsBlendFileInModels_FlagsOnlyBlendFilesUnderModels(string path, bool expected)
        {
            Assert.That(ModelImportRules.IsBlendFileInModels(path), Is.EqualTo(expected));
        }
    }
}
