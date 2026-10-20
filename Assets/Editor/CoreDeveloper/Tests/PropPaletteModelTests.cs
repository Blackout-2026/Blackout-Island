using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class PropPaletteModelTests
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

        private static PropEntry TestEntry(string name, PropSource source, Vector3 size)
        {
            return new PropEntry(name, TestAssetFolders.Prefabs, source, PropGroup.Storage, PropPivot.BaseCentre, size, 4, string.Empty);
        }

        [Test]
        public void AllEntries_ListsEveryPropAndEverySignOnceEach()
        {
            List<PropEntry> entries = PropPaletteModel.AllEntries();

            Assert.AreEqual(PropCatalog.All.Count + DecalCatalog.All.Count, entries.Count);
            var paths = new HashSet<string>();
            foreach (PropEntry entry in entries)
            {
                Assert.IsTrue(paths.Add(entry.PrefabPath), entry.PrefabPath);
            }
        }

        [Test]
        public void ARowWithoutAPrefab_IsMissingAndHasNoNumbers()
        {
            PropPaletteRow row = PropPaletteModel.Describe(TestEntry("Prop_TestNothing", PropSource.Cc0Kit, Vector3.one));

            Assert.IsFalse(row.Exists);
            Assert.AreEqual(PropStatus.Missing, row.Status);
            Assert.AreEqual(BudgetState.None, row.TriangleState);
            Assert.AreEqual(0, row.LevelTriangles);
        }

        [Test]
        public void ABuiltPlaceholder_ShowsItsMeasuredTrianglesCollidersAndSize()
        {
            PropEntry entry = TestEntry("Prop_TestBench", PropSource.Blender, new Vector3(3.5f, 0.93f, 1f));
            ModelPrefabBuilder.Build(entry, PropDefinitions.For("Prop_LabBench"), materials, true);

            PropPaletteRow row = PropPaletteModel.Describe(entry);

            Assert.IsTrue(row.Exists);
            Assert.AreEqual(PropStatus.Placeholder, row.Status);
            Assert.AreEqual(24, row.Triangles);
            Assert.AreEqual(2, row.Materials);
            Assert.AreEqual(1, row.Colliders);
            Assert.AreEqual(3.5f, row.MeasuredSize.x, 0.01f);
            Assert.AreEqual(96, row.LevelTriangles); // 24 triangles x 4 copies
        }

        [Test]
        public void AHandAddedKitPrefab_IsListedAsKitWithItsTriangles()
        {
            PropEntry entry = TestEntry("Prop_TestPipe", PropSource.Cc0Kit, new Vector3(0.2f, 0.2f, 2f));
            ModelMaterials.EnsureFolder(TestAssetFolders.Prefabs);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            PrefabUtility.SaveAsPrefabAsset(go, entry.PrefabPath);
            Object.DestroyImmediate(go);

            PropPaletteRow row = PropPaletteModel.Describe(entry);

            Assert.AreEqual(PropStatus.Kit, row.Status);
            Assert.AreEqual(80, row.Triangles);
        }

        [TestCase(1200, BudgetState.Ok)]
        [TestCase(1201, BudgetState.OverTarget)]
        [TestCase(1800, BudgetState.OverTarget)]
        [TestCase(1801, BudgetState.OverMax)]
        public void TheBudgetState_FollowsTargetAndHardMaximum(int triangles, BudgetState expected)
        {
            var instance = new GameObject("Stand-in");
            try
            {
                // 0.4 m is a small prop: target 1,200, hard maximum 1,800.
                var row = new PropPaletteRow
                {
                    Entry = TestEntry("Prop_TestSmall", PropSource.Blender, new Vector3(0.4f, 0.4f, 0.4f)),
                    Prefab = instance,
                    Triangles = triangles,
                };

                Assert.AreEqual(expected, row.TriangleState);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void MoreThanTheAllowedMaterials_IsFlagged()
        {
            var instance = new GameObject("Stand-in");
            try
            {
                var row = new PropPaletteRow
                {
                    Entry = TestEntry("Prop_TestSmall", PropSource.Blender, new Vector3(0.4f, 0.4f, 0.4f)),
                    Prefab = instance,
                    Materials = 5,
                };

                Assert.IsTrue(row.MaterialsOverLimit); // the limit is 4
                row.Materials = 4;
                Assert.IsFalse(row.MaterialsOverLimit);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
