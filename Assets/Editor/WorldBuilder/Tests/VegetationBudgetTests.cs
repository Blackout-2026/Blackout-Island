using System.Collections.Generic;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class VegetationBudgetTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";

        private static readonly float[] TreeLods = { 0.25f, 0.10f, 0.02f };

        [Test]
        public void RelativeHeight_IsTheInverseOfLodDistance()
        {
            float distance = VegetationLibrary.LodDistance(10.6f, 0.25f);
            Assert.That(VegetationBudget.RelativeHeight(10.6f, distance, 1f), Is.EqualTo(0.25f).Within(1e-4f));
        }

        [Test]
        public void LodBias_MakesObjectsLookBigger()
        {
            // PC quality uses LOD bias 2: the switch happens twice as far away.
            float distance = VegetationLibrary.LodDistance(10.6f, 0.25f, 2f);
            Assert.That(distance, Is.EqualTo(2f * VegetationLibrary.LodDistance(10.6f, 0.25f)).Within(1e-3f));
            Assert.That(VegetationBudget.RelativeHeight(10.6f, distance, 2f), Is.EqualTo(0.25f).Within(1e-4f));
        }

        [TestCase(0.3f, 0)]
        [TestCase(0.25f, 0)]
        [TestCase(0.2f, 1)]
        [TestCase(0.05f, 2)]
        [TestCase(0.01f, -1)]
        public void SelectLod_PicksTheFirstLodTheObjectStillFills(float relativeHeight, int expected)
        {
            Assert.That(VegetationBudget.SelectLod(relativeHeight, TreeLods), Is.EqualTo(expected));
        }

        [Test]
        public void Frustum_SeesAheadButNotBehind()
        {
            Plane[] north = VegetationBudget.Frustum(new Viewpoint("test", new Vector3(0f, 2f, 0f), 0f));
            Assert.That(GeometryUtility.TestPlanesAABB(north, new Bounds(new Vector3(0f, 2f, 10f), Vector3.one)), Is.True);
            Assert.That(GeometryUtility.TestPlanesAABB(north, new Bounds(new Vector3(0f, 2f, -10f), Vector3.one)), Is.False);

            Plane[] east = VegetationBudget.Frustum(new Viewpoint("test", new Vector3(0f, 2f, 0f), 90f));
            Assert.That(GeometryUtility.TestPlanesAABB(east, new Bounds(new Vector3(10f, 2f, 0f), Vector3.one)), Is.True);
            Assert.That(GeometryUtility.TestPlanesAABB(east, new Bounds(new Vector3(0f, 2f, 10f), Vector3.one)), Is.False);
        }

        [Test]
        public void Viewpoints_AreFivePlayerEyesOnTheRoute()
        {
            var layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);
            List<Viewpoint> views = VegetationBudget.Viewpoints(MarkerLayout.Build(layout));
            Assert.That(views.Count, Is.EqualTo(5));

            // The dock start: deck 1.3 m + eye 1.65 m, facing north.
            Assert.That(views[0].Eye, Is.EqualTo(new Vector3(0f, 1.3f + VegetationBudget.EyeHeight, -121f)));
            Assert.That(views[0].Yaw, Is.EqualTo(0f));

            // The gate looks back south over the forest.
            Assert.That(views[3].Yaw, Is.EqualTo(180f));
            foreach (Viewpoint view in views)
            {
                Assert.That(view.Eye.y, Is.GreaterThan(IslandHeightField.Sample(view.Eye.x, view.Eye.z)), view.Name);
            }
        }
    }
}
