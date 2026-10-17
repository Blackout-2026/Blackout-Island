using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blackout.Environment;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Blackout.Editor.Tests
{
    public class BunkerDresserTests
    {
        private Scene scene;
        private Transform container;
        private BunkerLayout layout;
        private List<MarkerSpec> markers;
        private List<string> messages;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject(BunkerDresser.ContainerName);
            SceneManager.MoveGameObjectToScene(root, scene);
            container = root.transform;
            layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(BunkerLayoutAssetBuilder.AssetPath);
            markers = MarkerLayout.Build(layout);
            messages = new List<string>();
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.ClosePreviewScene(scene);

        private DressingSpec Crate(string prefab = "Prop_CrateSmall", float x = 5, float z = 72)
            => new DressingSpec("SecurityArea", "TestCrate", prefab, new Vector3(x, 3, z), 0, Vector3.one);

        private int Build(params DressingSpec[] specs) => BunkerDresser.Dress(container, specs, layout, markers, messages.Add);

        [Test]
        public void MissingPrefab_IsSkippedWithoutCreatingRoom()
        {
            Assert.That(Build(Crate("Prop_MissingW10Test")), Is.Zero);
            Assert.That(container.childCount, Is.Zero);
            Assert.That(messages.Single(), Does.Contain("missing Assets/Prefabs/Props/Prop_MissingW10Test.prefab"));
        }

        [Test]
        public void Rerun_ReusesLinkedInstanceAndPreservesHandAddedChildren()
        {
            Assert.That(Build(Crate()), Is.EqualTo(1));
            Transform instance = container.Find("SecurityArea/SecurityArea_TestCrate");
            var handAdded = new GameObject("HandAdded");
            handAdded.transform.SetParent(instance, false);
            Assert.That(Build(Crate()), Is.EqualTo(1));
            Assert.That(container.Find("SecurityArea/SecurityArea_TestCrate"), Is.SameAs(instance));
            Assert.That(instance.Find("HandAdded"), Is.SameAs(handAdded.transform));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject),
                Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(Crate().PrefabPath)));
            Assert.That(messages, Is.Empty);
        }

        [Test]
        public void NameCollision_DoesNotReplaceHandMadeObject()
        {
            var room = new GameObject("SecurityArea");
            room.transform.SetParent(container, false);
            var handMade = new GameObject("SecurityArea_TestCrate");
            handMade.transform.SetParent(room.transform, false);
            Assert.That(Build(Crate()), Is.Zero);
            Assert.That(room.transform.GetChild(0).gameObject, Is.SameAs(handMade));
            Assert.That(messages.Single(), Does.Contain("different source"));
        }

        [TestCase(0, 76, "walking path")]
        [TestCase(9, 72, "outside room footprint")]
        [TestCase(3, 74, "AgentSpawn_Patrol-2")]
        public void UnsafePlacement_IsRejectedBeforeSceneChanges(float x, float z, string reason)
        {
            Assert.That(Build(Crate(x: x, z: z)), Is.Zero);
            Assert.That(container.childCount, Is.Zero);
            Assert.That(messages.Single(), Does.Contain(reason));
        }

        [Test]
        public void ChangedTable_DoesNotDeleteOldOrHandAddedObjects()
        {
            Assert.That(Build(Crate()), Is.EqualTo(1));
            Transform instance = container.Find("SecurityArea/SecurityArea_TestCrate");
            Assert.That(Build(), Is.Zero);
            Assert.That(instance, Is.Not.Null);
            Assert.That(container.Find("SecurityArea/SecurityArea_TestCrate"), Is.SameAs(instance));
        }

        [Test]
        public void DynamicComponents_AreDetected()
        {
            var child = new GameObject("Dynamic");
            child.transform.SetParent(container, false);
            Assert.That(BunkerDresser.HasDynamicContent(container.gameObject), Is.False);
            child.AddComponent<Rigidbody>();
            Assert.That(BunkerDresser.HasDynamicContent(container.gameObject), Is.True);
        }

        [Test]
        public void Bounds_IncludeRotationAndInvisibleCollider()
        {
            var child = new GameObject("ColliderOnly");
            child.transform.SetParent(container, false);
            BoxCollider collider = child.AddComponent<BoxCollider>();
            collider.size = new Vector3(4, 1, 2);
            child.transform.SetPositionAndRotation(new Vector3(3, 4, 5), Quaternion.Euler(0, 90, 0));
            Assert.That(BunkerDresser.TryBounds(child, out Bounds bounds), Is.True);
            Assert.That(Vector3.Distance(bounds.center, new Vector3(3, 4, 5)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(bounds.size, new Vector3(2, 1, 4)), Is.LessThan(0.001f));
        }

        [Test]
        public void Bounds_UsePhysicsSphereScaleAndRejectCollapsedShapes()
        {
            var child = new GameObject("Sphere");
            child.transform.SetParent(container, false);
            child.AddComponent<SphereCollider>().radius = 0.5f;
            child.transform.localScale = new Vector3(2, 1, 1);
            Assert.That(BunkerDresser.TryBounds(child, out Bounds bounds), Is.True);
            Assert.That(bounds.size, Is.EqualTo(Vector3.one * 2), "Physics uses the largest scale for a sphere radius.");
            child.transform.localScale = Vector3.zero;
            Assert.That(BunkerDresser.TryBounds(child, out _), Is.False);
        }

        [Test]
        public void ExistingTablePrefabs_AllFitClearanceAndRoomEnvelopes()
        {
            List<DressingSpec> specs = DressingLayout.Parse(File.ReadAllText(DressingLayout.DocumentPath));
            DressingSpec[] available = specs.Where(s => AssetDatabase.LoadAssetAtPath<GameObject>(s.PrefabPath) != null).ToArray();
            Assert.That(Build(available), Is.EqualTo(available.Length), string.Join("\n", messages));
            Assert.That(messages, Is.Empty);
        }

        [Test]
        public void StaticBodyAndDynamicScreen_AreSetWithoutEditingMaterials()
        {
            var spec = new DressingSpec("SecurityArea", "Monitor", "Prop_Monitor", new Vector3(6.6f, 3.28f, 76), 270, Vector3.one);
            Assert.That(Build(spec), Is.EqualTo(1));
            Transform monitor = container.Find("SecurityArea/SecurityArea_Monitor");
            Renderer screen = monitor.Find("Model/Screen").GetComponent<Renderer>();
            Assert.That(GameObjectUtility.GetStaticEditorFlags(screen.gameObject), Is.EqualTo((StaticEditorFlags)0));
            Assert.That(GameObjectUtility.GetStaticEditorFlags(monitor.Find("Model/Foot").gameObject), Is.EqualTo(StaticFlagRules.Environment));

            var powerObject = new GameObject("PowerStateLighting");
            SceneManager.MoveGameObjectToScene(powerObject, scene);
            PowerStateLighting power = powerObject.AddComponent<PowerStateLighting>();
            GameObject manual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            manual.transform.SetParent(container, false);
            var serialized = new SerializedObject(power);
            SerializedProperty screens = serialized.FindProperty("screens");
            screens.arraySize = 1;
            screens.GetArrayElementAtIndex(0).objectReferenceValue = manual.GetComponent<Renderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BunkerDresser.WireScreens(power, container);
            BunkerDresser.WireScreens(power, container);
            serialized.Update();
            Assert.That(screens.arraySize, Is.EqualTo(2));
            Assert.That(screens.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(manual.GetComponent<Renderer>()));
            Assert.That(screens.GetArrayElementAtIndex(1).objectReferenceValue, Is.SameAs(screen));
        }
    }
}
