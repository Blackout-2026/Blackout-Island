using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    /// <summary>Step OC1: the sea mesh, the wet sand strip, the driftwood, the sand ripple texture, the shore placement and the shaders.</summary>
    public class OceanBuilderTests
    {
        // A plain beach: 0.115 m per metre, height 0 at z -105 (the real slope, without the rest of the island).
        private static float Slope(float x, float z) => (z + 105f) * 0.115f;

        // A cliff: far too steep for a wet band.
        private static float Cliff(float x, float z) => (z + 105f) * 2f;

        private static float TriangleUpness(Mesh mesh, int triangle)
        {
            int[] t = mesh.triangles;
            Vector3[] v = mesh.vertices;
            return Vector3.Cross(v[t[triangle * 3 + 1]] - v[t[triangle * 3]], v[t[triangle * 3 + 2]] - v[t[triangle * 3]]).y;
        }

        // ---- ocean mesh ----

        [Test]
        public void OceanMesh_IsAFlatGridOfTheRightSizeThatFitsSixteenBitIndices()
        {
            Mesh mesh = OceanMesh.Create();
            Assert.AreEqual(129 * 129, mesh.vertexCount);
            Assert.That(mesh.vertexCount, Is.LessThan(65535), "a UInt16 index buffer");
            Assert.AreEqual(128 * 128 * 2, mesh.triangles.Length / 3);
            Assert.That(mesh.vertices.All(v => v.y == 0f), "flat: the shader makes the waves");
            Assert.AreEqual(520f, mesh.bounds.size.x, 1e-3f);
            Assert.AreEqual(520f, mesh.bounds.size.z, 1e-3f);
            Assert.That(mesh.bounds.size.y, Is.GreaterThanOrEqualTo(0.3f), "the bounds carry the swell height");
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void OceanMesh_EveryTriangleFacesUpSoTheTopOfTheWaterIsDrawn()
        {
            Mesh mesh = OceanMesh.Create(8, 80f);
            for (int i = 0; i < mesh.triangles.Length / 3; i++)
            {
                Assert.That(TriangleUpness(mesh, i), Is.GreaterThan(0f), "triangle " + i);
            }

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        // ---- wet sand strip ----

        [Test]
        public void WetSandStrip_SitsJustAboveTheGroundBetweenTheTwoHeightsAndFadesOut()
        {
            Mesh mesh = WetSandStrip.Create(Slope);
            Assert.That(mesh.triangles.Length, Is.GreaterThan(0));
            Vector3[] vertices = mesh.vertices;
            Color[] colors = mesh.colors;
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.AreEqual(Slope(vertices[i].x, vertices[i].z) + WetSandStrip.Lift, vertices[i].y, 1e-3f, "vertex " + i);
            }

            int columns = vertices.Length / WetSandStrip.Rows;
            for (int c = 0; c < columns; c++)
            {
                int first = c * WetSandStrip.Rows;
                Assert.AreEqual(1f, colors[first].a, 1e-4f, "fully wet at the waterline");
                Assert.AreEqual(0f, colors[first + WetSandStrip.Rows - 1].a, 1e-4f, "dry at the top edge");
                Assert.AreEqual(WetSandStrip.LowHeight, Slope(vertices[first].x, vertices[first].z), 0.01f);
            }

            for (int i = 0; i < mesh.triangles.Length / 3; i++)
            {
                Assert.That(TriangleUpness(mesh, i), Is.GreaterThan(0f), "triangle " + i);
            }

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void WetSandStrip_SkipsCliffsWhereABandWouldBeAThinLine()
        {
            Mesh mesh = WetSandStrip.Create(Cliff);
            Assert.AreEqual(0, mesh.triangles.Length);
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        // ---- driftwood ----

        [Test]
        public void Driftwood_HasOutwardNormalsLiesOnTheGroundAndIsRepeatable()
        {
            Mesh a = DriftwoodMesh.Create("a", 9311, 2.6f, 0.12f);
            Mesh again = DriftwoodMesh.Create("a", 9311, 2.6f, 0.12f);
            Mesh other = DriftwoodMesh.Create("b", 9312, 2.6f, 0.12f);
            CollectionAssert.AreEqual(a.vertices, again.vertices);
            CollectionAssert.AreNotEqual(a.vertices, other.vertices);

            Assert.AreEqual(2.6f, a.bounds.size.x, 0.15f);
            Assert.That(a.bounds.min.y, Is.InRange(-0.02f, 0.06f), "the underside is at about local y 0");

            // Side vertices (not the cap centres): the normal points away from the log's axis.
            Vector3[] vertices = a.vertices;
            Vector3[] normals = a.normals;
            int checkedCount = 0;
            for (int i = 0; i < 9 * 9; i++) // the 9 rings of 9 side vertices come first; the caps follow
            {
                IEnumerable<Vector3> ring = vertices.Skip(i / 9 * 9).Take(9);
                Vector3 centre = new Vector3(vertices[i].x, ring.Average(v => v.y), ring.Average(v => v.z));
                Vector3 outward = vertices[i] - centre;
                if (outward.sqrMagnitude < 1e-6f) continue;
                Assert.That(Vector3.Dot(normals[i], outward.normalized), Is.GreaterThan(0f), "vertex " + i);
                checkedCount++;
            }

            Assert.That(checkedCount, Is.GreaterThan(50));
            foreach (Mesh mesh in new[] { a, again, other }) UnityEngine.Object.DestroyImmediate(mesh);
        }

        // ---- sand ripples ----

        [Test]
        public void SandRipple_TilesWithoutASeamAndLooksLikeANormalMap()
        {
            Texture2D texture = SandRippleTexture.Create(128);
            Color32[] pixels = texture.GetPixels32();
            Texture2D again = SandRippleTexture.Create(128);
            CollectionAssert.AreEqual(pixels, again.GetPixels32(), "same seed, same texture");

            int size = 128;
            float edgeStep = 0f;
            float innerStep = 0f;
            for (int y = 0; y < size; y++)
            {
                edgeStep += Mathf.Abs(pixels[y * size].r - pixels[y * size + size - 1].r); // wraps from the last column to the first
                innerStep += Mathf.Abs(pixels[y * size + 40].r - pixels[y * size + 41].r);
                Assert.That(pixels[y * size + 20].b, Is.GreaterThan(127), "tangent-space normals point out of the surface");
            }

            Assert.That(edgeStep, Is.LessThanOrEqualTo(innerStep * 3f + 40f), "the seam is no rougher than the inside of the tile");

            // No stripes: regular parallel ripples tilt the surface along one axis only. Natural sand tilts about equally
            // along both, so the average slope along x and along y must be within 1.5 times of each other.
            float slopeX = 0f;
            float slopeY = 0f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    slopeX += Mathf.Abs(pixels[y * size + x].r - 127.5f);
                    slopeY += Mathf.Abs(pixels[y * size + x].g - 127.5f);
                }
            }

            Assert.That(slopeX / slopeY, Is.InRange(1f / 1.5f, 1.5f), "sand tilts the same way in both directions");
            Assert.That(slopeX / (size * size), Is.InRange(3f, 40f), "a gentle but visible tilt (about 1.5 to 15 percent of full range)");
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(again);
        }

        // ---- placement ----

        [Test]
        public void ShorePlacement_ClearsTheDockCorridorAndTreesAndIsRepeatable()
        {
            List<ShoreProp> first = ShorePlacement.Plan(Slope, new List<Vector2>());
            List<ShoreProp> second = ShorePlacement.Plan(Slope, new List<Vector2>());
            Assert.AreEqual(ShorePlacement.RockCount + ShorePlacement.DriftwoodCount, first.Count);
            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].position, second[i].position);
                Assert.That(first[i].position.x, Is.Not.InRange(ShorePlacement.ClearMinX, ShorePlacement.ClearMaxX), "dock/trail corridor");
                for (int j = 0; j < i; j++)
                {
                    Vector2 d = new Vector2(first[i].position.x - first[j].position.x, first[i].position.z - first[j].position.z);
                    Assert.That(d.magnitude, Is.GreaterThanOrEqualTo(ShorePlacement.Spacing - 1e-3f));
                }
            }

            Assert.AreEqual(ShorePlacement.RockCount, first.Count(p => p.kind <= ShoreKind.RockRound));

            var trees = first.Select(p => new Vector2(p.position.x + 0.5f, p.position.z)).ToList();
            foreach (ShoreProp prop in ShorePlacement.Plan(Slope, trees))
            {
                foreach (Vector2 tree in trees)
                {
                    Assert.That(Vector2.Distance(tree, new Vector2(prop.position.x, prop.position.z)), Is.GreaterThanOrEqualTo(ShorePlacement.TreeClearance - 1e-3f));
                }
            }
        }

        // ---- shaders and assets ----

        [TestCase(OceanBuilder.OceanShaderPath)]
        [TestCase(OceanBuilder.WetSandShaderPath)]
        public void Shaders_CompileWithoutErrorsAndAreSupported(string path)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.NotNull(shader, path);
            Assert.That(shader.isSupported, path + " is not supported on this GPU/pipeline");
            string errors = string.Join("\n", ShaderUtil.GetShaderMessages(shader)
                .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                .Select(m => $"line {m.line}: {m.message}"));
            Assert.IsEmpty(errors, path);
        }

        [Test]
        public void OceanAssets_WhenBuilt_UseTheOceanShaderAndTheSmallMesh()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(OceanBuilder.OceanMaterialPath);
            if (material == null) Assert.Ignore("Run Blackout > World > Build Ocean and Shore first.");
            Assert.AreEqual("Blackout/Ocean", material.shader.name);
            Assert.AreEqual(3000, material.renderQueue);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(OceanBuilder.MeshPath);
            Assert.AreEqual(OceanMesh.MeshName, mesh.name);
            Assert.AreEqual((OceanMesh.Cells + 1) * (OceanMesh.Cells + 1), mesh.vertexCount);
            var wet = AssetDatabase.LoadAssetAtPath<Material>(OceanBuilder.WetSandMaterialPath);
            Assert.AreEqual("Blackout/WetSand", wet.shader.name);
        }
    }
}
