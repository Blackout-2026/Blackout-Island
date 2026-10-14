using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class ModelPrefabBuilderTests
    {
        private PartMaterials materials;

        [SetUp]
        public void SetUp()
        {
            TestAssetFolders.Create();
            materials = PropMaterialBuilder.BuildAll(TestAssetFolders.Materials, TestAssetFolders.Textures);
        }

        [TearDown]
        public void TearDown()
        {
            TestAssetFolders.Delete();
        }

        private static PropEntry TestEntry(string name, PropPivot pivot, Vector3 size)
        {
            return new PropEntry(name, TestAssetFolders.Prefabs, PropSource.Blender, PropGroup.Storage, pivot, size, 1, string.Empty);
        }

        private GameObject Build(PropEntry entry, ModelDefinition definition, bool colliders = true, bool rebuild = false)
        {
            ModelPrefabBuilder.Build(entry, definition, materials, colliders, null, rebuild);
            return AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath);
        }

        [Test]
        public void Build_CreatesAPrefabWithAModelChildCollidersAndSockets()
        {
            PropEntry entry = TestEntry("Prop_TestChamber", PropPivot.BaseCentre, new Vector3(3.8f, 2.88f, 3.8f));

            GameObject prefab = Build(entry, PropDefinitions.For("Prop_ContainmentChamber"));

            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.transform.Find("Model"));
            Assert.AreEqual(18, PropStats.Colliders(prefab)); // floor, pedestal and 16 glass panels
            Transform anchor = prefab.transform.Find(PropDefinitions.CureAnchor);
            Assert.IsNotNull(anchor);
            Assert.AreEqual(new Vector3(0f, 1.22f, 0f), anchor.localPosition);
        }

        [Test]
        public void TheSavedPrefab_KeepsItsSizePivotAndMaterials()
        {
            PropEntry entry = TestEntry("Prop_TestBench", PropPivot.BaseCentre, new Vector3(3.5f, 0.93f, 1.0f));

            GameObject prefab = Build(entry, PropDefinitions.For("Prop_LabBench"));

            Bounds bounds = PropStats.LocalBounds(prefab);
            Assert.IsNull(PivotRules.CheckSize(bounds, entry.Size));
            Assert.IsNull(PivotRules.CheckPivot(bounds, entry.Pivot));
            Assert.AreEqual(2, PropStats.Materials(prefab)); // LabWhite and DeskTop
            Assert.AreEqual(24, PropStats.Triangles(prefab)); // two boxes
        }

        [Test]
        public void VisualOnlyPrefabs_HaveNoColliders()
        {
            PropEntry entry = TestEntry("Medkit_TestVisual", PropPivot.BaseCentre, new Vector3(0.42f, 0.33f, 0.28f));

            GameObject prefab = Build(entry, PropDefinitions.For("Medkit_Visual"), colliders: false);

            Assert.AreEqual(0, PropStats.Colliders(prefab));
        }

        [Test]
        public void RunningAgain_KeepsTheModelAndDoesNotDuplicateColliders()
        {
            PropEntry entry = TestEntry("Prop_TestCrate", PropPivot.Centre, new Vector3(1.5f, 1.5f, 1.5f));
            ModelDefinition crate = PropDefinitions.For("Prop_Crate");
            GameObject first = Build(entry, crate);
            string firstYaml = System.IO.File.ReadAllText(System.IO.Path.GetFullPath(entry.PrefabPath));

            GameObject second = Build(entry, crate);

            Assert.AreEqual(1, PropStats.Colliders(second));
            Assert.AreEqual(1, second.transform.childCount);
            Assert.AreEqual(firstYaml, System.IO.File.ReadAllText(System.IO.Path.GetFullPath(entry.PrefabPath)), "a re-run must not change the file");
            Assert.IsNotNull(first);
        }

        [Test]
        public void ASocketMovedByHand_StaysWhereItIs()
        {
            PropEntry entry = TestEntry("Prop_TestChamber2", PropPivot.BaseCentre, new Vector3(3.8f, 2.88f, 3.8f));
            ModelDefinition chamber = PropDefinitions.For("Prop_ContainmentChamber");
            Build(entry, chamber);
            GameObject contents = PrefabUtility.LoadPrefabContents(entry.PrefabPath);
            contents.transform.Find(PropDefinitions.CureAnchor).localPosition = new Vector3(0f, 1.5f, 0f);
            PrefabUtility.SaveAsPrefabAsset(contents, entry.PrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);

            GameObject prefab = Build(entry, chamber, rebuild: true);

            Assert.AreEqual(new Vector3(0f, 1.5f, 0f), prefab.transform.Find(PropDefinitions.CureAnchor).localPosition);
        }

        [Test]
        public void RebuildPlaceholder_ReplacesTheModelWithTheChangedDefinition()
        {
            PropEntry entry = TestEntry("Prop_TestRebuild", PropPivot.BaseCentre, new Vector3(1f, 1f, 1f));
            Build(entry, new ModelDefinition("T").Box("Small", 0f, 0.25f, 0f, 0.5f, 0.5f, 0.5f, "Steel"), false);

            GameObject plain = Build(entry, new ModelDefinition("T").Box("Big", 0f, 0.5f, 0f, 1f, 1f, 1f, "Steel"), false);
            Assert.IsNotNull(plain.transform.Find("Model/Small"), "without the flag the placeholder is kept");

            GameObject rebuilt = Build(entry, new ModelDefinition("T").Box("Big", 0f, 0.5f, 0f, 1f, 1f, 1f, "Steel"), false, rebuild: true);
            Assert.IsNull(rebuilt.transform.Find("Model/Small"));
            Assert.IsNotNull(rebuilt.transform.Find("Model/Big"));
        }

        [Test]
        public void AssignMaterials_ReplacesSlotsByNameAndWarnsAboutAStranger()
        {
            var instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var imported = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Prop_Steel" };
                var stranger = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Lambert1" };
                instance.GetComponent<Renderer>().sharedMaterials = new[] { imported, stranger };

                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Lambert1"));
                ModelPrefabBuilder.AssignMaterials(instance, materials);

                Material[] slots = instance.GetComponent<Renderer>().sharedMaterials;
                Assert.AreEqual(materials.Get("Steel"), slots[0]);
                Assert.AreEqual(stranger, slots[1]);
                Object.DestroyImmediate(imported);
                Object.DestroyImmediate(stranger);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void TheFbxPath_FollowsTheNamingRules()
        {
            Assert.AreEqual("Assets/Models/Custom/SM_Prop_LabBench.fbx", PropCatalog.Find("Prop_LabBench").FbxPath);
            Assert.AreEqual("Assets/Models/Custom/SM_Prop_Medkit.fbx", PropCatalog.Find("Medkit_Visual").FbxPath);
            Assert.AreEqual("Assets/Models/Custom/SM_Weapon_Smg.fbx", PropCatalog.Find("Weapon_Smg_Visual").FbxPath);
        }
    }
}
