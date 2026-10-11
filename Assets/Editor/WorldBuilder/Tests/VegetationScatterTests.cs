using System.Collections.Generic;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class VegetationScatterTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";

        // Counts from running the prototype's own Forest.place() in Node, with its ground height and trail distance
        // computed exactly, as the Unity height field does. The reference's 465 pines and 65 palms come from the
        // prototype sampling both from a 2 m grid; bushes, grass and rocks are the same either way.
        private const int PrototypePines = 461;
        private const int PrototypePalms = 69;
        private const int PrototypeBushes = 420;
        private const int PrototypeGrass = 6000;
        private const int PrototypeRocks = 170;
        private const int PrototypeRocksWithColliders = 116;

        private List<Vector2> clearings;
        private VegetationSet set;

        [OneTimeSetUp]
        public void Scatter()
        {
            var layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);
            Assert.That(layout, Is.Not.Null, LayoutPath);
            clearings = VegetationScatter.HunterClearings(MarkerLayout.Build(layout));
            set = VegetationScatter.Scatter(clearings);
        }

        [Test]
        public void Mulberry32_GivesThePrototypesSequence()
        {
            // U.rng(1234) in prototype/src/core/namespace.js, first three values.
            var random = new Mulberry32(1234);
            Assert.That(random.Next(), Is.EqualTo(0.073294978123).Within(1e-12));
            Assert.That(random.Next(), Is.EqualTo(0.703411989845).Within(1e-12));
            Assert.That(random.Next(), Is.EqualTo(0.902856019093).Within(1e-12));
        }

        [Test]
        public void Fbm_MatchesThePrototype()
        {
            // U.fbm2(1.5, -2.25, 2, 77): the noise behind the forest clearings.
            Assert.That(IslandHeightField.Fbm(1.5, -2.25, 2, 77), Is.EqualTo(0.586066767404).Within(1e-12));
        }

        [Test]
        public void FirstTree_IsThePrototypesFirstTree()
        {
            // Prototype: x -107.0813, z -58.6696 (Unity z 58.6696), s 1.2227, rot 3.2101, pine.
            VegetationInstance first = set.Trees[0];
            Assert.That(first.Kind, Is.EqualTo(VegetationKind.Pine));
            Assert.That(first.X, Is.EqualTo(-107.0813f).Within(1e-3f));
            Assert.That(first.Z, Is.EqualTo(58.6696f).Within(1e-3f));
            Assert.That(first.Scale, Is.EqualTo(1.2227f).Within(1e-3f));
            Assert.That(first.Yaw, Is.EqualTo(-3.2101f).Within(1e-3f));
        }

        [TestCase(VegetationKind.Pine, PrototypePines)]
        [TestCase(VegetationKind.Palm, PrototypePalms)]
        [TestCase(VegetationKind.Bush, PrototypeBushes)]
        [TestCase(VegetationKind.Grass, PrototypeGrass)]
        [TestCase(VegetationKind.Rock, PrototypeRocks)]
        public void CountBeforeClearings_MatchesThePrototype(VegetationKind kind, int prototype)
        {
            Assert.That(set.Count(kind) + set.Cleared[(int)kind], Is.EqualTo(prototype));
        }

        [Test]
        public void RocksWithColliders_MatchThePrototype()
        {
            int big = 0;
            foreach (VegetationInstance rock in set.Rocks)
            {
                if (rock.Scale > VegetationLayout.RockColliderMinScale)
                {
                    big++;
                }
            }

            // Cleared rocks are counted too, unless they were small.
            Assert.That(big, Is.LessThanOrEqualTo(PrototypeRocksWithColliders));
            Assert.That(big + set.Cleared[(int)VegetationKind.Rock], Is.GreaterThanOrEqualTo(PrototypeRocksWithColliders));
        }

        [Test]
        public void HunterClearings_AreTheTwelveRoutePoints()
        {
            // Hunter-1 and Hunter-2 have 6 waypoints each; each spawn is on its first waypoint.
            Assert.That(clearings.Count, Is.EqualTo(12));
            Assert.That(clearings, Does.Contain(new Vector2(-24f, -54f)));
            Assert.That(clearings, Does.Contain(new Vector2(36f, -30f)));
        }

        [Test]
        public void Clearings_HaveNoTreeRockOrBush()
        {
            foreach (List<VegetationInstance> list in new[] { set.Trees, set.Rocks, set.Bushes })
            {
                foreach (VegetationInstance instance in list)
                {
                    Assert.That(VegetationScatter.InClearing(instance.X, instance.Z, clearings), Is.False,
                        $"{instance.Kind} at ({instance.X:0.0}, {instance.Z:0.0})");
                }
            }

            Assert.That(set.Cleared[(int)VegetationKind.Pine] + set.Cleared[(int)VegetationKind.Palm], Is.GreaterThan(0));
        }

        [TestCase(VegetationKind.Pine, 3.4f)]
        [TestCase(VegetationKind.Palm, 3.4f)]
        [TestCase(VegetationKind.Bush, 3.8f)]
        [TestCase(VegetationKind.Rock, 3f)]
        [TestCase(VegetationKind.Grass, 1.4f)]
        public void Trail_StaysClear(VegetationKind kind, float clearance)
        {
            foreach (VegetationInstance instance in All(kind))
            {
                Assert.That(IslandHeightField.TrailDistance(instance.X, instance.Z), Is.GreaterThanOrEqualTo(clearance - 1e-3f),
                    $"{kind} at ({instance.X:0.0}, {instance.Z:0.0})");
            }
        }

        [Test]
        public void CompoundCliffAndDockApproach_HaveNoPlants()
        {
            foreach (VegetationInstance instance in Plants())
            {
                Assert.That(VegetationScatter.Blocked(instance.X, instance.Z), Is.False, $"{instance.Kind} at ({instance.X:0.0}, {instance.Z:0.0})");
            }

            foreach (VegetationInstance rock in set.Rocks)
            {
                bool inCompound = rock.X > -24f && rock.X < 24f && rock.Z > 26f && rock.Z < 63f;
                Assert.That(inCompound, Is.False, $"rock at ({rock.X:0.0}, {rock.Z:0.0})");
            }
        }

        [Test]
        public void Trees_StandOnDryGentleGround()
        {
            foreach (VegetationInstance tree in set.Trees)
            {
                Assert.That(IslandHeightField.Sample(tree.X, tree.Z), Is.GreaterThanOrEqualTo(1f));
                Assert.That(VegetationScatter.GroundNormalY(tree.X, tree.Z), Is.GreaterThanOrEqualTo(0.8));
                Assert.That(tree.Scale, Is.InRange(0.78f, 1.28f));
            }
        }

        [Test]
        public void Scatter_IsDeterministic()
        {
            VegetationSet again = VegetationScatter.Scatter(clearings);
            Assert.That(again.Trees, Is.EqualTo(set.Trees));
            Assert.That(again.Rocks, Is.EqualTo(set.Rocks));
            Assert.That(again.Bushes, Is.EqualTo(set.Bushes));
            Assert.That(again.Grass, Is.EqualTo(set.Grass));
        }

        private IEnumerable<VegetationInstance> All(VegetationKind kind)
        {
            foreach (VegetationInstance instance in Plants())
            {
                if (instance.Kind == kind)
                {
                    yield return instance;
                }
            }

            foreach (VegetationInstance rock in set.Rocks)
            {
                if (kind == VegetationKind.Rock)
                {
                    yield return rock;
                }
            }
        }

        private IEnumerable<VegetationInstance> Plants()
        {
            foreach (List<VegetationInstance> list in new[] { set.Trees, set.Bushes, set.Grass })
            {
                foreach (VegetationInstance instance in list)
                {
                    yield return instance;
                }
            }
        }
    }
}
