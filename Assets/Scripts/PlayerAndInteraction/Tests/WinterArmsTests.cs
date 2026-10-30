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
                if (side == "R" && gun != "Knife")
                {
                    var trigger = glove.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    Assert.That(glove.GetComponent<Renderer>().enabled, Is.False, "retained static arm cannot overlap TF1");
                    Assert.That(trigger.enabled, Is.True);
                    Assert.That(trigger.sharedMaterial, Is.SameAs(material));
                    Assert.That(trigger.sharedMesh.vertices, Is.EqualTo(glove.sharedMesh.vertices), "holding shape retains the accepted geometry");
                }
                else Assert.That(glove.GetComponent<Renderer>().enabled, Is.True);
                Assert.That(Find(normal, "Sleeve" + side).GetComponent<Renderer>().enabled, Is.False,
                    "old sleeve must not overlap the extracted arm's own sleeve");
                Assert.That(glove.sharedMesh.uv.Length, Is.EqualTo(glove.sharedMesh.vertexCount));
            }
        }

        [TestCase("SMG", "Smg")]
        [TestCase("Rifle", "Rifle")]
        public void TriggerShape_PreservesRestMesh_AndMovesOnlyWeightedIndexVertices(string gun, string modelName)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(Player);
            var finger = root.transform.Find(Holder+"/"+gun+"/Visual").GetComponent<TriggerFinger>();
            Assert.That(finger, Is.Not.Null);
            var skin = finger.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var source = finger.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="GloveR");
            Assert.That(source.GetComponent<MeshRenderer>().enabled,Is.False,"no duplicate arm");
            Assert.That(skin.sharedMaterials,Is.EqualTo(source.GetComponent<MeshRenderer>().sharedMaterials));
            Mesh originalMesh = source.sharedMesh, shaped = skin.sharedMesh;
            Assert.That(shaped.vertices,Is.EqualTo(originalMesh.vertices));
            Assert.That(shaped.triangles,Is.EqualTo(originalMesh.triangles));
            Assert.That(shaped.uv,Is.EqualTo(originalMesh.uv));
            var delta = new Vector3[shaped.vertexCount];
            shaped.GetBlendShapeFrameVertices(0,0,delta,null,null);
            string label = modelName;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Custom/SM_WinterArms_"+label+".fbx");
            var native = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Winter"+label+"R");
            BoneWeight[] weights = originalMesh.boneWeights;
            int moved = 0;
            for(int i=0;i<delta.Length;i++)
            {
                if(delta[i].sqrMagnitude<1e-12f) continue;
                moved++;
                var w = weights[i];
                bool index = new[] {(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)}
                    .Any(p=>p.Item2>0 && (native.bones[p.Item1].name=="Index2R" || native.bones[p.Item1].name=="Index3R"));
                Assert.That(index,Is.True,"palm, wrist, other fingers and sleeve must stay fixed");
                Assert.That(delta[i].magnitude,Is.LessThan(.012f));
            }
            Assert.That(moved,Is.GreaterThan(5));
            Assert.That(delta.Sum(v=>v.z),Is.LessThan(0),"pulls toward the rear of the actual gun, not its muzzle");
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
                    // SG1 deliberately fits new SMG wrist anchors; the original gun/magazine curves remain exact.
                    foreach (string bone in label == "Smg" ? new[] { "Motion", "Magazine" } : new[] { "Motion", "Magazine", "HandR", "HandL" })
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
