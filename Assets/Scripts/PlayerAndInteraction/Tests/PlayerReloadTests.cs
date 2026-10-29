// Owner: Systems Engineer. RL1: saved wiring and actual fitted-clip deformation.
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Player.Tests
{
    public class PlayerReloadTests
    {
        private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
        private const string Holder = "CameraRig/CameraPivot/PlayerCamera/WeaponHolder/";

        [TestCase("SMG", "AssaultRifle_Reload01")]
        [TestCase("Rifle", "Rifle_Reload01")]
        public void SavedPlayer_HasFittedGenericReloadAndOriginalPackReference(string gun, string sourceName)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var reload = player.transform.Find(Holder + gun).GetComponent<ViewmodelReload>();
            Assert.That(reload, Is.Not.Null);
            var fields = new SerializedObject(reload);
            foreach (string field in new[] { "weapons", "data", "normalVisual", "reloadVisual", "animator", "clip", "sourceClip" })
                Assert.That(fields.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(reload.Clip.isHumanMotion, Is.False);
            Assert.That(reload.Clip.legacy, Is.False);
            Assert.That(reload.Clip.length, Is.EqualTo(1f).Within(0.02f));
            Assert.That(AssetDatabase.GetAssetPath(reload.SourceClip), Does.Contain(sourceName));
            Assert.That(reload.SourceClip.isHumanMotion, Is.True);
            Assert.That(reload.ShowingReload, Is.False);
            Assert.That(player.transform.Find(Holder + gun + "/Visual").gameObject.activeSelf, Is.True);
            var visual = player.transform.Find(Holder + gun + "/WinterReloadVisual");
            int total = 0;
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Assert.That(skin.sharedMesh.bindposes.Length, Is.EqualTo(skin.bones.Length));
                foreach (Transform bone in skin.bones) Assert.That(bone, Is.Not.Null);
                foreach (Material material in skin.sharedMaterials)
                    Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith("Assets/Materials/Models/"));
                for (int i = 0; i < skin.sharedMesh.subMeshCount; i++) total += (int)skin.sharedMesh.GetIndexCount(i) / 3;
            }
            Assert.That(total, Is.InRange(1000, 10000), "Winter Soldier arms retain the viewmodel budget");
            Assert.That(player.transform.Find(Holder + "Knife").GetComponent<ViewmodelReload>(), Is.Null);
        }

        [TestCase("SMG")]
        [TestCase("Rifle")]
        public void BakedClip_MovesTheMagazineWristAndFingers_ThenReturnsToApprovedPose(string gun)
        {
            GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
            ViewmodelReload reload = player.transform.Find(Holder + gun).GetComponent<ViewmodelReload>();
            Mesh mesh = new Mesh();
            try
            {
                // BakeMesh includes ancestor scaling before skinning; compare the actual vertex geometry at unit holder scale.
                player.transform.Find(Holder.TrimEnd('/')).localScale = Vector3.one;
                player.transform.Find(Holder + gun).gameObject.SetActive(true);
                reload.ApplyAt(0f);
                var rig = player.transform.Find(Holder + gun + "/WinterReloadVisual");
                Transform magazine = Find(rig, "Magazine"), wrist = Find(rig, "HandL"), finger = Find(rig, "Index1L");
                Vector3 magStart = magazine.position, wristStart = wrist.position;
                Quaternion fingerStart = finger.localRotation;
                Bounds start = BakedBounds(rig, mesh);
                Bounds original = RendererBounds(player.transform.Find(Holder + gun + "/Visual"));
                TestContext.WriteLine(gun + " start " + start.ToString("F4") + "; original " + original.ToString("F4"));
                Assert.That(Vector3.Distance(start.center, original.center), Is.LessThan(0.002f));
                Assert.That(Vector3.Distance(start.size, original.size), Is.LessThan(0.004f));
                reload.ApplyAt(0.45f);
                Assert.That(Vector3.Distance(magazine.position, magStart), Is.GreaterThan(0.01f));
                Assert.That(Vector3.Distance(wrist.position, wristStart), Is.GreaterThan(0.01f));
                Assert.That(Quaternion.Angle(fingerStart, finger.localRotation), Is.GreaterThan(3f));
                Bounds moving = BakedBounds(rig, mesh);
                Assert.That(moving.size.magnitude, Is.LessThan(3f), "no exploding skin vertices");
                reload.ApplyAt(1f);
                Bounds end = BakedBounds(rig, mesh);
                Assert.That(Vector3.Distance(end.center, start.center), Is.LessThan(0.002f));
                Assert.That(Vector3.Distance(end.size, start.size), Is.LessThan(0.004f));
                Assert.That(Vector3.Distance(magazine.position, magStart), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(wrist.position, wristStart), Is.LessThan(0.001f));
            }
            finally { reload.ResetVisual(); Object.DestroyImmediate(mesh); Object.DestroyImmediate(player); }
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            Assert.Fail("Missing bone " + name);
            return null;
        }

        private static Bounds BakedBounds(Transform root, Mesh mesh)
        {
            Bounds bounds = default;
            bool first = true;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.BakeMesh(mesh);
                foreach (Vector3 p in mesh.vertices)
                {
                    Vector3 world = skin.transform.TransformPoint(p);
                    Assert.That(float.IsNaN(world.sqrMagnitude) || float.IsInfinity(world.sqrMagnitude), Is.False);
                    if (first) { bounds = new Bounds(world, Vector3.zero); first = false; }
                    else bounds.Encapsulate(world);
                }
            }
            return bounds;
        }

        private static Bounds RendererBounds(Transform root)
        {
            Bounds bounds = default;
            bool first = true;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.GetComponent<MeshRenderer>().enabled) continue;
                bool visible = true;
                for (Transform t = filter.transform; t != root; t = t.parent) visible &= t.gameObject.activeSelf;
                if (!visible) continue; // Muzzle flash quads are present but hidden in the normal visual prefab.
                foreach (Vector3 p in filter.sharedMesh.vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(p);
                    if (first) { bounds = new Bounds(world, Vector3.zero); first = false; } else bounds.Encapsulate(world);
                }
            }
            return bounds;
        }
    }
}
