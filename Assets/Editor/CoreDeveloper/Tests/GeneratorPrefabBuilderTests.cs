using Blackout.Art;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class GeneratorPrefabBuilderTests
    {
        private GameObject root;
        private GeneratorMaterialSet materials;
        private VoidEventChannel channel;

        [SetUp]
        public void SetUp()
        {
            TestAssetFolders.Create();
            materials = GeneratorMaterialBuilder.BuildAll(TestAssetFolders.Materials, TestAssetFolders.Textures);
            channel = ScriptableObject.CreateInstance<VoidEventChannel>();
            root = new GameObject("Generator_Visual");
            GeneratorPrefabBuilder.Populate(root, null, materials, channel);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(channel);
            TestAssetFolders.Delete();
        }

        [Test]
        public void Populate_CreatesTheThreeSocketsAndTheModel()
        {
            Assert.IsNotNull(root.transform.Find(GeneratorPrefabBuilder.ModelName));
            Assert.IsNotNull(root.transform.Find(GeneratorPrefabBuilder.InteractPointName));
            Assert.IsNotNull(root.transform.Find(GeneratorPrefabBuilder.SparkName).GetComponent<ParticleSystem>());
            Assert.IsNotNull(root.transform.Find(GeneratorPrefabBuilder.HumName).GetComponent<AudioSource>());
        }

        [Test]
        public void Populate_InteractPointIsAtTheControlFaceOnPlusX()
        {
            Vector3 point = root.transform.Find(GeneratorPrefabBuilder.InteractPointName).localPosition;

            Assert.Greater(point.x, 3.2f);
            Assert.That(point.y, Is.InRange(0.5f, 2f));
        }

        [Test]
        public void Populate_HumIsLoopingAndFullyThreeDimensional()
        {
            var hum = root.transform.Find(GeneratorPrefabBuilder.HumName).GetComponent<AudioSource>();

            Assert.IsTrue(hum.loop);
            Assert.AreEqual(1f, hum.spatialBlend);
        }

        [Test]
        public void Populate_SparksWaitForTheEvent()
        {
            var sparks = root.transform.Find(GeneratorPrefabBuilder.SparkName).GetComponent<ParticleSystem>();

            Assert.IsFalse(sparks.main.playOnAwake);
            Assert.IsFalse(sparks.isPlaying);
        }

        [Test]
        public void Populate_AddsAColliderProxyAndNoInteractionScripts()
        {
            var box = root.GetComponent<BoxCollider>();

            Assert.IsNotNull(box);
            Assert.AreEqual(new Vector3(6.4f, 3.6f, 3.2f), box.size);
            Assert.IsNull(root.GetComponent<Rigidbody>());
            Assert.AreEqual(0, root.GetComponentsInChildren<Collider>().Length - 1, "Only the root has a collider.");
        }

        [Test]
        public void Populate_PlaceholderHasTheRealHeightAndDepth()
        {
            Bounds bounds = default;
            bool first = true;
            foreach (MeshRenderer renderer in root.transform.Find(GeneratorPrefabBuilder.ModelName).GetComponentsInChildren<MeshRenderer>())
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

            Assert.AreEqual(4.1f, bounds.max.y, 0.01f);
            Assert.AreEqual(0f, bounds.min.y, 0.01f);
            Assert.AreEqual(2.9f, bounds.size.z, 0.01f);
        }

        [Test]
        public void Populate_WiresTheVisualsToTheCoreAndTheSockets()
        {
            var visuals = root.GetComponent<GeneratorVisuals>();
            var serialized = new SerializedObject(visuals);

            Assert.AreEqual(channel, serialized.FindProperty("generatorDisabled").objectReferenceValue);
            Assert.AreEqual(materials.CoreOff, serialized.FindProperty("coreOffMaterial").objectReferenceValue);
            Assert.AreEqual(root.transform.Find(GeneratorPrefabBuilder.HumName).GetComponent<AudioSource>(), serialized.FindProperty("humSource").objectReferenceValue);
            Assert.AreEqual(root.transform.Find(GeneratorPrefabBuilder.SparkName).GetComponent<ParticleSystem>(), serialized.FindProperty("sparks").objectReferenceValue);
            var core = (Renderer)serialized.FindProperty("coreRenderer").objectReferenceValue;
            Assert.IsNotNull(core);
            Assert.AreEqual(materials.Core, core.sharedMaterials[serialized.FindProperty("coreMaterialSlot").intValue]);
        }

        [Test]
        public void RaisingTheChannel_SwitchesTheCoreOffAndPlaysTheSparks()
        {
            var visuals = root.GetComponent<GeneratorVisuals>();
            visuals.Bind(channel);

            channel.Raise();

            var serialized = new SerializedObject(visuals);
            var core = (Renderer)serialized.FindProperty("coreRenderer").objectReferenceValue;
            Assert.AreEqual(materials.CoreOff, core.sharedMaterials[serialized.FindProperty("coreMaterialSlot").intValue]);
            Assert.IsTrue(root.transform.Find(GeneratorPrefabBuilder.SparkName).GetComponent<ParticleSystem>().isPlaying);
            visuals.Unbind();
        }

        [Test]
        public void Populate_TwiceDoesNotDuplicateAnything()
        {
            GeneratorPrefabBuilder.Populate(root, null, materials, channel);

            Assert.AreEqual(4, root.transform.childCount, "Model plus three sockets.");
            Assert.AreEqual(1, root.GetComponents<GeneratorVisuals>().Length);
            Assert.AreEqual(1, root.GetComponents<BoxCollider>().Length);
        }

        [Test]
        public void Populate_KeepsTheExistingPlaceholderWhenThereIsStillNoFbx()
        {
            Transform before = root.transform.Find(GeneratorPrefabBuilder.ModelName);
            int id = before.gameObject.GetInstanceID();

            GeneratorPrefabBuilder.Populate(root, null, materials, channel);

            Assert.AreEqual(id, root.transform.Find(GeneratorPrefabBuilder.ModelName).gameObject.GetInstanceID());
        }

        [Test]
        public void Populate_RepairsACoreLeftSwitchedOffAndSavesTheRunningMaterial()
        {
            var visuals = root.GetComponent<GeneratorVisuals>();
            var core = (Renderer)new SerializedObject(visuals).FindProperty("coreRenderer").objectReferenceValue;
            core.sharedMaterial = materials.CoreOff; // what a Power Down test with Auto Save leaves behind

            GeneratorPrefabBuilder.Populate(root, null, materials, channel);

            Assert.AreEqual(materials.Core, core.sharedMaterial);
            Assert.AreEqual(materials.Core, new SerializedObject(visuals).FindProperty("coreOnMaterial").objectReferenceValue);
        }

        [Test]
        public void Populate_KeepsASocketMovedByHand()
        {
            Transform point = root.transform.Find(GeneratorPrefabBuilder.InteractPointName);
            point.localPosition = new Vector3(9f, 9f, 9f);

            GeneratorPrefabBuilder.Populate(root, null, materials, channel);

            Assert.AreEqual(new Vector3(9f, 9f, 9f), root.transform.Find(GeneratorPrefabBuilder.InteractPointName).localPosition);
        }

        [Test]
        public void AssignMaterials_ReplacesSlotsByBlenderMaterialName()
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var imported = new Material(Shader.Find("Sprites/Default")) { name = GeneratorMaterialBuilder.CoreName };
            var unknown = new Material(Shader.Find("Sprites/Default")) { name = "M_Generator_Typo" };
            part.GetComponent<Renderer>().sharedMaterials = new[] { imported, unknown };

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("matches no M_Generator_"));
            GeneratorPrefabBuilder.AssignMaterials(part, materials);

            Material[] slots = part.GetComponent<Renderer>().sharedMaterials;
            Assert.AreEqual(materials.Core, slots[0]);
            Assert.AreSame(unknown, slots[1], "An unknown name is left as imported.");
            (Renderer renderer, int slot) = GeneratorPrefabBuilder.FindCore(part, materials.Core);
            Assert.AreSame(part.GetComponent<Renderer>(), renderer);
            Assert.AreEqual(0, slot);

            Object.DestroyImmediate(part);
            Object.DestroyImmediate(imported);
            Object.DestroyImmediate(unknown);
        }

        [Test]
        public void FindCore_ReturnsNothingWhenNoSlotUsesTheCore()
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);

            (Renderer renderer, int slot) = GeneratorPrefabBuilder.FindCore(part, materials.Core);

            Assert.IsNull(renderer);
            Assert.AreEqual(-1, slot);
            Object.DestroyImmediate(part);
        }
    }
}
