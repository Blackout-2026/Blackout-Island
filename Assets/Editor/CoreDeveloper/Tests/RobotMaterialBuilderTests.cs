using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class RobotMaterialBuilderTests
    {
        private const string LitShader = "Universal Render Pipeline/Lit";

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

        private static RobotMaterialSet Build()
        {
            return RobotMaterialBuilder.BuildAll(TestAssetFolders.Materials, TestAssetFolders.Textures);
        }

        [Test]
        public void BuildAll_CreatesEverySpecWithTheUrpLitShader()
        {
            RobotMaterialSet set = Build();

            Assert.AreEqual(5, RobotMaterialBuilder.Specs.Length);
            foreach (ModelMaterialSpec spec in RobotMaterialBuilder.Specs)
            {
                Material material = set.Find(spec.Name);
                Assert.IsNotNull(material, spec.Name);
                Assert.AreEqual(LitShader, material.shader.name, spec.Name);
                Assert.AreEqual(spec.Name, material.name);
                Assert.IsTrue(AssetDatabase.Contains(material), spec.Name);
            }
        }

        [Test]
        public void Find_ReturnsNothingForAnUnknownName()
        {
            Assert.IsNull(Build().Find("M_SecurityRobot_Typo"));
        }

        [Test]
        public void EyeAndCells_GlowWithAnHdrColourAndThePaintDoesNot()
        {
            RobotMaterialSet set = Build();

            Assert.IsTrue(set.Eye.IsKeywordEnabled("_EMISSION"));
            Assert.AreEqual(new Color(5f, 0.35f, 0.15f), set.Eye.GetColor("_EmissionColor")); // core-developer.md 3.4
            Assert.IsTrue(set.Cells.IsKeywordEnabled("_EMISSION"));
            Assert.Greater(set.Cells.GetColor("_EmissionColor").maxColorComponent, 1f);
            Assert.IsFalse(set.Body.IsKeywordEnabled("_EMISSION"));
            Assert.IsFalse(set.Joint.IsKeywordEnabled("_EMISSION"));
            Assert.IsFalse(set.Accent.IsKeywordEnabled("_EMISSION"));
        }

        [Test]
        public void ExistingMaterial_KeepsHandEdits()
        {
            RobotMaterialSet first = Build();
            first.Accent.SetColor("_BaseColor", Color.green);
            EditorUtility.SetDirty(first.Accent);
            AssetDatabase.SaveAssets();

            RobotMaterialSet second = Build();

            Assert.AreEqual(Color.green, second.Accent.GetColor("_BaseColor"));
        }

        [Test]
        public void AddingTextures_FillsTheThreePaintMaterialsAndNotTheGlowOnes()
        {
            Build();
            TestAssetFolders.WriteTexture("T_SecurityRobot_BaseColor.png");
            TestAssetFolders.WriteTexture("T_SecurityRobot_Normal.png");

            RobotMaterialSet set = Build();

            foreach (Material paint in new[] { set.Body, set.Joint, set.Accent })
            {
                Assert.IsNotNull(paint.GetTexture("_BaseMap"), paint.name);
                Assert.IsNotNull(paint.GetTexture("_BumpMap"), paint.name);
                Assert.AreEqual(Color.white, paint.GetColor("_BaseColor"), "The flat colour must not tint the map.");
            }

            Assert.IsNull(set.Eye.GetTexture("_BaseMap"));
            Assert.IsNull(set.Cells.GetTexture("_BaseMap"));
        }

        [Test]
        public void TheGeneratorTextures_AreNotPickedUpByTheRobot()
        {
            Build();
            TestAssetFolders.WriteTexture("T_Generator_BaseColor.png");

            Assert.IsNull(Build().Body.GetTexture("_BaseMap"));
        }
    }
}
