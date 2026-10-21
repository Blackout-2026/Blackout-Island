using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor.Tests
{
    /// <summary>Step F1: the forest's rock, bush and grass prefabs and the swap of the terrain's prototypes. Skipped until the prefabs exist.</summary>
    public class ForestModelSwapTests
    {
        private static GameObject LoadPrefab(VegetationPrototype prototype)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VegetationLibrary.PrefabPath(prototype));
            if (prefab == null)
            {
                Assert.Ignore($"{VegetationLibrary.PrefabPath(prototype)} does not exist yet: run Blackout > World > Apply Forest Models.");
            }

            return prefab;
        }

        private static int Triangles(Mesh mesh)
        {
            return mesh.triangles.Length / 3;
        }

        [TestCase(VegetationPrototype.Rock, true)]
        [TestCase(VegetationPrototype.RockSmall, false)]
        public void RockPrefabs_HaveThreeFallingLodsAndThePlaceholdersCapsule(VegetationPrototype prototype, bool collider)
        {
            GameObject prefab = LoadPrefab(prototype);
            LOD[] lods = prefab.GetComponent<LODGroup>().GetLODs();
            Assert.AreEqual(3, lods.Length);

            int t0 = Triangles(lods[0].renderers[0].GetComponent<MeshFilter>().sharedMesh);
            int t1 = Triangles(lods[1].renderers[0].GetComponent<MeshFilter>().sharedMesh);
            int t2 = Triangles(lods[2].renderers[0].GetComponent<MeshFilter>().sharedMesh);
            Assert.That(t0, Is.GreaterThan(t1));
            Assert.That(t1, Is.GreaterThan(t2));
            Assert.That(t0, Is.LessThanOrEqualTo(1200), "the budget of docs/level-layout.md, LOD0");
            Assert.That(t1, Is.LessThanOrEqualTo(400));
            Assert.That(t2, Is.LessThanOrEqualTo(120));

            var capsule = prefab.GetComponent<CapsuleCollider>();
            Assert.AreEqual(collider, capsule != null);
            if (collider)
            {
                // the NavMesh cuts these capsules out: the placeholder's numbers keep it valid without a rebake
                Assert.That(capsule.radius, Is.EqualTo(0.8f));
                Assert.That(capsule.height, Is.EqualTo(1.6f));
                Assert.That(capsule.center.y, Is.EqualTo(-0.2f));
            }
        }

        [TestCase(VegetationPrototype.Bush)]
        [TestCase(VegetationPrototype.Grass)]
        public void DetailPrefabs_AreOneInstancedMeshWithoutACollider(VegetationPrototype prototype)
        {
            GameObject prefab = LoadPrefab(prototype);
            Assert.IsNotNull(prefab.GetComponent<MeshFilter>().sharedMesh);
            Material[] materials = prefab.GetComponent<MeshRenderer>().sharedMaterials;
            Assert.AreEqual(1, materials.Length);
            Assert.IsTrue(materials[0].enableInstancing, "terrain details are drawn instanced");
            Assert.IsNull(prefab.GetComponent<Collider>());
            Assert.IsNull(prefab.GetComponent<LODGroup>());
        }

        [Test]
        public void SwapPrototypes_ReplacesThePlaceholdersAndKeepsEveryInstance()
        {
            LoadPrefab(VegetationPrototype.Rock);
            LoadPrefab(VegetationPrototype.RockSmall);
            LoadPrefab(VegetationPrototype.Bush);
            LoadPrefab(VegetationPrototype.Grass);

            var data = new TerrainData();
            try
            {
                data.SetDetailResolution(32, 8);
                data.treePrototypes = new[]
                {
                    new TreePrototype { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VegetationLibrary.PlaceholderPath(VegetationPrototype.Rock)) },
                    new TreePrototype { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VegetationLibrary.PlaceholderPath(VegetationPrototype.RockSmall)) },
                    new TreePrototype { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VegetationLibrary.PrefabPath(VegetationPrototype.Pine)) },
                };
                data.detailPrototypes = new[]
                {
                    new DetailPrototype { prototype = AssetDatabase.LoadAssetAtPath<GameObject>(VegetationLibrary.PlaceholderPath(VegetationPrototype.Bush)), usePrototypeMesh = true },
                    new DetailPrototype { prototype = AssetDatabase.LoadAssetAtPath<GameObject>(VegetationLibrary.PlaceholderPath(VegetationPrototype.Grass)), usePrototypeMesh = true },
                };
                var trees = new[]
                {
                    new TreeInstance { prototypeIndex = 0, position = new Vector3(0.1f, 0.2f, 0.3f), widthScale = 2f, heightScale = 3f },
                    new TreeInstance { prototypeIndex = 1, position = new Vector3(0.4f, 0.5f, 0.6f), widthScale = 1f, heightScale = 1f },
                    new TreeInstance { prototypeIndex = 2, position = new Vector3(0.7f, 0.8f, 0.9f), widthScale = 1.1f, heightScale = 1.2f },
                };
                data.SetTreeInstances(trees, false);
                var bushes = new int[32, 32];
                bushes[3, 4] = 2;
                data.SetDetailLayer(0, 0, 0, bushes);

                string first = ForestModelSwap.SwapPrototypes(data);

                Assert.That(first, Does.Contain("prototypes swapped: 4"));
                Assert.AreEqual("Rock", data.treePrototypes[0].prefab.name);
                Assert.AreEqual("RockSmall", data.treePrototypes[1].prefab.name);
                Assert.AreEqual("Pine", data.treePrototypes[2].prefab.name, "pines are not touched");
                Assert.AreEqual("Bush", data.detailPrototypes[0].prototype.name);
                Assert.AreEqual("Grass", data.detailPrototypes[1].prototype.name);
                Assert.AreEqual(3, data.treeInstances.Length);
                for (int i = 0; i < trees.Length; i++)
                {
                    Assert.AreEqual(trees[i].position, data.treeInstances[i].position);
                    Assert.AreEqual(trees[i].prototypeIndex, data.treeInstances[i].prototypeIndex);
                    Assert.AreEqual(trees[i].heightScale, data.treeInstances[i].heightScale);
                }

                Assert.AreEqual(2, data.GetDetailLayer(0, 0, 32, 32, 0)[3, 4], "the bushes stay where they were");
                Assert.That(ForestModelSwap.SwapPrototypes(data), Does.Contain("prototypes swapped: 0"), "a second run changes nothing");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }
    }
}
