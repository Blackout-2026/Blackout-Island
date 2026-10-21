using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// The Hunter as the downloaded Left 4 Dead model (step HV1): everything <see cref="SkinnedHumanoidTestsBase"/> asks of a skinned humanoid agent (a Valve-biped skeleton with a
    /// Humanoid rig for AN1's soldier clips, the scoped rifle on its right hand), plus its one textured material.
    /// </summary>
    public class HunterModelTests : SkinnedHumanoidTestsBase
    {
        protected override SkinnedHumanoidSpec Spec => HunterModelBuilder.Spec;

        [Test]
        public void Material_IsOneAndHasTheTexture()
        {
            Assert.AreEqual(1, Spec.MaterialNames().Count);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Models/" + HunterModelBuilder.MaterialName + ".mat");
            Assert.IsNotNull(material);
            Assert.IsNotNull(material.GetTexture("_BaseMap"), "T_CharHunterL4D_BaseColor");
        }
    }
}
