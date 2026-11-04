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
                Assert.That(AssetDatabase.GetAssetPath(glove.sharedMesh), Is.EqualTo(gun=="SMG" && side=="L" ? "Assets/Models/Custom/SM_SmgThumb.asset" : Prefix + model + ".fbx"));
                Assert.That(glove.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
                if (side == "R" && gun != "Knife")
                {
                    var trigger = glove.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    Assert.That(glove.GetComponent<Renderer>().enabled, Is.False, "retained static arm cannot overlap TF1");
                    Assert.That(trigger.enabled, Is.True);
                    Assert.That(trigger.sharedMaterial, Is.SameAs(material));
                    Assert.That(trigger.sharedMesh.triangles, Is.EqualTo(glove.sharedMesh.triangles), "index fitting retains topology");
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
            Vector3[] fitted = shaped.vertices, original = originalMesh.vertices;
            BoneWeight[] nativeWeights = originalMesh.boneWeights;
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
            for(int i=0;i<fitted.Length;i++)
            {
                var w=nativeWeights[i];
                bool index=new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)}
                    .Any(p=>p.Item2>0 && native.bones[p.Item1].name.StartsWith("Index"));
                if(!index) Assert.That(Vector3.Distance(fitted[i],original[i]),Is.LessThan(1e-6f),"only the index can be refitted");
            }
            if(gun=="SMG") Assert.That(fitted.Zip(original,Vector3.Distance).Max(),Is.GreaterThan(.01f),"saved SMG asset must retain its new index pose after reimport");
            else Assert.That(fitted,Is.EqualTo(original),"rifle holding geometry is unchanged");
            Vector3 contact=root.transform.Find(Holder+"/"+gun+"/RightIndexContact").localPosition;
            var gunTransform=root.transform.Find(Holder+"/"+gun);
            var meshToGun=gunTransform.worldToLocalMatrix*source.transform.localToWorldMatrix;
            float nearest=float.PositiveInfinity;
            int[] triangles=shaped.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=meshToGun.MultiplyPoint3x4(fitted[triangles[i]]),b=meshToGun.MultiplyPoint3x4(fitted[triangles[i+1]]),c=meshToGun.MultiplyPoint3x4(fitted[triangles[i+2]]);
                Vector3 n=Vector3.Cross(b-a,c-a).normalized;
                if(n.sqrMagnitude<.5f) continue;
                Vector3 projected=contact-n*Vector3.Dot(contact-a,n);
                if(Vector3.Dot(Vector3.Cross(b-a,projected-a),n)>=0 && Vector3.Dot(Vector3.Cross(c-b,projected-b),n)>=0 && Vector3.Dot(Vector3.Cross(a-c,projected-c),n)>=0)
                    nearest=Mathf.Min(nearest,Vector3.Distance(contact,projected));
            }
            TestContext.WriteLine(gun+" trigger-to-skin surface distance: "+nearest);
            if(gun=="SMG") Assert.That(nearest,Is.LessThan(.002f),"finger pad should meet the trigger without stretching its bones");
            Assert.That(delta.Sum(v=>meshToGun.MultiplyVector(v).z),Is.LessThan(0),"pulls toward the rear of the actual gun, not its muzzle");
        }

        [Test]
        public void SmgThumb_ChangesOnlyTheThumb_AndRetainsNativeTopologyAndMaterial()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(Player);
            var glove=player.transform.Find(Holder+"/SMG/Visual").GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="GloveL");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Prefix+"Smg.fbx");
            var native=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="WinterSmgL");
            Vector3[] before=native.sharedMesh.vertices,after=glove.sharedMesh.vertices;
            Assert.That(glove.sharedMesh.triangles,Is.EqualTo(native.sharedMesh.triangles));
            Assert.That(glove.sharedMesh.uv,Is.EqualTo(native.sharedMesh.uv));
            BoneWeight[] weights=native.sharedMesh.boneWeights;
            int moved=0;
            for(int i=0;i<before.Length;i++)
            {
                var w=weights[i];
                bool thumb=new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)}
                    .Any(v=>v.Item2>0 && native.bones[v.Item1].name.StartsWith("Thumb"));
                if(!thumb) Assert.That(Vector3.Distance(before[i],after[i]),Is.LessThan(1e-6f),"wrist, sleeves and support fingers stay fixed");
                else if(Vector3.Distance(before[i],after[i])>.001f) moved++;
            }
            Assert.That(moved,Is.GreaterThan(5));
            Assert.That(glove.GetComponent<MeshRenderer>().enabled,Is.True);
            Assert.That(player.transform.Find(Holder+"/SMG/LeftThumbContact"),Is.Not.Null);
            Transform gun=player.transform.Find(Holder+"/SMG");
            Matrix4x4 toGun=gun.worldToLocalMatrix*glove.transform.localToWorldMatrix;
            int distal=System.Array.FindIndex(native.bones,b=>b.name=="Thumb3L");
            Vector3 centre=Vector3.zero;float total=0;
            Bounds bounds=default;bool first=true;
            for(int i=0;i<after.Length;i++)
            {
                var w=weights[i];
                float amount=(w.boneIndex0==distal?w.weight0:0)+(w.boneIndex1==distal?w.weight1:0)
                    +(w.boneIndex2==distal?w.weight2:0)+(w.boneIndex3==distal?w.weight3:0);
                Vector3 point=toGun.MultiplyPoint3x4(after[i]);
                centre+=point*amount;total+=amount;
                bool thumb=new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)}
                    .Any(v=>v.Item2>.5f && native.bones[v.Item1].name.StartsWith("Thumb"));
                if(!thumb) continue;
                if(first) {bounds=new Bounds(point,Vector3.zero);first=false;} else bounds.Encapsulate(point);
            }
            centre/=total;
            Vector3 contact=gun.Find("LeftThumbContact").localPosition;
            TestContext.WriteLine("Thumb distal centre "+centre.ToString("F6")+"; bounds "+bounds.size.ToString("F6"));
            // GN1: the thumb lies along the handguard's left face (one finger radius off it), pointing forward, instead of wrapping a vertical foregrip.
            Assert.That(Mathf.Abs(centre.x-(contact.x-.0086f)),Is.LessThan(.012f),"the thumb tip rests against the handguard's left face");
            Assert.That(centre.z,Is.GreaterThan(contact.z-.02f),"the thumb points forward along the handguard");
            Assert.That(bounds.size.z,Is.GreaterThan(bounds.size.y*1.15f),"the thumb lies along the handguard rather than standing upright");
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
                // GN1: the SMG's reload was remade for the IOER-32G, so only its first and last frame (the approved pose) must equal the original's.
                // The rifle's whole retargeted motion is still compared frame by frame.
                foreach (int frame in label == "Smg" ? new[] { 0, 60 } : Enumerable.Range(0, 61))
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
