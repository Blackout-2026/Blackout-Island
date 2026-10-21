using System.Collections.Generic;
using Blackout.Agents;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// Checks the Security Patrol as the downloaded skinned soldier (step PS4): the FBX is a full skeleton of the right height and inside its budget, the rifle has its
    /// Gun and MuzzlePoint, the visual prefab holds that rifle in both hands with sensible elbows and curled fingers, wears the five textured materials, builds the same
    /// pose every time, and the agent prefab shoots from the rifle's muzzle. Assets that are not built yet fail these tests: they are the point of the step.
    /// </summary>
    public class PatrolSoldierTests
    {
        private const string VisualPath = "Assets/Prefabs/Models/Char_Patrol_Visual.prefab";
        private const string AgentPath = "Assets/Prefabs/Agents/SecurityPatrol.prefab";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }

            created.Clear();
        }

        private GameObject Instantiate(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(asset, path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            created.Add(instance);
            return instance;
        }

        // The real extent of what is drawn. A skinned renderer's stored bounds are padded for animation, so its vertices are baked in their current pose instead.
        private Bounds RendererBounds(GameObject instance)
        {
            Bounds bounds = default;
            bool first = true;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                Bounds one;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    var baked = new Mesh();
                    created.Add(baked);
                    skinned.BakeMesh(baked, true);
                    Matrix4x4 toWorld = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one); // the baked mesh is in the renderer's local space, scale applied
                    Vector3[] vertices = baked.vertices;
                    one = new Bounds(toWorld.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                    foreach (Vector3 vertex in vertices)
                    {
                        one.Encapsulate(toWorld.MultiplyPoint3x4(vertex));
                    }
                }
                else
                {
                    one = renderer.bounds;
                }

                if (first)
                {
                    bounds = one;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(one);
                }
            }

            return bounds;
        }

        [Test]
        public void Fbx_IsASkinnedSkeletonAboutAsTallAsTheRigWithItsFeetOnTheFloor()
        {
            GameObject soldier = Instantiate(PatrolSoldierBuilder.SoldierFbxPath);

            var skinned = soldier.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(skinned, "a skinned mesh, not rigid parts");
            Assert.That(skinned.bones.Length, Is.GreaterThanOrEqualTo(50), "the body and finger bones");
            Assert.IsNotNull(PatrolSoldierPose.Bone(soldier.transform, "Hips"));
            Assert.IsNotNull(PatrolSoldierPose.Bone(soldier.transform, "RightHandIndex3"));

            Bounds bounds = RendererBounds(soldier);
            Assert.That(bounds.max.y, Is.InRange(CharacterRig.Height - 0.08f, CharacterRig.Height + 0.08f), "the top of the head or the cap");
            Assert.That(bounds.min.y, Is.InRange(-0.03f, 0.03f), "feet on the floor");
        }

        [Test]
        public void Fbx_IsAValidHumanoidSoThatMixamoClipsCanDriveItLater()
        {
            Assert.IsTrue(AssetImporter.GetAtPath(PatrolSoldierBuilder.SoldierFbxPath) is ModelImporter importer
                          && importer.animationType == ModelImporterAnimationType.Human, "imported as a Humanoid");

            Avatar avatar = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(PatrolSoldierBuilder.SoldierFbxPath))
            {
                avatar = asset as Avatar ?? avatar;
            }

            Assert.IsNotNull(avatar, "the importer made an avatar");
            Assert.IsTrue(avatar.isValid && avatar.isHuman, "a valid humanoid avatar");
        }

        [Test]
        public void Fbx_StaysInsideTheHeroBudget()
        {
            GameObject soldier = Instantiate(PatrolSoldierBuilder.SoldierFbxPath);
            var skinned = soldier.GetComponentInChildren<SkinnedMeshRenderer>();

            Assert.IsTrue(ModelBudget.TryGet("SM_Char_PatrolSoldier", out ModelBudget budget));
            Assert.That(MeshStats.CountTriangles(skinned.sharedMesh), Is.LessThanOrEqualTo(budget.TargetTriangles));
            Assert.That(skinned.sharedMaterials.Length, Is.LessThanOrEqualTo(budget.MaxMaterials));
            Assert.IsTrue(MeshStats.HasUv0(skinned.sharedMesh));
        }

        [Test]
        public void Rifle_HasItsGunAndAMuzzlePointAtTheFlashHider()
        {
            GameObject rifle = Instantiate(PatrolSoldierBuilder.RifleFbxPath);

            Transform gun = RobotRig.FindDeep(rifle.transform, PatrolSoldierBuilder.GunName);
            Transform muzzle = RobotRig.FindDeep(rifle.transform, CharacterRig.MuzzlePoint);
            Assert.IsNotNull(gun);
            Assert.IsNotNull(muzzle);
            Assert.AreSame(gun, muzzle.parent);
            Assert.AreEqual(CharacterRig.Find("Patrol").MuzzleAhead, Vector3.Dot(muzzle.position - gun.position, gun.forward), 0.03f, "metres ahead of the origin");
            Assert.That(Vector3.Dot(muzzle.forward, gun.forward), Is.GreaterThan(0.99f));

            var filter = rifle.GetComponentInChildren<MeshFilter>();
            Assert.That(MeshStats.CountTriangles(filter.sharedMesh), Is.InRange(200, 2000), "a small, detailed rifle");
        }

        private Transform SoldierOf(GameObject visual)
        {
            Transform model = visual.transform.Find(PatrolSoldierBuilder.ModelName);
            Assert.IsNotNull(model, "the soldier's Model under the visual root");
            return model;
        }

        [Test]
        public void Visual_WearsTheFiveTexturedMaterialsOnAllSevenSlots()
        {
            GameObject visual = Instantiate(VisualPath);
            var skinned = SoldierOf(visual).GetComponentInChildren<SkinnedMeshRenderer>();

            var names = new HashSet<string>();
            foreach (Material material in skinned.sharedMaterials)
            {
                Assert.IsNotNull(material, "no slot is left empty or default");
                StringAssert.StartsWith("M_Char_PatrolSoldier_", material.name);
                Assert.IsNotNull(material.GetTexture("_BaseMap"), material.name + " has its texture");
                names.Add(material.name);
            }

            Assert.AreEqual(5, names.Count);
            var rifleMaterial = RobotRig.FindDeep(visual.transform, PatrolSoldierBuilder.GunName).GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.AreEqual(PatrolSoldierBuilder.RifleMaterialName, rifleMaterial.name, "the rifle wears the Kit atlas, not the importer's default");
            var body = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Models/M_Char_PatrolSoldier_Body.mat");
            Assert.IsNotNull(body.GetTexture("_BumpMap"), "the jacket's normal map");
        }

        [Test]
        public void Visual_FacesTheAgentsForwardAndStandsOnTheFloor()
        {
            GameObject visual = Instantiate(VisualPath);
            PatrolSoldierPose.Frame frame = PatrolSoldierPose.FrameOf(SoldierOf(visual));

            Assert.That(Vector3.Dot(frame.Forward, Vector3.forward), Is.GreaterThan(0.99f), "an agent's forward is +Z");
            Assert.That(Vector3.Dot(frame.Right, Vector3.right), Is.GreaterThan(0.99f), "and its right is +X");
            Bounds bounds = RendererBounds(visual);
            Assert.That(bounds.min.y, Is.InRange(-0.03f, 0.03f));
            Assert.That(bounds.max.y, Is.InRange(CharacterRig.Height - 0.08f, CharacterRig.Height + 0.08f));
        }

        [Test]
        public void Visual_CarriesTheRifleOnTheRightHandWithItsMuzzleInFrontOfTheChest()
        {
            GameObject visual = Instantiate(VisualPath);
            Transform model = SoldierOf(visual);
            PatrolSoldierPose.Frame frame = PatrolSoldierPose.FrameOf(model);

            Transform gun = RobotRig.FindDeep(visual.transform, PatrolSoldierBuilder.GunName);
            Transform muzzle = RobotRig.FindDeep(visual.transform, CharacterRig.MuzzlePoint);
            Assert.IsNotNull(gun);
            Assert.IsNotNull(muzzle);
            Assert.IsTrue(gun.IsChildOf(PatrolSoldierPose.Bone(model, "RightHand")), "the rifle follows the right hand");
            Assert.AreEqual(CharacterRig.Find("Patrol").MuzzleAhead, Vector3.Distance(gun.position, muzzle.position), 0.03f);

            Vector3 fromHips = muzzle.position - PatrolSoldierPose.Bone(model, "Hips").position;
            Assert.That(Vector3.Dot(fromHips, frame.Forward), Is.GreaterThan(0.8f), "well in front of the body");
            Assert.That(muzzle.position.y, Is.InRange(1.1f, 1.6f), "at chest height");
            Assert.That(Vector3.Dot(muzzle.forward, frame.Forward), Is.GreaterThan(0.9f), "the barrel points the way the soldier faces");
        }

        [TestCase("Right", PatrolSoldierPose.RightGripHalfWidth, 0.06f)]
        [TestCase("Left", PatrolSoldierPose.LeftGripHalfWidth, 0.07f)]
        public void Visual_PutsEachPalmOnItsGripWithABentElbow(string side, float halfWidth, float tolerance)
        {
            GameObject visual = Instantiate(VisualPath);
            Transform model = SoldierOf(visual);
            Transform gun = RobotRig.FindDeep(visual.transform, PatrolSoldierBuilder.GunName);
            Vector3 gripLocal = side == "Right" ? PatrolSoldierPose.RightGripLocal : PatrolSoldierPose.LeftGripLocal;

            float distance = Vector3.Distance(PatrolSoldierPose.PalmCentre(model, side), gun.TransformPoint(gripLocal));
            Assert.That(distance, Is.LessThanOrEqualTo(halfWidth + tolerance), side + " palm on the grip");

            Vector3 shoulder = PatrolSoldierPose.Bone(model, side + "Arm").position;
            Vector3 elbow = PatrolSoldierPose.Bone(model, side + "ForeArm").position;
            Vector3 wrist = PatrolSoldierPose.Bone(model, side + "Hand").position;
            float bend = Vector3.Angle(shoulder - elbow, wrist - elbow);
            Assert.That(bend, Is.InRange(40f, 172f), side + " elbow bends like an arm, not a straight pole or a folded one");
        }

        [Test]
        public void Visual_CurlsTheFingersTowardsThePalm()
        {
            GameObject posed = Instantiate(VisualPath);
            GameObject rest = Instantiate(PatrolSoldierBuilder.SoldierFbxPath);

            foreach (string side in new[] { "Right", "Left" })
            {
                float posedDistance = Vector3.Distance(PatrolSoldierPose.Bone(SoldierOf(posed), side + "HandIndex4").position, PatrolSoldierPose.PalmCentre(SoldierOf(posed), side));
                float restDistance = Vector3.Distance(PatrolSoldierPose.Bone(rest.transform, side + "HandIndex4").position, PatrolSoldierPose.PalmCentre(rest.transform, side));
                Assert.That(posedDistance, Is.LessThan(restDistance * 0.85f), side + " index fingertip comes in to the palm");
            }
        }

        [Test]
        public void Pose_IsTheSameEveryTimeItIsBuilt()
        {
            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(PatrolSoldierBuilder.SoldierFbxPath);
            var rifle = AssetDatabase.LoadAssetAtPath<GameObject>(PatrolSoldierBuilder.RifleFbxPath);
            Dictionary<string, Material> materials = InMemoryMaterials();

            var first = new GameObject("first");
            var second = new GameObject("second");
            created.Add(first);
            created.Add(second);
            Transform a = PatrolSoldierBuilder.Populate(first, soldier, rifle, materials);
            Transform b = PatrolSoldierBuilder.Populate(second, soldier, rifle, materials);

            foreach (string bone in new[] { "RightHand", "LeftHand", "RightForeArm", "LeftForeArm", "RightHandIndex3", "LeftHandThumb3" })
            {
                Assert.That(Vector3.Distance(PatrolSoldierPose.Bone(a, bone).position, PatrolSoldierPose.Bone(b, bone).position), Is.LessThan(1e-4f), bone);
            }
        }

        private Dictionary<string, Material> InMemoryMaterials()
        {
            var materials = new Dictionary<string, Material>();
            foreach (KeyValuePair<string, string> pair in PatrolSoldierBuilder.SlotMaterials)
            {
                if (!materials.ContainsKey(pair.Value))
                {
                    var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = pair.Value };
                    created.Add(material);
                    materials.Add(pair.Value, material);
                }
            }

            return materials;
        }

        [Test]
        public void SecurityPatrolPrefab_ShowsTheSoldier_AndShootsFromTheRiflesMuzzle()
        {
            GameObject agent = Instantiate(AgentPath);

            Assert.IsNull(agent.transform.Find("Visual/PLACEHOLDER_Humanoid"), "the blue capsule is gone");
            Transform visual = agent.transform.Find("Visual/Char_Patrol_Visual");
            Assert.IsNotNull(visual, "the soldier's visual nested in the agent");
            Assert.IsNotNull(visual.GetComponentInChildren<SkinnedMeshRenderer>(true));
            Assert.AreEqual(0, visual.GetComponentsInChildren<Collider>(true).Length, "the agent's own capsule is the collider");

            var serialized = new SerializedObject(agent.GetComponent<AgentShooter>());
            var muzzle = serialized.FindProperty("muzzle").objectReferenceValue as Transform;
            Assert.IsNotNull(muzzle);
            Assert.AreEqual(CharacterRig.MuzzlePoint, muzzle.name);
            Assert.IsTrue(muzzle.IsChildOf(visual), "the muzzle belongs to the soldier's rifle, not to a model that was swapped out");
        }
    }
}
