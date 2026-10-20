using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// Checks the real SM_SecurityRobot.fbx (step C8) against everything the Agent Controller's ProceduralRobotAnimator and the
    /// catalog rely on: joint names, rest rotation 0, the RobotRig offsets, the size, the budget and the facing. Skipped (not
    /// failed) while the FBX does not exist, because the placeholder path of RobotPrefabBuilder is tested separately.
    /// </summary>
    public class RobotModelFbxTests
    {
        private GameObject instance;

        [SetUp]
        public void SetUp()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabBuilder.FbxPath);
            if (fbx == null)
            {
                Assert.Ignore($"{RobotPrefabBuilder.FbxPath} does not exist yet.");
            }

            instance = Object.Instantiate(fbx);
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

        [Test]
        public void EveryJoint_IsThereUnderItsParentAtTheRigOffsetWithRotationZero()
        {
            foreach (RobotJoint joint in RobotRig.Joints)
            {
                Transform t = Find(joint.Name);
                Assert.IsNotNull(t, joint.Name);
                Assert.AreEqual(joint.Parent ?? instance.name, t.parent.name, joint.Name + " is parented as in art-pipeline.md 11.1.");
                Assert.That(Vector3.Distance(joint.Offset, t.localPosition), Is.LessThan(0.002f), $"{joint.Name} offset {t.localPosition}");
                Assert.That(Quaternion.Angle(Quaternion.identity, t.localRotation), Is.LessThan(0.1f), $"{joint.Name} rests at rotation 0, was {t.localEulerAngles}");
                Assert.That(Vector3.Distance(Vector3.one, t.localScale), Is.LessThan(0.0001f), joint.Name + " has scale 1.");
            }

            Assert.IsEmpty(RobotPrefabBuilder.MissingJoints(instance.transform));
        }

        [Test]
        public void EveryAnimatedJoint_RotatesAboutTheRobotsLeftRightAxis()
        {
            // ProceduralRobotAnimator turns joints about their local X: it must be the world X axis of the robot at rest.
            foreach (string name in RobotRig.AnimatedJoints)
            {
                Assert.That(Vector3.Distance(Vector3.right, Find(name).right), Is.LessThan(0.001f), name);
            }
        }

        [Test]
        public void Names_AreUniqueSoTheAnimatorFindsTheRightObject()
        {
            var names = new HashSet<string>();
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                Assert.IsTrue(names.Add(t.name), $"Duplicate name {t.name}");
            }
        }

        [Test]
        public void Size_MatchesTheCatalogAndTheFeetStandOnTheFloor()
        {
            Bounds bounds = ModelBounds();

            Assert.That(bounds.size.x, Is.InRange(1.03f, 1.11f), "1.07 m wide.");
            Assert.That(bounds.max.y, Is.InRange(2.35f, 2.41f), "2.38 m to the antenna tip.");
            Assert.That(bounds.min.y, Is.InRange(0f, 0.03f), "Feet on the floor.");
            Assert.That(bounds.size.z, Is.InRange(0.60f, 0.68f), "0.64 m deep.");
            Assert.AreEqual(0f, bounds.center.x, 0.03f, "Centred left to right.");
        }

        [Test]
        public void RobotFacesPlusZWithItsPodOnTheRightArmAtPlusX()
        {
            Assert.Greater(Find(RobotRig.HandR).position.x, 0.3f);
            Assert.Less(Find(RobotRig.HandL).position.x, -0.3f);
            Assert.Greater(Find(RobotRig.ScannerDot).position.z, Find(RobotRig.Head).position.z, "The scanner sits on the visor, the front (+Z).");

            // The right hand holds the carbine (the white rifle of the reference photo): it hangs from the hand and its
            // muzzle brake ends 0.33 m below the wrist, which is exactly RobotRig.MuzzleOffset, so shots leave the barrel.
            Bounds gun = Find(RobotRig.HandR).GetComponent<MeshRenderer>().bounds;
            Bounds claw = Find(RobotRig.HandL).GetComponent<MeshRenderer>().bounds;
            Assert.Greater(gun.size.y, claw.size.y + 0.15f, "The carbine is longer than the left claw.");
            Assert.AreEqual(Find(RobotRig.HandR).position.y + RobotRig.MuzzleOffset.y, gun.min.y, 0.02f, "The barrel tip is at the muzzle point.");
            Assert.Greater(gun.center.x, 0.3f, "and it is on the right (+X).");

            Bounds chest = Find(RobotRig.Chest).GetComponent<MeshRenderer>().bounds;
            Assert.Less(chest.min.z, -0.3f, "The back pack with the glowing cells is behind.");
        }

        [Test]
        public void AimPose_RaisesTheRightHandForwardAndUp()
        {
            // The animator's aim pose: the right upper arm turns -1.5 rad about its X axis (agent-controller.md 9.3).
            Transform hand = Find(RobotRig.HandR);
            Vector3 before = hand.position;

            Find(RobotRig.UpperArmR).localRotation = Quaternion.AngleAxis(-1.5f * Mathf.Rad2Deg, Vector3.right);

            Assert.Greater(hand.position.z - before.z, 0.3f, "The pod swings to the front.");
            Assert.Greater(hand.position.y - before.y, 0.3f, "and up.");
        }

        [Test]
        public void ThighSwing_CarriesTheShinAndFootWithIt()
        {
            Transform foot = Find(RobotRig.FootL);
            Vector3 before = foot.position;

            Find(RobotRig.ThighL).localRotation = Quaternion.AngleAxis(0.32f * Mathf.Rad2Deg, Vector3.right);

            Assert.Greater(Vector3.Distance(before, foot.position), 0.1f, "The foot is nested under the shin under the thigh.");
            Assert.That(Mathf.Abs(foot.position.x - before.x), Is.LessThan(0.001f), "A swing about X never moves the foot sideways.");
        }

        [Test]
        public void Budget_StaysUnderTheTargetWithAtMostFourMaterials()
        {
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

            Assert.That(triangles, Is.LessThanOrEqualTo(5000), "Target of docs/art-pipeline.md section 6.");
            Assert.That(materials.Count, Is.LessThanOrEqualTo(4));
            foreach (string name in materials)
            {
                Assert.That(name, Does.StartWith("M_SecurityRobot_"), "Material names equal the Unity assets.");
            }
        }
    }
}
