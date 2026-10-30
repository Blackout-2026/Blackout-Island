using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class ForestDensityTests
    {
        [Test]
        public void Supplement_IsRepeatableVariedAndKeepsGameplayClearances()
        {
            var layout = AssetDatabase.LoadAssetAtPath<Blackout.World.BunkerLayout>(BunkerLayoutAssetBuilder.AssetPath);
            List<Vector2> clearings = VegetationScatter.HunterClearings(MarkerLayout.Build(layout));
            VegetationSet first = ForestDensityLayout.Generate(clearings);
            VegetationSet second = ForestDensityLayout.Generate(clearings);
            foreach (var lists in new[] { (first.Trees, second.Trees), (first.Rocks, second.Rocks), (first.Bushes, second.Bushes) })
            {
                Assert.That(lists.Item1.Count, Is.GreaterThan(200));
                CollectionAssert.AreEqual(lists.Item1, lists.Item2);
                foreach (VegetationInstance instance in lists.Item1)
                {
                    Assert.That(VegetationScatter.InClearing(instance.X, instance.Z, clearings), Is.False);
                    Assert.That(VegetationScatter.Blocked(instance.X, instance.Z), Is.False);
                    Assert.That(IslandHeightField.TrailDistance(instance.X, instance.Z), Is.GreaterThanOrEqualTo(3.8));
                }
            }
            Assert.That(first.Trees.Select(p => p.Variant).Distinct().Count(), Is.EqualTo(7));
            Assert.That(first.Rocks.Select(p => p.Variant).Distinct().Count(), Is.EqualTo(2));
            Assert.That(first.Bushes.Select(p => p.Variant).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void Append_PreservesExistingInstancesRejectsOverlapAndDoesNotAccumulate()
        {
            var data = new TerrainData { heightmapResolution = 33, size = IslandLayout.TerrainSize };
            try
            {
                var authored = new TreeInstance
                {
                    prototypeIndex = 0, position = new Vector3(0.5f, 0.2f, 0.45f),
                    rotation = 1.23f, widthScale = 1.2f, heightScale = 1.3f, color = Color.gray,
                };
                var trees = new List<TreeInstance> { authored };
                var plan = new List<VegetationInstance>
                {
                    new VegetationInstance(VegetationKind.Pine, 0, 0, 1, 0, .1, 0),
                    new VegetationInstance(VegetationKind.Pine, 10, 0, 1, 0, .1, 0),
                };
                Assert.That(ForestDensityBuilder.Append(data, trees, plan, new[] { 0 }, 2.6f), Is.EqualTo(1));
                Assert.That(trees[0], Is.EqualTo(authored));
                Assert.That(ForestDensityBuilder.Append(data, trees, plan, new[] { 0 }, 2.6f), Is.Zero);
                Assert.That(trees.Count, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(data); }
        }

        [Test]
        public void AddedPrefabs_HaveCompleteLodsAndValidInstancedDetails()
        {
            foreach (string name in new[] { "SmallTreeA", "SmallTreeB", "SmallTreeC", "RockWide", "RockRound" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environment/Vegetation/" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null, name);
                Assert.That(prefab.GetComponentsInChildren<LODGroup>(true).Length, Is.EqualTo(1), name);
                LOD[] levels = prefab.GetComponent<LODGroup>().GetLODs();
                Assert.That(levels.Length, Is.EqualTo(3), name);
                for (int i = 0; i < levels.Length; i++)
                {
                    Assert.That(levels[i].renderers.Length, Is.EqualTo(1), name);
                    var renderer = (MeshRenderer)levels[i].renderers[0];
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), name);
                    if (name.StartsWith("SmallTree")) Assert.That(mesh.bounds.min.y, Is.EqualTo(0f).Within(0.02f), name);
                    Assert.That(mesh.triangles.Length / 3, Is.LessThanOrEqualTo(new[] { 2600, 750, 250 }[i]), name);
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, name);
                        Assert.That(material.enableInstancing, Is.True, name);
                        Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, name);
                    }
                }
                Assert.That(prefab.GetComponent<CapsuleCollider>(), Is.Not.Null, name);
            }
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(ForestModelSwap.TerrainDataPath);
            var leaves = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/Models/T_SmallTree_Leaves_BaseColor.png");
            Assert.That(leaves.DoesSourceTextureHaveAlpha(), Is.True, "Leaf cards require the supplied opacity mask.");
            foreach (string name in ForestDensityBuilder.BushNames)
            {
                DetailPrototype detail = data.detailPrototypes.Single(p => p.prototype.name == name);
                Assert.That(detail.Validate(out string error), Is.True, error);
                Assert.That(detail.useInstancing, Is.True);
            }
        }
    }
}
