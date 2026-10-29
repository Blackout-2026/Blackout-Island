// WA1: saved visual integration, real bone deformation and cinematic/scope visibility.
using System.Linq;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Player.Tests
{
    public class WinterArmsTests
    {
        private const string Player = "Assets/Prefabs/Player/Player.prefab";
        private const string Holder = "CameraRig/CameraPivot/PlayerCamera/WeaponHolder";
        private const string Prefix = "Assets/Models/Custom/SM_WinterArms_";

        [TestCase("SMG", "Smg", "Visual")]
        [TestCase("Rifle", "Rifle", "Visual")]
        [TestCase("Knife", "Knife", "Hand/Visual")]
        public void SavedArms_UseExtractedMeshesAndTheCinematicMaterial(string gun, string model, string visual)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(Player);
            Transform normal = root.transform.Find(Holder + "/" + gun + "/" + visual);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Models/M_PlayerWinter_Body.mat");
            foreach (string side in new[] { "L", "R" })
            {
                var glove = Find(normal, "Glove" + side).GetComponent<MeshFilter>();
                Assert.That(AssetDatabase.GetAssetPath(glove.sharedMesh), Is.EqualTo(Prefix + model + ".fbx"));
                Assert.That(glove.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(glove.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(Find(normal, "Sleeve" + side).GetComponent<Renderer>().enabled, Is.False,
                    "old sleeve must not overlap the extracted arm's own sleeve");
                Assert.That(glove.sharedMesh.uv.Length, Is.EqualTo(glove.sharedMesh.vertexCount));
            }
        }

        [TestCase("SMG")]
        [TestCase("Rifle")]
        public void RetargetedReload_DeformsNewSkinAndReturnsToItsBindPose(string gun)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
            var baked = new Mesh();
            try
            {
                root.transform.Find(Holder).localScale = Vector3.one;
                Transform weapon = root.transform.Find(Holder + "/" + gun);
                var reload = weapon.GetComponent<ViewmodelReload>();
                Assert.That(AssetDatabase.GetAssetPath(reload.Clip), Does.StartWith(Prefix));
                Assert.That(reload.Clip.events, Is.Empty, "existing ammo-clock clip has no animation callbacks");
                foreach (string side in new[] { "L", "R" })
                {
                    var skin = Find(weapon.Find("WinterReloadVisual"), "Winter" + (gun == "SMG" ? "Smg" : "Rifle") + side).GetComponent<SkinnedMeshRenderer>();
                    Assert.That(AssetDatabase.GetAssetPath(skin.sharedMesh), Does.StartWith(Prefix));
                    Assert.That(skin.bones.Length, Is.EqualTo(skin.sharedMesh.bindposes.Length));
                    Assert.That(skin.bones.All(b => b != null), Is.True);
                    reload.ApplyAt(0);
                    skin.BakeMesh(baked);
                    Vector3[] start = baked.vertices;
                    reload.ApplyAt(0.45f);
                    skin.BakeMesh(baked);
                    Vector3[] moved = baked.vertices;
                    Assert.That(start.Where((p, i) => Vector3.Distance(p, moved[i]) > 0.001f).Count(), Is.GreaterThan(10));
                    foreach (Vector3 p in moved)
                        Assert.That(float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude), Is.False);
                    Assert.That(baked.bounds.size.magnitude, Is.LessThan(2f));
                    reload.ApplyAt(1);
                    skin.BakeMesh(baked);
                    Assert.That(start.Zip(baked.vertices, Vector3.Distance).Max(), Is.LessThan(0.001f));
                }
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(root); }
        }

        [Test]
        public void ScopeCinematicAndSprintVisibility_KeepOldSleevesOff()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
            try
            {
                var hands = root.transform.Find(Holder).GetComponent<ViewmodelHands>();
                hands.Collect();
                foreach (int mode in new[] { 0, 1, 2 })
                {
                    if (mode == 0) hands.SetScopeHidden(true);
                    if (mode == 1) hands.SetCinematicHidden(true);
                    if (mode == 2) hands.Apply(false);
                    Assert.That(hands.GetComponentsInChildren<Renderer>(true).All(r => !r.enabled), Is.True);
                    hands.SetScopeHidden(false); hands.SetCinematicHidden(false); hands.Apply(true);
                    Assert.That(hands.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Sleeve")).All(r => !r.enabled), Is.True);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("Smg")]
        [TestCase("Rifle")]
        public void Retarget_PreservesOriginalGunAndMagazineMotion(string label)
        {
            string oldPath = "Assets/Models/Custom/SM_Reload_" + label + ".fbx";
            string newPath = Prefix + label + ".fbx";
            var original = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(oldPath));
            var replacement = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(newPath));
            AnimationClip oldClip = AssetDatabase.LoadAllAssetsAtPath(oldPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            AnimationClip newClip = AssetDatabase.LoadAllAssetsAtPath(newPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            try
            {
                Assert.That(newClip.length, Is.EqualTo(oldClip.length).Within(0.001f));
                for (int frame = 0; frame <= 60; frame++)
                {
                    float t = frame / 60f;
                    oldClip.SampleAnimation(original, t);
                    newClip.SampleAnimation(replacement, t);
                    foreach (string bone in new[] { "Motion", "Magazine", "HandR", "HandL" })
                    {
                        Transform a = Find(original.transform, bone), b = Find(replacement.transform, bone);
                        Assert.That(Vector3.Distance(a.position, b.position), Is.LessThan(0.001f), bone + " frame " + frame);
                        Assert.That(Quaternion.Angle(a.rotation, b.rotation), Is.LessThan(0.1f), bone + " frame " + frame);
                    }
                }
            }
            finally { Object.DestroyImmediate(original); Object.DestroyImmediate(replacement); }
        }

        private static Transform Find(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
    }
}
