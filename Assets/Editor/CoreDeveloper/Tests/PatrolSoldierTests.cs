using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// The Security Patrol as the downloaded Mixamo soldier (step PS4): everything <see cref="SkinnedHumanoidTestsBase"/> asks of a skinned humanoid agent, as a Humanoid with
    /// a valid avatar, plus the five textured materials with the jacket's normal map.
    /// </summary>
    public class PatrolSoldierTests : SkinnedHumanoidTestsBase
    {
        protected override SkinnedHumanoidSpec Spec => PatrolSoldierBuilder.Spec;

        [Test]
        public void Materials_AreFiveAndTheJacketHasItsNormalMap()
        {
            Assert.AreEqual(5, Spec.MaterialNames().Count);
            var body = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Models/M_Char_PatrolSoldier_Body.mat");
            Assert.IsNotNull(body);
            Assert.IsNotNull(body.GetTexture("_BumpMap"), "the jacket's normal map");
        }
    }
}
