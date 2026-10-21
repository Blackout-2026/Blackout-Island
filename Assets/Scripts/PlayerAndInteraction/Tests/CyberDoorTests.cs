using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Interaction.Tests
{
    /// <summary>
    /// The cyberpunk double door of step D1: SlidingDoor's second panel parts the other way, and Door.prefab (shared by D-Bunker, D-Security,
    /// D-Generator and D-Lab) shows a frame and two leaves that exactly fill the frozen 1.4 x 2.3 m doorway and slide out of it into the wall.
    /// </summary>
    public class CyberDoorTests
    {
        private const string DoorPath = "Assets/Prefabs/Interactables/Door.prefab";
        private const string ScenePath = "Assets/Scenes/Interactables.unity";
        private const float GapWidth = 1.4f;
        private const float GapHeight = 2.3f;
        private const float WallThickness = 0.4f;
        private const float Tolerance = 0.03f;

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            created.Clear();
        }

        private GameObject InstantiateDoor()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPath);
            Assert.IsNotNull(prefab, DoorPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            created.Add(instance);
            return instance;
        }

        private static Bounds BoundsOf(Transform part)
        {
            Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers, part.name + " has a mesh");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        // ---- SlidingDoor's second panel ---------------------------------------------------------------------------

        [Test]
        public void MirrorPanel_SlidesTheOtherWayByTheSameDistance()
        {
            var root = new GameObject("Double door");
            created.Add(root);
            var door = root.AddComponent<SlidingDoor>();
            Transform right = new GameObject("Right").transform;
            Transform left = new GameObject("Left").transform;
            right.SetParent(root.transform, false);
            left.SetParent(root.transform, false);
            right.localPosition = new Vector3(0.2f, 0f, 0f);
            left.localPosition = new Vector3(-0.2f, 0f, 0f);

            var serialized = new SerializedObject(door);
            serialized.FindProperty("panel").objectReferenceValue = right;
            serialized.FindProperty("mirrorPanel").objectReferenceValue = left;
            serialized.FindProperty("slideDistance").floatValue = 0.8f;
            serialized.FindProperty("speed").floatValue = 0f; // time does pass in EditMode; with no speed the door stays where it is put
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // EditMode does not run Awake, so it is called here, and the door is put half way to its target by hand; Update then places
            // the panels for that fraction (SmoothStep(0.5) = 0.5), and a speed of 0 keeps time from moving it on.
            Call(door, "Awake");
            Field(door, "target", 1f);
            Field(door, "open", 0.5f);
            Call(door, "Update");

            Assert.AreEqual(0.2f + 0.4f, right.localPosition.x, 0.001f, "the panel goes +X");
            Assert.AreEqual(-0.2f - 0.4f, left.localPosition.x, 0.001f, "the mirror panel goes -X");
        }

        [Test]
        public void CombatLock_ClosesBothLeaves()
        {
            var root = new GameObject("Double door");
            created.Add(root);
            var door = root.AddComponent<SlidingDoor>();
            Transform right = new GameObject("Right").transform;
            Transform left = new GameObject("Left").transform;
            right.SetParent(root.transform, false);
            left.SetParent(root.transform, false);
            var serialized = new SerializedObject(door);
            serialized.FindProperty("panel").objectReferenceValue = right;
            serialized.FindProperty("mirrorPanel").objectReferenceValue = left;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Call(door, "Awake");

            right.localPosition = Vector3.right;
            left.localPosition = Vector3.left;
            door.SetCombatLocked(true);

            Assert.AreEqual(Vector3.zero, right.localPosition);
            Assert.AreEqual(Vector3.zero, left.localPosition);
        }

        [Test]
        public void WithoutAMirrorPanel_TheDoorStillWorksAsBefore()
        {
            var root = new GameObject("Single door");
            created.Add(root);
            var door = root.AddComponent<SlidingDoor>();
            Transform panel = new GameObject("Panel").transform;
            panel.SetParent(root.transform, false);
            var serialized = new SerializedObject(door);
            serialized.FindProperty("panel").objectReferenceValue = panel;
            serialized.FindProperty("slideDistance").floatValue = 1f;
            serialized.FindProperty("speed").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Call(door, "Awake");
            Field(door, "target", 1f);
            Field(door, "open", 0.5f);

            Call(door, "Update");

            Assert.AreEqual(0.5f, panel.localPosition.x, 0.001f);
        }

        private static void Call(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static void Field(object target, string name, float value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        // ---- Door.prefab ------------------------------------------------------------------------------------------

        [Test]
        public void Prefab_HasAFrameAndTwoLeavesWiredToTheSlidingDoor()
        {
            GameObject door = InstantiateDoor();
            Transform model = door.transform.Find("Model");
            Assert.IsNotNull(model, "the cyberpunk model");
            Assert.IsNull(door.transform.Find("Panel"), "the grey slab is gone");
            Transform frame = model.Find("DoorFrame");
            Transform leafLeft = model.Find("DoorLeaf_L");
            Transform leafRight = model.Find("DoorLeaf_R");
            Assert.IsNotNull(frame);
            Assert.IsNotNull(leafLeft);
            Assert.IsNotNull(leafRight);

            var sliding = door.GetComponent<SlidingDoor>();
            var serialized = new SerializedObject(sliding);
            Assert.AreSame(leafRight, serialized.FindProperty("panel").objectReferenceValue);
            Assert.AreSame(leafLeft, serialized.FindProperty("mirrorPanel").objectReferenceValue);
            Assert.AreEqual(GapWidth * 0.5f + 0.05f, serialized.FindProperty("slideDistance").floatValue, 0.001f);
        }

        [Test]
        public void Prefab_LeavesExactlyFillTheDoorway()
        {
            GameObject door = InstantiateDoor();
            Bounds left = BoundsOf(door.transform.Find("Model/DoorLeaf_L"));
            Bounds right = BoundsOf(door.transform.Find("Model/DoorLeaf_R"));

            Assert.AreEqual(GapWidth * 0.5f, left.size.x, Tolerance);
            Assert.AreEqual(GapWidth * 0.5f, right.size.x, Tolerance);
            Assert.AreEqual(-GapWidth * 0.5f, left.min.x, Tolerance, "the left leaf starts at the left jamb");
            Assert.AreEqual(GapWidth * 0.5f, right.max.x, Tolerance, "the right leaf ends at the right jamb");
            Assert.AreEqual(0f, left.max.x, Tolerance, "the leaves meet in the middle");
            Assert.AreEqual(0f, right.min.x, Tolerance);
            Assert.AreEqual(0f, left.min.y, Tolerance, "the leaves stand on the sill");
            Assert.AreEqual(GapHeight, left.max.y, Tolerance);
            Assert.AreEqual(GapHeight, right.max.y, Tolerance);
        }

        [Test]
        public void Prefab_FrameSurroundsTheDoorwayAndSitsProudOfTheWall()
        {
            GameObject door = InstantiateDoor();
            Bounds frame = BoundsOf(door.transform.Find("Model/DoorFrame"));

            Assert.Less(frame.min.x, -GapWidth * 0.5f, "the frame is wider than the gap");
            Assert.Greater(frame.max.x, GapWidth * 0.5f);
            Assert.GreaterOrEqual(frame.max.y, GapHeight, "and as tall");
            Assert.Greater(frame.size.z, WallThickness, "it stands out of both faces of the 0.4 m wall");
            Assert.Less(frame.min.z, -WallThickness * 0.5f);
            Assert.Greater(frame.max.z, WallThickness * 0.5f);
        }

        [Test]
        public void Prefab_LeavesClearTheGapWhenOpen()
        {
            GameObject door = InstantiateDoor();
            float slide = new SerializedObject(door.GetComponent<SlidingDoor>()).FindProperty("slideDistance").floatValue;
            Bounds right = BoundsOf(door.transform.Find("Model/DoorLeaf_R"));
            Bounds left = BoundsOf(door.transform.Find("Model/DoorLeaf_L"));

            Assert.GreaterOrEqual(right.min.x + slide, GapWidth * 0.5f - 0.001f, "the right leaf's edge is past the right jamb");
            Assert.LessOrEqual(left.max.x - slide, -GapWidth * 0.5f + 0.001f, "the left leaf's edge is past the left jamb");
        }

        [Test]
        public void Prefab_BlockerIsAThinKinematicSolidOnTheInteractableLayer()
        {
            GameObject door = InstantiateDoor();
            Transform blocker = door.transform.Find("Blocker");
            Assert.IsNotNull(blocker);
            var box = blocker.GetComponent<BoxCollider>();

            Assert.IsFalse(box.isTrigger);
            Assert.GreaterOrEqual(box.size.x, GapWidth);
            Assert.GreaterOrEqual(box.size.y, GapHeight);
            Assert.Less(box.size.z, WallThickness);
            Assert.AreEqual(PhysicsLayers.Interactable, blocker.gameObject.layer);
            Assert.IsTrue(blocker.GetComponent<Rigidbody>().isKinematic);
            Assert.AreSame(box, new SerializedObject(door.GetComponent<SlidingDoor>()).FindProperty("blocker").objectReferenceValue);
        }

        [Test]
        public void Prefab_LeavesAndFrameHaveNoColliders()
        {
            GameObject door = InstantiateDoor();

            Assert.IsEmpty(door.transform.Find("Model").GetComponentsInChildren<Collider>(), "only the Blocker blocks");
        }

        [Test]
        public void Prefab_EveryMeshUsesTheCyberDoorMaterialWithAllFourMaps()
        {
            GameObject door = InstantiateDoor();
            foreach (Renderer renderer in door.transform.Find("Model").GetComponentsInChildren<Renderer>())
            {
                Material material = renderer.sharedMaterial;
                Assert.AreEqual("M_DoorCyber", material.name, renderer.name);
                Assert.IsNotNull(material.GetTexture("_BaseMap"), "base colour");
                Assert.IsNotNull(material.GetTexture("_BumpMap"), "normal");
                Assert.IsNotNull(material.GetTexture("_MetallicGlossMap"), "metallic and smoothness");
                Assert.IsNotNull(material.GetTexture("_OcclusionMap"), "occlusion");
                Assert.IsNotNull(material.GetTexture("_EmissionMap"), "emission");
            }
        }

        [Test]
        public void Textures_AreImportedAsTheirMapsNeed()
        {
            var normal = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/Models/T_DoorCyber_Normal.png");
            var metal = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/Models/T_DoorCyber_MetalSmooth.png");
            var colour = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/Models/T_DoorCyber_BaseColor.png");

            Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
            Assert.IsFalse(metal.sRGBTexture, "the metal map is data");
            Assert.IsTrue(colour.sRGBTexture);
        }

        // ---- the four doorways --------------------------------------------------------------------------------------

        [Test]
        public void TheFourDoorwaysAreAllInstancesOfThisPrefab()
        {
            string guid = AssetDatabase.AssetPathToGUID(DoorPath);
            string scene = File.ReadAllText(ScenePath);
            int instances = Regex.Matches(scene, "m_SourcePrefab: \\{fileID: 100100000, guid: " + guid).Count;

            Assert.AreEqual(4, instances, "D-Bunker, D-Security, D-Generator and D-Lab");
            foreach (string id in new[] { "Door_D-Bunker", "Door_D-Security", "Door_D-Generator", "Door_D-Lab" })
            {
                StringAssert.Contains("value: " + id, scene);
            }
        }
    }
}
