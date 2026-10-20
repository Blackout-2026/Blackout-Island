using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    /// <summary>The choice of tree variant by position (step C11): fixed for a place, and following the weights.</summary>
    public class TreeDiversifierTests
    {
        [Test]
        public void SamePlace_AlwaysGetsTheSameVariant()
        {
            var position = new Vector3(0.4137f, 0.2f, 0.8021f);
            int first = TreeDiversifier.ChooseVariant(position, TreeDiversifier.PineMix.Weights, 5);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(first, TreeDiversifier.ChooseVariant(position, TreeDiversifier.PineMix.Weights, 5));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TheMix_FollowsTheWeights(int familyIndex)
        {
            TreeDiversifier.Mix mix = familyIndex == 0 ? TreeDiversifier.PineMix : familyIndex == 1 ? TreeDiversifier.PalmMix : TreeDiversifier.BeachMix;
            var counts = new int[mix.Weights.Length];
            var random = new System.Random(12345);
            const int trees = 6000;
            for (int i = 0; i < trees; i++)
            {
                var position = new Vector3((float)random.NextDouble(), 0f, (float)random.NextDouble());
                counts[TreeDiversifier.ChooseVariant(position, mix.Weights, 3)]++;
            }

            float total = 0f;
            foreach (float weight in mix.Weights)
            {
                total += weight;
            }

            for (int v = 0; v < counts.Length; v++)
            {
                Assert.That(counts[v] / (float)trees, Is.EqualTo(mix.Weights[v] / total).Within(0.04f), mix.Names[v]);
            }
        }

        [Test]
        public void EveryVariant_HasAPrefabEntry()
        {
            foreach (TreeDiversifier.Mix mix in new[] { TreeDiversifier.PineMix, TreeDiversifier.PalmMix, TreeDiversifier.BeachMix })
            {
                Assert.AreEqual(mix.Names.Length, mix.Weights.Length);
                foreach (string name in mix.Names)
                {
                    Assert.IsTrue(System.Array.Exists(TreePrefabBuilder.Trees, t => t.Name == name), name);
                }
            }
        }

        [Test]
        public void TheBeachShare_IsAboutTheConstant()
        {
            var random = new System.Random(777);
            const int trees = 6000;
            int other = 0;
            for (int i = 0; i < trees; i++)
            {
                var position = new Vector3((float)random.NextDouble(), 0f, (float)random.NextDouble());
                float chance = TreeDiversifier.Chance(position, 11);
                Assert.That(chance, Is.InRange(0f, 0.9999999f));
                if (chance < TreeDiversifier.BeachShare)
                {
                    other++;
                }
            }

            Assert.That(other / (float)trees, Is.EqualTo(TreeDiversifier.BeachShare).Within(0.04f));
        }
    }
}
