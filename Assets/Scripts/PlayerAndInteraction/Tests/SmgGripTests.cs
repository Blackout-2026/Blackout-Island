// SG1: contact anchors, reload return and camera alignment on the actual saved Player.
using System.Linq;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Player.Tests
{
    public class SmgGripTests
    {
        private const string Player = "Assets/Prefabs/Player/Player.prefab";
        private const string Holder = "CameraRig/CameraPivot/PlayerCamera/WeaponHolder";

        [Test]
        public void Palms_SeatAtSeparatePistolAndFrontGrips()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
            try
            {
                Transform smg = root.transform.Find(Holder + "/SMG");
                Transform rig = smg.Find("WinterReloadVisual");
                var reload = smg.GetComponent<ViewmodelReload>();
                reload.ApplyAt(0);
                foreach (string side in new[] { "R", "L" })
                {
                    Transform knuckle = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Middle1" + side);
                    Transform target = smg.Find(side == "R" ? "RightHandGrip" : "LeftHandGrip");
                    Assert.That(Vector3.Distance(smg.InverseTransformPoint(knuckle.position), target.localPosition), Is.LessThan(.04f));
                    var held = smg.Find("Visual").GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "Glove" + side);
                    var skin = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "WinterSmg" + side);
                    var baked = new Mesh();
                    try
                    {
                        skin.BakeMesh(baked, true);
                        Vector3[] a = held.sharedMesh.vertices, b = baked.vertices;
                        float error = 0;
                        BoneWeight[] weights=skin.sharedMesh.boneWeights;
                        for(int i=0;i<a.Length;i++)
                        {
                            var w=weights[i];
                            bool fittedThumb=side=="L" && new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)}
                                .Any(v=>v.Item2>0 && skin.bones[v.Item1].name.StartsWith("Thumb"));
                            if(!fittedThumb) error=Mathf.Max(error,Vector3.Distance(held.transform.TransformPoint(a[i]),skin.transform.TransformPoint(b[i])));
                        }
                        Assert.That(error, Is.LessThan(.001f), "visible holding mesh must match the actual reload start");
                    }
                    finally { Object.DestroyImmediate(baked); }
                }
                Assert.That(smg.Find("LeftHandGrip").localPosition.z - smg.Find("RightHandGrip").localPosition.z, Is.GreaterThan(.1f));
                foreach(string finger in new[] { "Thumb", "Index" })
                    for(int joint=1;joint<=3;joint++)
                    {
                        Transform bone = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == finger + joint + "R");
                        Assert.That(smg.InverseTransformPoint(bone.position).z,
                            Is.LessThan(smg.Find("LeftHandGrip").localPosition.z-.08f),
                            "trigger thumb/index must stay at the rear grip instead of crowding the support hand");
                    }
                Transform supportWrist = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "HandL");
                Vector3 support = smg.InverseTransformPoint(supportWrist.position);
                Transform triggerWrist = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "HandR");
                Assert.That(support.z - smg.InverseTransformPoint(triggerWrist.position).z, Is.GreaterThan(.1f), "wrists stay at separate grips");
                Assert.That(support.x, Is.LessThan(-.02f), "support wrist stays on its own side");
                foreach (string side in new[] { "L", "R" })
                {
                    Transform elbow = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Forearm" + side);
                    Vector3 point = smg.InverseTransformPoint(elbow.position);
                    Assert.That(point.x * (side == "L" ? -1 : 1), Is.GreaterThan(.07f), "elbows stay on their own side (SM1: tucked in, not crossed)");
                    Transform hand = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Hand" + side);
                    Transform knuckle = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Middle1" + side);
                    Assert.That(Vector3.Angle(hand.position-elbow.position, knuckle.position-hand.position),
                        Is.LessThan(25f), side + " wrist must stay in a natural range (SM1): the forearm is within 25 degrees of straight with the palm");
                }
                // A marker near a palm is insufficient: check the front target against the actual MP9 surface.
                smg.gameObject.SetActive(true);
                smg.Find("Visual").gameObject.SetActive(true);
                var gun = smg.Find("Visual").GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "Smg");
                var collider = gun.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = gun.sharedMesh;
                Physics.SyncTransforms();
                // GN1: the support-hand marker is the centre of the handguard's cross-section: rays in from both sides reach the real surface within 3 cm of it.
                Vector3 marker = smg.Find("LeftHandGrip").localPosition;
                foreach (Vector3 side in new[] { Vector3.right, Vector3.left })
                {
                    Assert.That(collider.Raycast(new Ray(smg.TransformPoint(marker + side * .08f), -smg.TransformDirection(side)), out RaycastHit hit, 1), Is.True);
                    Assert.That(Vector3.Distance(smg.InverseTransformPoint(hit.point), marker), Is.LessThan(.03f), "the marker sits inside the handguard");
                }
                foreach (string side in new[] { "R", "L" })
                {
                    Transform finger = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Middle3" + side);
                    Transform target = smg.Find(side == "R" ? "RightHandGrip" : "LeftHandGrip");
                    Vector3 towardGrip = target.position - finger.position;
                    Assert.That(collider.Raycast(new Ray(finger.position, towardGrip.normalized), out RaycastHit contact, .2f), Is.True,
                        "curled distal joint must remain outside and face the actual grip surface");
                    Assert.That(contact.distance / smg.lossyScale.x, Is.LessThan(.025f),
                        "fingers must wrap around the grip rather than curl behind it with a gap");
                }
                Transform wrist = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "HandR");
                Transform motion = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Motion");
                Vector3 grip = motion.InverseTransformPoint(wrist.position);
                for(int i=0; i<=60; i++)
                {
                    reload.ApplyAt(i/60f);
                    Assert.That(Vector3.Distance(grip, motion.InverseTransformPoint(wrist.position)), Is.LessThan(.001f), "right hand stays on pistol grip during reload");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(4f/3f)]
        [TestCase(16f/9f)]
        [TestCase(8f/3f)]
        public void AimPose_CentresSightAndReturnsWithoutMovingCameraOrOtherWeapons(float aspect)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Player));
            try
            {
                Camera camera = root.GetComponentInChildren<Camera>(true);
                camera.aspect = aspect;
                Transform holder = root.transform.Find(Holder), smg = holder.Find("SMG");
                Vector3 hip = smg.localPosition, rifle = holder.Find("Rifle").localPosition, knife = holder.Find("Knife").localPosition;
                Quaternion hipRotation = smg.localRotation;
                Assert.That(Vector3.Angle(smg.forward, camera.transform.forward), Is.LessThan(15f), "hip cant still points forward into the view");
                foreach(string name in new[] { "LeftHandGrip", "RightHandGrip" })
                {
                    Vector3 grip = camera.WorldToViewportPoint(smg.Find(name).position);
                    Assert.That(grip.x, Is.InRange(0f,1f));
                    Assert.That(grip.y, Is.InRange(0f,1f));
                }
                Matrix4x4 cameraMatrix = camera.transform.localToWorldMatrix;
                var pose = smg.GetComponent<SmgAimPose>();
                pose.Apply(1);
                Vector3 sight = camera.WorldToViewportPoint(smg.Find("AimReference").position);
                Assert.That(sight.x, Is.EqualTo(.5f).Within(.001f));
                Assert.That(sight.y, Is.EqualTo(.5f).Within(.001f));
                Assert.That(Vector3.Angle(smg.Find("AimReference").forward, camera.transform.forward), Is.LessThan(.01f));
                AssertSleeveEndOutsideView(smg, camera);
                pose.Apply(0);
                AssertSleeveEndOutsideView(smg, camera);
                Assert.That(smg.localPosition, Is.EqualTo(hip));
                Assert.That(Quaternion.Angle(hipRotation, smg.localRotation), Is.LessThan(.001f));
                Assert.That(holder.Find("Rifle").localPosition, Is.EqualTo(rifle));
                Assert.That(holder.Find("Knife").localPosition, Is.EqualTo(knife));
                Assert.That(camera.transform.localToWorldMatrix, Is.EqualTo(cameraMatrix));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void AssertSleeveEndOutsideView(Transform smg, Camera camera)
        {
            Transform rig = smg.Find("WinterReloadVisual");
            Transform wrist = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "HandL");
            Transform elbow = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "ForearmL");
            var sleeve = smg.Find("Visual").GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "GloveL");
            Vector3 axis = (elbow.position-wrist.position).normalized;
            Vector3[] vertices = sleeve.sharedMesh.vertices;
            float end = vertices.Max(v => Vector3.Dot(sleeve.transform.TransformPoint(v)-wrist.position,axis));
            foreach(Vector3 vertex in vertices)
            {
                Vector3 world = sleeve.transform.TransformPoint(vertex);
                if(Vector3.Dot(world-wrist.position,axis) < end*.9f) continue;
                Vector3 view = camera.WorldToViewportPoint(world);
                Assert.That(view.z < camera.nearClipPlane || view.y < 0 || view.y > 1 || view.x < 0 || view.x > 1,
                    Is.True, "cropped sleeve end must stay outside hip and ADS views");
            }
        }
    }
}
