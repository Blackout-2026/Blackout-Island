using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class PartMaterialBuildersTests
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

        private static PartMaterials Props()
        {
            return PropMaterialBuilder.BuildAll(TestAssetFolders.Materials, TestAssetFolders.Textures);
        }

        private static PartMaterials Weapons()
        {
            return WeaponMaterialBuilder.BuildAll(TestAssetFolders.Materials, TestAssetFolders.Textures);
        }

        [Test]
        public void PropMaterials_AreCreatedWithTheUrpLitShaderAndTheirPrefix()
        {
            PartMaterials set = Props();

            foreach (ModelMaterialSpec spec in PropMaterialBuilder.Specs)
            {
                StringAssert.StartsWith("M_Prop_", spec.Name);
                Material material = set.Find(spec.Name);
                Assert.IsNotNull(material, spec.Name);
                Assert.AreEqual(LitShader, material.shader.name, spec.Name);
                Assert.IsTrue(AssetDatabase.Contains(material), spec.Name);
            }
        }

        [Test]
        public void WeaponMaterials_AreCreatedWithTheUrpLitShaderAndTheirPrefix()
        {
            PartMaterials set = Weapons();

            foreach (ModelMaterialSpec spec in WeaponMaterialBuilder.Specs)
            {
                StringAssert.StartsWith("M_Weapon_", spec.Name);
                Assert.IsNotNull(set.Find(spec.Name), spec.Name);
            }
        }

        [Test]
        public void Find_ReturnsNothingForTheOtherFamilyOrATypo()
        {
            PartMaterials props = Props();

            Assert.IsNull(props.Find("M_Weapon_Metal"));
            Assert.IsNull(props.Find("M_Prop_Typo"));
            Assert.IsNull(props.Find(string.Empty));
            Assert.IsNull(props.Find(null));
            Assert.IsNotNull(props.Get("Steel"));
            Assert.IsNull(props.Get("Typo"));
        }

        [Test]
        public void TheGlowMaterials_UseHdrEmissionAndThePaintDoesNot()
        {
            PartMaterials props = Props();

            foreach (string glow in new[] { "GlowCyan", "GlowGreen", "CureVial" })
            {
                Assert.IsTrue(props.Get(glow).IsKeywordEnabled("_EMISSION"), glow);
                Assert.Greater(props.Get(glow).GetColor("_EmissionColor").maxColorComponent, 1f, glow);
            }

            Assert.IsFalse(props.Get("Steel").IsKeywordEnabled("_EMISSION"));
            Assert.AreEqual(new Color(0.4f, 2.6f, 2.8f), props.Get("CureVial").GetColor("_EmissionColor")); // core-developer.md 6.1
            Assert.AreEqual(new Color(8f, 0.4f, 0.2f), Weapons().Get("RedDot").GetColor("_EmissionColor")); // 5.1
        }

        [Test]
        public void Glass_IsTransparentAtTwelvePercent()
        {
            Material glass = Props().Get("Glass");

            Assert.AreEqual(1f, glass.GetFloat("_Surface"));
            Assert.AreEqual(0.12f, glass.GetColor("_BaseColor").a, 0.001f);
        }

        [Test]
        public void ExistingMaterial_KeepsHandEdits()
        {
            PartMaterials first = Props();
            first.Get("Wood").SetColor("_BaseColor", Color.green);
            EditorUtility.SetDirty(first.Get("Wood"));
            AssetDatabase.SaveAssets();

            Assert.AreEqual(Color.green, Props().Get("Wood").GetColor("_BaseColor"));
        }
    }
}
