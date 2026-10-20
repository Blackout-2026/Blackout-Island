using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// Checks the real tree FBX files (step C11: SM_Tree_Pine and SM_Tree_Palm): three LOD meshes of falling triangle counts, the height of
    /// the tree, its base on the ground, and the two material slots the prefab builder expects. A file that does not exist yet is skipped.
    /// </summary>
    public class TreeFbxTests
    {
        private GameObject instance;

        private TreeEntry Load(string treeName)
        {
            TreeEntry entry = null;
            foreach (TreeEntry candidate in TreePrefabBuilder.Trees)
            {
                if (candidate.Name == treeName)
                {
                    entry = candidate;
                }
            }

            Assert.IsNotNull(entry, treeName);
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

        private MeshFilter Lod(TreeEntry entry, int index)
        {
            Transform t = RobotRig.FindDeep(instance.transform, $"{entry.Name}_LOD{index}");
            Assert.IsNotNull(t, $"{entry.Name}_LOD{index}");
            return t.GetComponent<MeshFilter>();
        }

        [TestCase("Pine", 10.2f, 11.0f)]
        [TestCase("PineB", 12.3f, 13.2f)]
        [TestCase("PineC", 8.6f, 9.3f)]
        [TestCase("PineD", 5.0f, 5.6f)]
        [TestCase("Palm", 9.2f, 9.9f)]
        [TestCase("PalmB", 10.8f, 11.5f)]
        [TestCase("PalmC", 5.9f, 6.5f)]
        [TestCase("PalmD", 9.5f, 10.2f)]
        [TestCase("Almond", 7.7f, 8.3f)]
        [TestCase("AlmondB", 6.5f, 7.2f)]
        [TestCase("Banana", 3.0f, 3.6f)]
        [TestCase("BananaB", 2.1f, 2.7f)]
        public void Lod0_HasTheHeightOfTheTreeAndStandsOnTheGround(string treeName, float minHeight, float maxHeight)
        {
            TreeEntry entry = Load(treeName);
            Bounds bounds = Lod(entry, 0).sharedMesh.bounds;

            Assert.That(bounds.max.y, Is.InRange(minHeight, maxHeight), "height");
            Assert.That(bounds.min.y, Is.InRange(-0.02f, 0.05f), "the trunk starts at the ground");
            Assert.That(Mathf.Max(bounds.extents.x, bounds.extents.z), Is.LessThan(5.5f), "crown radius");
        }

        [TestCase("Pine")]
        [TestCase("PineB")]
        [TestCase("PineC")]
        [TestCase("PineD")]
        [TestCase("Palm")]
        [TestCase("PalmB")]
        [TestCase("PalmC")]
        [TestCase("PalmD")]
        [TestCase("Almond")]
        [TestCase("AlmondB")]
        [TestCase("Banana")]
        [TestCase("BananaB")]
        public void ThreeLods_FallInTriangleCount(string treeName)
        {
            TreeEntry entry = Load(treeName);
            int t0 = MeshStats.CountTriangles(Lod(entry, 0).sharedMesh);
            int t1 = MeshStats.CountTriangles(Lod(entry, 1).sharedMesh);
            int t2 = MeshStats.CountTriangles(Lod(entry, 2).sharedMesh);

            Assert.Greater(t0, t1);
            Assert.Greater(t1, t2);
            Assert.That(t0, Is.LessThanOrEqualTo(5000), "a tree seen up close");
            Assert.That(t2, Is.LessThanOrEqualTo(300), "a tree seen from far away");
        }

        [TestCase("Pine")]
        [TestCase("PineB")]
        [TestCase("PineC")]
        [TestCase("PineD")]
        [TestCase("Palm")]
        [TestCase("PalmB")]
        [TestCase("PalmC")]
        [TestCase("PalmD")]
        [TestCase("Almond")]
        [TestCase("AlmondB")]
        [TestCase("Banana")]
        [TestCase("BananaB")]
        public void EveryLod_HasUvsAndTheBarkAndFoliageSlots(string treeName)
        {
            TreeEntry entry = Load(treeName);
            for (int i = 0; i < 3; i++)
            {
                MeshFilter filter = Lod(entry, i);
                Assert.IsTrue(filter.sharedMesh.uv.Length > 0, $"LOD{i} has UV0");
                var names = new HashSet<string>();
                foreach (Material material in filter.GetComponent<MeshRenderer>().sharedMaterials)
                {
                    names.Add(material.name);
                }

                Assert.That(names, Has.Member(entry.BarkMaterial), $"LOD{i} bark slot");
                Assert.That(names, Has.Member(entry.FoliageMaterial), $"LOD{i} foliage slot");
            }
        }
    }
}
