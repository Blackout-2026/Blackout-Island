using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace Blackout.Agents.Tests
{
    /// <summary>
    /// Runs the real PathProvider against a small NavMesh built here from two flat floors, so no baked scene is needed.
    /// Floor A spans x -10..10, floor B spans x 14..24 (a 4 m gap between them), both z -10..10.
    /// </summary>
    public class PathProviderTests
    {
        private readonly List<Mesh> meshes = new List<Mesh>();
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private PathProvider provider;

        [SetUp]
        public void SetUp()
        {
            var sources = new List<NavMeshBuildSource>
            {
                Floor(-10f, 10f),
                Floor(14f, 24f),
            };
            var bounds = new Bounds(new Vector3(7f, 0f, 0f), new Vector3(60f, 10f, 40f));
            data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources, bounds, Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
            provider = new PathProvider();
        }

        [TearDown]
        public void TearDown()
        {
            instance.Remove();
            Object.DestroyImmediate(data);
            foreach (Mesh mesh in meshes)
            {
                Object.DestroyImmediate(mesh);
            }
            meshes.Clear();
        }

        private NavMeshBuildSource Floor(float minX, float maxX)
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(minX, 0f, -10f),
                new Vector3(minX, 0f, 10f),
                new Vector3(maxX, 0f, 10f),
                new Vector3(maxX, 0f, -10f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            meshes.Add(mesh);

            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh,
                sourceObject = mesh,
                transform = Matrix4x4.identity,
                area = 0,
            };
        }

        [Test]
        public void TryCalculatePath_AcrossAFloor_ReturnsCornersFromStartToDestination()
        {
            bool found = provider.TryCalculatePath(new Vector3(-5f, 0f, -5f), new Vector3(5f, 0f, 5f), out Vector3[] corners);

            Assert.IsTrue(found);
            Assert.GreaterOrEqual(corners.Length, 2);
            Assert.Less(Vector3.Distance(corners[0], new Vector3(-5f, 0f, -5f)), 0.3f, "first corner is the start");
            Assert.Less(Vector3.Distance(corners[corners.Length - 1], new Vector3(5f, 0f, 5f)), 0.3f, "last corner is the destination");
        }

        [Test]
        public void TryCalculatePath_ToAnotherFloorWithNoLink_FindsNoPath()
        {
            bool found = provider.TryCalculatePath(new Vector3(0f, 0f, 0f), new Vector3(19f, 0f, 0f), out Vector3[] corners);

            Assert.IsFalse(found, "the two floors are not connected");
            Assert.IsNull(corners);
        }

        [Test]
        public void TryCalculatePath_ToAPointJustOffTheFloor_SnapsTheDestinationOntoIt()
        {
            bool found = provider.TryCalculatePath(new Vector3(0f, 0f, 0f), new Vector3(11.0f, 0f, 0f), out Vector3[] corners);

            Assert.IsTrue(found);
            Assert.LessOrEqual(corners[corners.Length - 1].x, 10.01f, "ends on the nearest floor point, at the edge");
        }

        [Test]
        public void TryCalculatePath_ToAPointFarFromAnyFloor_FindsNoPath()
        {
            bool found = provider.TryCalculatePath(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 50f), out Vector3[] corners);

            Assert.IsFalse(found);
            Assert.IsNull(corners);
        }

        [Test]
        public void TryCalculatePath_FromAPointFarFromAnyFloor_FindsNoPath()
        {
            Assert.IsFalse(provider.TryCalculatePath(new Vector3(0f, 30f, 0f), new Vector3(5f, 0f, 5f), out _));
        }

        [Test]
        public void TrySnap_PutsAPointAboveTheFloorOnIt()
        {
            bool snapped = provider.TrySnap(new Vector3(3f, 0.5f, 3f), 1f, out Vector3 point);

            Assert.IsTrue(snapped);
            Assert.AreEqual(0f, point.y, 0.15f, "a baked NavMesh sits a few centimetres above its source geometry (voxel size)");
            Assert.AreEqual(3f, point.x, 0.05f);
        }

        [Test]
        public void TrySnap_FailsWhenTheFloorIsFurtherAwayThanTheLimit()
        {
            bool snapped = provider.TrySnap(new Vector3(3f, 5f, 3f), 1f, out Vector3 point);

            Assert.IsFalse(snapped);
            Assert.AreEqual(new Vector3(3f, 5f, 3f), point, "the position is returned unchanged");
        }

        [Test]
        public void IsBlocked_IsFalseWhenTheWayStaysOnTheFloor()
        {
            Assert.IsFalse(provider.IsBlocked(new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f)));
        }

        [Test]
        public void IsBlocked_IsTrueWhenTheWayLeavesTheFloor()
        {
            Assert.IsTrue(provider.IsBlocked(new Vector3(0f, 0f, 0f), new Vector3(12f, 0f, 0f)), "the floor ends at x = 10");
        }

        [Test]
        public void IsBlocked_IsFalseWhenTheStartIsNotOnAnyFloor()
        {
            Assert.IsFalse(provider.IsBlocked(new Vector3(0f, 30f, 0f), new Vector3(5f, 0f, 0f)));
        }
    }
}
