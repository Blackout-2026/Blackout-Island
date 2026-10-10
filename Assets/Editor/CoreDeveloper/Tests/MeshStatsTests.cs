using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.Editor.Tests
{
    public class MeshStatsTests
    {
        private Mesh mesh;

        [TearDown]
        public void TearDown()
        {
            if (mesh != null)
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void CountTriangles_CountsOneSubmesh()
        {
            mesh = new Mesh { vertices = Quad(), triangles = new[] { 0, 1, 2, 0, 2, 3 } };

            Assert.That(MeshStats.CountTriangles(mesh), Is.EqualTo(2));
        }

        [Test]
        public void CountTriangles_AddsAllSubmeshes()
        {
            mesh = new Mesh { vertices = Quad() };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 0, 2, 3 }, 1);

            Assert.That(MeshStats.CountTriangles(mesh), Is.EqualTo(2));
        }

        [Test]
        public void CountTriangles_CountsAQuadAsTwoTriangles()
        {
            mesh = new Mesh { vertices = Quad() };
            mesh.SetIndices(new[] { 0, 1, 2, 3 }, MeshTopology.Quads, 0);

            Assert.That(MeshStats.CountTriangles(mesh), Is.EqualTo(2));
        }

        [Test]
        public void CountTriangles_IgnoresLines()
        {
            mesh = new Mesh { vertices = Quad() };
            mesh.SetIndices(new[] { 0, 1, 2, 3 }, MeshTopology.Lines, 0);

            Assert.That(MeshStats.CountTriangles(mesh), Is.EqualTo(0));
        }

        [Test]
        public void HasUv0_IsFalseUntilUvsAreAssigned()
        {
            mesh = new Mesh { vertices = Quad(), triangles = new[] { 0, 1, 2 } };
            Assert.That(MeshStats.HasUv0(mesh), Is.False);

            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            Assert.That(MeshStats.HasUv0(mesh), Is.True);
        }

        private static Vector3[] Quad()
        {
            return new[]
            {
                new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0)
            };
        }
    }
}
