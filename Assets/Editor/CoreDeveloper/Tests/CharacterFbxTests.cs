using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// Checks the three real humanoid FBX files (step C10: SM_Char_Patrol, SM_Char_Hunter, SM_Char_Commander) against what the
    /// Agent Controller needs: the 17 joints at their heights with rest rotation 0, the 1.87 m height with the feet on the floor,
    /// the Gun socket with a MuzzlePoint that looks along +Z at the distance of the weapon, and the budget. A file that does not
    /// exist yet is skipped, not failed.
    /// </summary>
    public class CharacterFbxTests
    {
        private GameObject instance;

        private CharacterEntry Load(string characterName)
        {
            CharacterEntry entry = CharacterRig.Find(characterName);
            Assert.IsNotNull(entry, characterName);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(entry.FbxPath);
            if (fbx == null)
            {
                Assert.Ignore($"{entry.FbxPath} does not exist yet.");
            }

            instance = Object.Instantiate(fbx);
            return entry;
        }

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }

        private Transform Find(string objectName)
        {
            return RobotRig.FindDeep(instance.transform, objectName);
        }

        private Bounds ModelBounds()
        {
            Bounds bounds = default;
            bool first = true;
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        [TestCase("Patrol")]
        [TestCase("Hunter")]
        [TestCase("Commander")]
        public void EveryJoint_IsThereAtItsHeightWithRotationZeroAndTurnsAboutTheLeftRightAxis(string characterName)
        {
            Load(characterName);

            Assert.IsNotNull(instance.transform.Find("Hips"), "Hips is a direct child of the model root.");
            foreach (KeyValuePair<string, float> joint in CharacterRig.JointHeights)
            {
                Transform t = Find(joint.Key);
                Assert.IsNotNull(t, joint.Key);
                Assert.AreEqual(joint.Value, t.position.y, 0.01f, joint.Key + " height");
                Assert.That(Quaternion.Angle(Quaternion.identity, t.localRotation), Is.LessThan(0.1f), joint.Key + " rests at rotation 0");
                Assert.That(Vector3.Distance(Vector3.right, t.right), Is.LessThan(0.001f), joint.Key + " turns about the robot's X axis");
            }
        }

        [TestCase("Patrol")]
        [TestCase("Hunter")]
        [TestCase("Commander")]
        public void Names_AreUniqueSoAnAnimatorFindsTheRightObject(string characterName)
        {
            Load(characterName);

            var names = new HashSet<string>();
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                Assert.IsTrue(names.Add(t.name), $"Duplicate name {t.name}");
            }
        }

        [TestCase("Patrol")]
        [TestCase("Hunter")]
        [TestCase("Commander")]
        public void Size_IsAbout1Point87MetresTallWithTheFeetOnTheFloor(string characterName)
        {
            Load(characterName);
            Bounds bounds = ModelBounds();

            Assert.That(bounds.max.y, Is.InRange(CharacterRig.Height - 0.06f, CharacterRig.Height + 0.06f), "to the top of the head or helmet");
            Assert.That(bounds.min.y, Is.InRange(-0.01f, 0.03f), "feet on the floor");
            Assert.That(bounds.size.x, Is.InRange(0.45f, 0.85f), "a slim body, with the rifle held in front");
        }

        [TestCase("Patrol")]
        [TestCase("Hunter")]
        [TestCase("Commander")]
        public void MuzzlePoint_IsOnTheGunAndLooksAlongPlusZAtTheWeaponsLength(string characterName)
        {
            CharacterEntry entry = Load(characterName);
            Transform gun = Find(CharacterRig.Gun);
            Transform muzzle = Find(CharacterRig.MuzzlePoint);

            Assert.IsNotNull(gun);
            Assert.IsNotNull(muzzle, "the Gun's child MuzzlePoint");
            Assert.AreSame(gun, muzzle.parent);
            Assert.AreEqual(entry.MuzzleAhead, muzzle.position.z - gun.position.z, 0.03f, "metres ahead of the socket");
            Assert.That(Vector3.Dot(muzzle.forward, Vector3.forward), Is.GreaterThan(0.99f), "a shot goes forward (+Z)");
            Assert.Greater(muzzle.position.z, Find("Hips").position.z + 0.35f, "in front of the body");
        }

        [TestCase("Patrol")]
        [TestCase("Hunter")]
        [TestCase("Commander")]
        public void Budget_StaysUnderTheTargetWithAtMostThreeMaterials(string characterName)
        {
            Load(characterName);

            int triangles = 0;
            var materials = new HashSet<string>();
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                triangles += MeshStats.CountTriangles(filter.sharedMesh);
                Assert.IsTrue(filter.sharedMesh.uv.Length > 0, filter.name + " has UV0.");
            }

            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    materials.Add(material.name);
                }
            }

            Assert.IsTrue(ModelBudget.TryGet($"SM_Char_{characterName}", out ModelBudget budget), "a budget exists for the character");
            Assert.That(triangles, Is.LessThanOrEqualTo(budget.TargetTriangles), "target of docs/art-pipeline.md section 6 for SM_Char_ (the Commander is the hero, 18.2)");
            Assert.That(materials.Count, Is.LessThanOrEqualTo(budget.MaxMaterials));
            foreach (string materialName in materials)
            {
                Assert.That(materialName, Does.StartWith("M_Char_" + characterName), "material names equal the Unity assets");
            }
        }
    }
}
