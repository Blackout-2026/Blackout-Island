using System.Collections.Generic;
using System.IO;
using Blackout.Agents;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// What every downloaded, skinned humanoid agent (the Security Patrol's soldier of step PS4, the Hunter of step HV1) has to be: a full skeleton of about the rig's height with its feet on
    /// the floor, inside its budget, with the rig type it needs; a rifle with its Gun and MuzzlePoint; a visual that faces +Z, wears its textured materials, carries the rifle on the
    /// right hand with the muzzle in front of the chest, holds it with both palms on the grips and bent elbows, curls its fingers, builds the same pose every time; and an agent prefab
    /// that shows it and shoots from the rifle's muzzle. Assets that are not built yet fail these tests: they are the point of the step. Each character is one small subclass.
    /// </summary>
    public abstract class SkinnedHumanoidTestsBase
    {
        private readonly List<Object> created = new List<Object>();

        protected abstract SkinnedHumanoidSpec Spec { get; }

        protected CharacterEntry Entry => CharacterRig.Find(Spec.Name);
        private HumanoidRiflePose Pose => Spec.Pose;

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

        protected GameObject Instantiate(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(asset, path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            created.Add(instance);
            return instance;
        }

        // The real extent of what is drawn. A skinned renderer's stored bounds are padded for animation, so its vertices are baked in their current pose instead.
        protected Bounds RendererBounds(GameObject instance)
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
                    skinned.BakeMesh(baked, true); // vertices in the renderer's local space, not scaled: the whole transform (an FBX in other units has a scale) takes them to the world
                    Matrix4x4 toWorld = skinned.transform.localToWorldMatrix;
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

        protected Transform ModelOf(GameObject visual)
        {
            Transform model = visual.transform.Find(SkinnedHumanoidBuilder.ModelName);
            Assert.IsNotNull(model, "the Model under the visual root");
            return model;
        }

        [Test]
        public void Fbx_IsASkinnedSkeletonAboutAsTallAsTheRigWithItsFeetOnTheFloor()
        {
            GameObject model = Instantiate(Spec.FbxPath);

            var skinned = model.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(skinned, "a skinned mesh, not rigid parts");
            Assert.That(skinned.bones.Length, Is.GreaterThanOrEqualTo(40), "the body and finger bones");
            Assert.IsNotNull(Spec.Pose.Bones.Hips(model.transform));
            Assert.IsNotNull(Spec.Pose.Bones.Finger(model.transform, "Right", "Index", 2));

            Bounds bounds = RendererBounds(model);
            Assert.That(bounds.max.y, Is.InRange(CharacterRig.Height - 0.08f, CharacterRig.Height + 0.08f), "the top of the head or the cap");
            Assert.That(bounds.min.y, Is.InRange(-0.03f, 0.03f), "feet on the floor");
        }

        [Test]
        public void Fbx_HasTheRigTypeItNeeds()
        {
            var importer = AssetImporter.GetAtPath(Spec.FbxPath) as ModelImporter;
            Assert.IsNotNull(importer);
            Assert.AreEqual(Spec.Rig, importer.animationType);
            Assert.AreNotEqual(ModelImporterAnimationType.None, importer.animationType, "a rig type of None drops the skin weights");

            if (Spec.Rig == ModelImporterAnimationType.Human)
            {
                Avatar avatar = null;
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(Spec.FbxPath))
                {
                    avatar = asset as Avatar ?? avatar;
                }

                Assert.IsNotNull(avatar, "the importer made an avatar");
                Assert.IsTrue(avatar.isValid && avatar.isHuman, "a valid humanoid avatar, so that Mixamo clips can drive it later");
            }
        }

        [Test]
        public void Fbx_StaysInsideItsBudget()
        {
            GameObject model = Instantiate(Spec.FbxPath);
            var skinned = model.GetComponentInChildren<SkinnedMeshRenderer>();

            Assert.IsTrue(ModelBudget.TryGet(Path.GetFileNameWithoutExtension(Spec.FbxPath), out ModelBudget budget));
            Assert.That(MeshStats.CountTriangles(skinned.sharedMesh), Is.LessThanOrEqualTo(budget.TargetTriangles));
            Assert.That(skinned.sharedMaterials.Length, Is.LessThanOrEqualTo(budget.MaxMaterials));
            Assert.IsTrue(MeshStats.HasUv0(skinned.sharedMesh));
        }

        [Test]
        public void Rifle_HasItsGunAndAMuzzlePointAtTheFlashHider()
        {
            GameObject rifle = Instantiate(Spec.RifleFbxPath);

            Transform gun = RobotRig.FindDeep(rifle.transform, SkinnedHumanoidBuilder.GunName);
            Transform muzzle = RobotRig.FindDeep(rifle.transform, CharacterRig.MuzzlePoint);
            Assert.IsNotNull(gun);
            Assert.IsNotNull(muzzle);
            Assert.AreSame(gun, muzzle.parent);
            Assert.AreEqual(Entry.MuzzleAhead, Vector3.Dot(muzzle.position - gun.position, gun.forward), 0.03f, "metres ahead of the origin");
            Assert.That(Vector3.Dot(muzzle.forward, gun.forward), Is.GreaterThan(0.99f));

            var filter = rifle.GetComponentInChildren<MeshFilter>();
            Assert.That(MeshStats.CountTriangles(filter.sharedMesh), Is.InRange(200, 2000), "a small, detailed rifle");
        }

        [Test]
        public void Visual_FacesTheAgentsForwardAndStandsOnTheFloor()
        {
            GameObject visual = Instantiate(Entry.VisualPrefabPath);
            HumanoidFrame frame = Pose.FrameOf(ModelOf(visual));

            Assert.That(Vector3.Dot(frame.Forward, Vector3.forward), Is.GreaterThan(0.99f), "an agent's forward is +Z");
            Assert.That(Vector3.Dot(frame.Right, Vector3.right), Is.GreaterThan(0.99f), "and its right is +X");
            Bounds bounds = RendererBounds(visual);
            Assert.That(bounds.min.y, Is.InRange(-0.03f, 0.03f));
            Assert.That(bounds.max.y, Is.InRange(CharacterRig.Height - 0.08f, CharacterRig.Height + 0.08f));
        }

        [Test]
        public void Visual_WearsItsTexturedMaterialsOnEverySlotAndTheRifleWearsTheKitAtlas()
        {
            GameObject visual = Instantiate(Entry.VisualPrefabPath);
            var skinned = ModelOf(visual).GetComponentInChildren<SkinnedMeshRenderer>();

            Assert.AreEqual(Spec.SlotMaterials.Length, skinned.sharedMaterials.Length, "one material per imported slot");
            var names = new HashSet<string>();
            foreach (Material material in skinned.sharedMaterials)
            {
                Assert.IsNotNull(material, "no slot is left empty or default");
                Assert.IsTrue(Spec.MaterialNames().Contains(material.name), material.name + " is one of the model's materials");
                Assert.IsNotNull(material.GetTexture("_BaseMap"), material.name + " has its texture");
                names.Add(material.name);
            }

            Assert.AreEqual(Spec.MaterialNames().Count, names.Count);
            var rifleMaterial = RobotRig.FindDeep(visual.transform, SkinnedHumanoidBuilder.GunName).GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.AreEqual(Spec.RifleMaterialName, rifleMaterial.name, "the rifle wears the Kit atlas, not the importer's default");
        }

        [Test]
        public void Visual_CarriesTheRifleOnTheRightHandWithItsMuzzleInFrontOfTheChest()
        {
            GameObject visual = Instantiate(Entry.VisualPrefabPath);
            Transform model = ModelOf(visual);
            HumanoidFrame frame = Pose.FrameOf(model);

            Transform gun = RobotRig.FindDeep(visual.transform, SkinnedHumanoidBuilder.GunName);
            Transform muzzle = RobotRig.FindDeep(visual.transform, CharacterRig.MuzzlePoint);
            Assert.IsNotNull(gun);
            Assert.IsNotNull(muzzle);
            Assert.IsTrue(gun.IsChildOf(Pose.Bones.Hand(model, "Right")), "the rifle follows the right hand");
            Assert.AreEqual(Entry.MuzzleAhead, Vector3.Distance(gun.position, muzzle.position), 0.03f);

            Vector3 fromHips = muzzle.position - Pose.Bones.Hips(model).position;
            Assert.That(Vector3.Dot(fromHips, frame.Forward), Is.GreaterThan(0.8f), "well in front of the body");
            Assert.That(muzzle.position.y, Is.InRange(1.1f, 1.6f), "at chest height");
            Assert.That(Vector3.Dot(muzzle.forward, frame.Forward), Is.GreaterThan(0.9f), "the barrel points the way the character faces");
        }

        [TestCase("Right", HumanoidRiflePose.RightGripHalfWidth, 0.06f)]
        [TestCase("Left", HumanoidRiflePose.LeftGripHalfWidth, 0.07f)]
        public void Visual_PutsEachPalmOnItsGripWithABentElbow(string side, float halfWidth, float tolerance)
        {
            GameObject visual = Instantiate(Entry.VisualPrefabPath);
            Transform model = ModelOf(visual);
            Transform gun = RobotRig.FindDeep(visual.transform, SkinnedHumanoidBuilder.GunName);
            Vector3 gripLocal = side == "Right" ? HumanoidRiflePose.RightGripLocal : HumanoidRiflePose.LeftGripLocal;

            float distance = Vector3.Distance(Pose.PalmCentre(model, side), gun.TransformPoint(gripLocal));
            Assert.That(distance, Is.LessThanOrEqualTo(halfWidth + tolerance), side + " palm on the grip");

            Vector3 shoulder = Pose.Bones.UpperArm(model, side).position;
            Vector3 elbow = Pose.Bones.ForeArm(model, side).position;
            Vector3 wrist = Pose.Bones.Hand(model, side).position;
            float bend = Vector3.Angle(shoulder - elbow, wrist - elbow);
            Assert.That(bend, Is.InRange(40f, 172f), side + " elbow bends like an arm, not a straight pole or a folded one");
        }

        // How straight a finger is: the distance from its first joint to its third divided by the length of its two bones (1 = straight, 0.77 = bent 80 degrees).
        // The Mixamo hand rests flat (1.0) and is bent to about 0.77; the Valve hand rests half-curled (about 0.95), so the test compares with each model's own rest pose and only asks
        // for a finger that is bent and not straighter than at rest. How well the hand wraps the grip is judged in the Unity-rendered pictures (CharacterPreviewRenderer).
        private float Straightness(HumanoidBones bones, Transform model, string side, string finger)
        {
            Vector3 first = bones.Finger(model, side, finger, 0).position;
            Vector3 second = bones.Finger(model, side, finger, 1).position;
            Vector3 third = bones.Finger(model, side, finger, 2).position;
            return Vector3.Distance(first, third) / (Vector3.Distance(first, second) + Vector3.Distance(second, third));
        }

        [Test]
        public void Visual_CurlsTheFingersRoundTheGrips()
        {
            GameObject posed = Instantiate(Entry.VisualPrefabPath);
            GameObject rest = Instantiate(Spec.FbxPath);

            foreach (string side in new[] { "Right", "Left" })
            {
                foreach (string finger in new[] { "Index", "Middle" })
                {
                    float posedStraightness = Straightness(Pose.Bones, ModelOf(posed), side, finger);
                    float restStraightness = Straightness(Pose.Bones, rest.transform, side, finger);
                    Assert.That(posedStraightness, Is.LessThan(0.95f), $"{side} {finger} finger is bent round the grip");
                    Assert.That(posedStraightness, Is.LessThan(restStraightness), $"{side} {finger} finger is bent more than at rest");
                }
            }
        }

        [Test]
        public void Pose_IsTheSameEveryTimeItIsBuilt()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Spec.FbxPath);
            var rifle = AssetDatabase.LoadAssetAtPath<GameObject>(Spec.RifleFbxPath);
            Dictionary<string, Material> materials = InMemoryMaterials();

            var first = new GameObject("first");
            var second = new GameObject("second");
            created.Add(first);
            created.Add(second);
            Transform a = SkinnedHumanoidBuilder.Populate(first, Spec, model, rifle, materials);
            Transform b = SkinnedHumanoidBuilder.Populate(second, Spec, model, rifle, materials);

            foreach (string side in new[] { "Right", "Left" })
            {
                Assert.That(Vector3.Distance(Pose.Bones.Hand(a, side).position, Pose.Bones.Hand(b, side).position), Is.LessThan(1e-4f), side + " hand");
                Assert.That(Vector3.Distance(Pose.Bones.ForeArm(a, side).position, Pose.Bones.ForeArm(b, side).position), Is.LessThan(1e-4f), side + " forearm");
                Assert.That(Vector3.Distance(Pose.Bones.Finger(a, side, "Index", 2).position, Pose.Bones.Finger(b, side, "Index", 2).position), Is.LessThan(1e-4f), side + " finger");
                Assert.That(Vector3.Distance(Pose.Bones.Finger(a, side, "Thumb", 2).position, Pose.Bones.Finger(b, side, "Thumb", 2).position), Is.LessThan(1e-4f), side + " thumb");
            }
        }

        private Dictionary<string, Material> InMemoryMaterials()
        {
            var materials = new Dictionary<string, Material>();
            foreach (string name in Spec.MaterialNames())
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                created.Add(material);
                materials.Add(name, material);
            }

            return materials;
        }

        [Test]
        public void AgentPrefab_ShowsTheModel_AndShootsFromTheRiflesMuzzle()
        {
            GameObject agent = Instantiate(Entry.AgentPrefab);

            Assert.IsNull(agent.transform.Find("Visual/PLACEHOLDER_Humanoid"), "the blue capsule is gone");
            Transform visual = agent.transform.Find("Visual/" + Entry.VisualPrefabName);
            Assert.IsNotNull(visual, "the visual nested in the agent");
            Assert.IsNotNull(visual.GetComponentInChildren<SkinnedMeshRenderer>(true));
            Assert.AreEqual(0, visual.GetComponentsInChildren<Collider>(true).Length, "the agent's own capsule is the collider");

            var serialized = new SerializedObject(agent.GetComponent<AgentShooter>());
            var muzzle = serialized.FindProperty("muzzle").objectReferenceValue as Transform;
            Assert.IsNotNull(muzzle);
            Assert.AreEqual(CharacterRig.MuzzlePoint, muzzle.name);
            Assert.IsTrue(muzzle.IsChildOf(visual), "the muzzle belongs to this model's rifle, not to a model that was swapped out");
        }
    }
}
