using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class GeneratorMaterialBuilderTests
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

        private static GeneratorMaterialSet Build()
        {
            return GeneratorMaterialBuilder.BuildAll(TestAssetFolders.Materials, TestAssetFolders.Textures);
        }

        // What the Inspector does after a hand edit; without it Unity may reload the unsaved material from disk.
        private static void SaveEdits(Material material)
        {
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        [Test]
        public void BuildAll_CreatesEverySpecWithTheUrpLitShader()
        {
            GeneratorMaterialSet set = Build();

            foreach (GeneratorMaterialSpec spec in GeneratorMaterialBuilder.Specs)
            {
                Material material = set.Find(spec.Name);
                Assert.IsNotNull(material, spec.Name);
                Assert.AreEqual(LitShader, material.shader.name, spec.Name);
                Assert.AreEqual(spec.Name, material.name);
                Assert.IsTrue(AssetDatabase.Contains(material), spec.Name);
            }

            Assert.IsNotNull(set.Spark);
        }

        [Test]
        public void Specs_AreWithinTheSixSlotBudget()
        {
            // docs/art-pipeline.md section 6: the generator may use at most 6 material slots. CoreOff is only
            // swapped in at run time, so the FBX itself needs one fewer.
            Assert.LessOrEqual(GeneratorMaterialBuilder.Specs.Length - 1, 6);
        }

        [Test]
        public void Core_IsEmissiveAndCoreOffIsNot()
        {
            GeneratorMaterialSet set = Build();

            Assert.IsTrue(set.Core.IsKeywordEnabled("_EMISSION"));
            Assert.Greater(set.Core.GetColor("_EmissionColor").maxColorComponent, 1f);
            Assert.IsFalse(set.CoreOff.IsKeywordEnabled("_EMISSION"));
        }

        [Test]
        public void Glass_IsTransparentAndDoubleSided()
        {
            Material glass = Build().Glass;

            Assert.AreEqual(1f, glass.GetFloat("_Surface"));
            Assert.AreEqual(0f, glass.GetFloat("_Cull"));
            Assert.AreEqual(0.12f, glass.GetColor("_BaseColor").a, 0.001f);
        }

        [Test]
        public void ExistingMaterial_KeepsHandEdits()
        {
            GeneratorMaterialSet first = Build();
            first.Body.SetColor("_BaseColor", Color.red);
            first.Body.SetFloat("_Smoothness", 0.77f);
            SaveEdits(first.Body);

            GeneratorMaterialSet second = Build();

            Assert.AreEqual(Color.red, second.Body.GetColor("_BaseColor"));
            Assert.AreEqual(0.77f, second.Body.GetFloat("_Smoothness"), 0.0001f);
        }

        [Test]
        public void AddingTextures_FillsEmptySlotsOfTheTexturedMaterialsOnly()
        {
            Build();
            TestAssetFolders.WriteTexture("T_Generator_BaseColor.png");
            TestAssetFolders.WriteTexture("T_Generator_Normal.png");
            TestAssetFolders.WriteTexture("T_Generator_MetalSmooth.png");

            GeneratorMaterialSet set = Build();

            Assert.IsNotNull(set.Body.GetTexture("_BaseMap"));
            Assert.IsNotNull(set.Metal.GetTexture("_BumpMap"));
            Assert.IsNotNull(set.Details.GetTexture("_MetallicGlossMap"));
            Assert.IsTrue(set.Body.IsKeywordEnabled("_NORMALMAP"));
            Assert.IsTrue(set.Body.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
            Assert.AreEqual(Color.white, set.Body.GetColor("_BaseColor"), "The flat colour must not tint the map.");
            Assert.IsNull(set.Glass.GetTexture("_BaseMap"));
            Assert.IsNull(set.Core.GetTexture("_BaseMap"));
        }

        [Test]
        public void AddingTextures_KeepsAMapThatWasAssignedByHand()
        {
            GeneratorMaterialSet first = Build();
            string handMapPath = TestAssetFolders.WriteTexture("T_Hand_BaseColor.png");
            var handMap = AssetDatabase.LoadAssetAtPath<Texture2D>(handMapPath);
            first.Body.SetTexture("_BaseMap", handMap);
            SaveEdits(first.Body);
            TestAssetFolders.WriteTexture("T_Generator_BaseColor.png");

            GeneratorMaterialSet second = Build();

            Assert.AreEqual(handMap, second.Body.GetTexture("_BaseMap"));
        }

        [Test]
        public void EmissionMask_GoesOnTheDetailsMaterialOnly()
        {
            Build();
            TestAssetFolders.WriteTexture("T_Generator_Emission.png");

            GeneratorMaterialSet set = Build();

            Assert.IsNotNull(set.Details.GetTexture("_EmissionMap"));
            Assert.IsNull(set.Body.GetTexture("_EmissionMap"));
        }
    }
}
