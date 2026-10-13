using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blackout.Editor.Tests
{
    public class RobotPrefabBuilderTests
    {
        private GameObject root;
        private RobotMaterialSet materials;

        [SetUp]
        public void SetUp()
        {
            TestAssetFolders.Create();
            materials = RobotMaterialBuilder.BuildAll(TestAssetFolders.Materials, TestAssetFolders.Textures);
            root = new GameObject("SecurityRobot_Visual");
            RobotPrefabBuilder.Populate(root, null, materials);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            TestAssetFolders.Delete();
        }

        private Transform Find(string objectName)
        {
            return RobotRig.FindDeep(root.transform, objectName);
        }

        private Bounds ModelBounds()
        {
            Bounds bounds = default;
            bool first = true;
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
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

        [Test]
        public void Populate_BuildsEveryJointUnderItsParentAtTheRestOffset()
        {
            foreach (RobotJoint joint in RobotRig.Joints)
            {
                Transform t = Find(joint.Name);
                Assert.IsNotNull(t, joint.Name);
                Assert.AreEqual(joint.Parent ?? RobotPrefabBuilder.ModelName, t.parent.name, joint.Name);
                Assert.AreEqual(joint.Offset, t.localPosition, joint.Name);
                Assert.AreEqual(Quaternion.identity, t.localRotation, joint.Name + " rests at rotation (0, 0, 0).");
                Assert.AreEqual(Vector3.one, t.localScale, joint.Name);
            }

            Assert.IsEmpty(RobotPrefabBuilder.MissingJoints(root.transform));
        }

        [Test]
        public void Populate_JointHeightsMatchThePrototypeTable()
        {
            // core-developer.md 3.2, "Joint heights (rest pose, Y above floor)".
            var expected = new Dictionary<string, float>
            {
                { RobotRig.Pelvis, 1.05f }, { RobotRig.Spine, 1.19f }, { RobotRig.Chest, 1.49f }, { RobotRig.Head, 1.93f },
                { RobotRig.UpperArmL, 1.76f }, { RobotRig.ForeArmL, 1.42f }, { RobotRig.HandL, 1.10f },
                { RobotRig.ThighR, 0.99f }, { RobotRig.ShinR, 0.57f }, { RobotRig.FootR, 0.14f },
            };

            foreach (KeyValuePair<string, float> pair in expected)
            {
                Assert.AreEqual(pair.Value, Find(pair.Key).position.y, 0.005f, pair.Key);
            }
        }

        [Test]
        public void Populate_PlaceholderHasTheRealSize()
        {
            Bounds bounds = ModelBounds();

            Assert.AreEqual(2.38f, bounds.max.y, 0.02f, "To the antenna tip.");
            Assert.That(bounds.min.y, Is.InRange(0f, 0.03f), "Feet on the floor.");
            Assert.That(bounds.size.x, Is.InRange(1.0f, 1.1f), "1.07 m wide.");
            Assert.That(bounds.size.z, Is.InRange(0.6f, 0.7f), "0.64 m deep.");
            Assert.AreEqual(0f, bounds.center.x, 0.03f, "Centred left to right.");
        }

        [Test]
        public void Populate_RobotFacesPlusZWithItsPodOnTheRightArmAtPlusX()
        {
            Assert.Greater(Find(RobotRig.UpperArmR).position.x, 0.3f);
            Assert.Less(Find(RobotRig.UpperArmL).position.x, -0.3f);
            Assert.Greater(Find("AmmoDrum_R").position.x, 0.4f, "The drum is on the outer side of the pod.");
            Assert.IsNotNull(Find("PodBlock_R"));
            Assert.IsNull(Find("PodBlock_L"));
            Assert.IsNotNull(Find("Palm_L"), "The left arm ends in the claw.");
            Assert.Greater(Find("EyeBar").position.z, Find(RobotRig.Head).position.z, "The eye is on the front (+Z) of the head.");
            Assert.Less(Find("PowerPack").position.z, Find(RobotRig.Chest).position.z, "The back pack is behind.");
        }

        [Test]
        public void Populate_PutsBothSocketsOnTheRightHand()
        {
            Transform hand = Find(RobotRig.HandR);
            Transform weapon = Find(RobotRig.WeaponSocket);
            Transform muzzle = Find(RobotRig.MuzzlePoint);

            Assert.AreSame(hand, weapon.parent);
            Assert.AreSame(hand, muzzle.parent);
            Assert.AreEqual(RobotRig.MuzzleOffset, muzzle.localPosition);
        }

        [Test]
        public void Muzzle_PointsAlongTheBarrelsAndForwardWhenTheArmIsRaised()
        {
            Transform muzzle = Find(RobotRig.MuzzlePoint);
            Assert.That(Vector3.Dot(muzzle.forward, Vector3.down), Is.GreaterThan(0.999f), "Hanging arm: the barrels point down.");

            // The animator's aim pose: the right upper arm rotates -1.5 rad about its X axis (agent-controller.md 9.3).
            Find(RobotRig.UpperArmR).localRotation = Quaternion.AngleAxis(-1.5f * Mathf.Rad2Deg, Vector3.right);

            Assert.That(Vector3.Dot(muzzle.forward, Vector3.forward), Is.GreaterThan(0.95f), "Raised arm: the muzzle shoots forward.");
        }

        [Test]
        public void Populate_IsVisualOnlyWithNoColliderRigidbodyOrScript()
        {
            Assert.AreEqual(0, root.GetComponentsInChildren<Collider>(true).Length);
            Assert.AreEqual(0, root.GetComponentsInChildren<Rigidbody>(true).Length);
            Assert.AreEqual(0, root.GetComponentsInChildren<MonoBehaviour>(true).Length);
        }

        [Test]
        public void Populate_EveryRendererUsesMaterialsOfTheSetAndAllFiveAreUsed()
        {
            var used = new HashSet<Material>();
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.IsNotNull(renderer.sharedMaterial, renderer.name);
                Assert.IsNotNull(materials.Find(renderer.sharedMaterial.name), renderer.name);
                used.Add(renderer.sharedMaterial);
            }

            Assert.AreEqual(5, used.Count);
        }

        [Test]
        public void Populate_EyeBarAndAntennaTipGlowRedAndTheCellsGlowCyan()
        {
            Assert.AreEqual(materials.Eye, Find("EyeBar").GetComponent<Renderer>().sharedMaterial);
            Assert.AreEqual(materials.Eye, Find("AntennaTip").GetComponent<Renderer>().sharedMaterial);
            Assert.AreEqual(materials.Eye, Find(RobotRig.ScannerDot).GetComponent<Renderer>().sharedMaterial);
            for (int k = 1; k <= 3; k++)
            {
                Assert.AreEqual(materials.Cells, Find($"PowerCell{k}").GetComponent<Renderer>().sharedMaterial);
            }
        }

        [Test]
        public void Populate_AllNamesAreUniqueSoTheAnimatorFindsTheRightObject()
        {
            var names = new HashSet<string>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.IsTrue(names.Add(t.name), $"Duplicate name {t.name}");
            }
        }

        [Test]
        public void Populate_TwiceDoesNotDuplicateAnything()
        {
            int before = root.GetComponentsInChildren<Transform>(true).Length;
            int id = Find(RobotPrefabBuilder.ModelName).gameObject.GetInstanceID();

            RobotPrefabBuilder.Populate(root, null, materials);

            Assert.AreEqual(before, root.GetComponentsInChildren<Transform>(true).Length);
            Assert.AreEqual(id, Find(RobotPrefabBuilder.ModelName).gameObject.GetInstanceID(), "The placeholder is kept while there is no FBX.");
            Assert.AreEqual(1, root.transform.childCount);
        }

        [Test]
        public void Populate_KeepsASocketMovedByHand()
        {
            Find(RobotRig.MuzzlePoint).localPosition = new Vector3(0f, -0.4f, 0.01f);

            RobotPrefabBuilder.Populate(root, null, materials);

            Assert.AreEqual(new Vector3(0f, -0.4f, 0.01f), Find(RobotRig.MuzzlePoint).localPosition);
        }

        [Test]
        public void MissingJoints_ListsEveryNameOfAModelThatHasNone()
        {
            var empty = new GameObject("Empty");

            Assert.AreEqual(RobotRig.Joints.Length, RobotPrefabBuilder.MissingJoints(empty.transform).Count);
            Object.DestroyImmediate(empty);
        }

        [Test]
        public void AssignMaterials_ReplacesSlotsByBlenderMaterialName()
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var imported = new Material(Shader.Find("Sprites/Default")) { name = RobotMaterialBuilder.EyeName };
            var unknown = new Material(Shader.Find("Sprites/Default")) { name = "M_SecurityRobot_Typo" };
            part.GetComponent<Renderer>().sharedMaterials = new[] { imported, unknown };

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("matches no M_SecurityRobot_"));
            RobotPrefabBuilder.AssignMaterials(part, materials);

            Material[] slots = part.GetComponent<Renderer>().sharedMaterials;
            Assert.AreEqual(materials.Eye, slots[0]);
            Assert.AreSame(unknown, slots[1], "An unknown name is left as imported.");

            Object.DestroyImmediate(part);
            Object.DestroyImmediate(imported);
            Object.DestroyImmediate(unknown);
        }
    }
}
