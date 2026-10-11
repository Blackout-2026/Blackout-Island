using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor.Tests
{
    public class VegetationMeshesTests
    {
        // Triangles per LOD. Pine: trunk sides x rows x 2 + tiers x sides. Palm: trunk segments x sides x 2
        // + 9 fronds x steps x 4 (+ 4 coconuts x 8). Rock: 20 x (detail + 1)^2.
        [TestCase("Pine", 0, 108)]
        [TestCase("Pine", 1, 46)]
        [TestCase("Pine", 2, 23)]
        [TestCase("Palm", 0, 712)]
        [TestCase("Palm", 1, 230)]
        [TestCase("Palm", 2, 96)]
        [TestCase("Rock", 0, 180)]
        [TestCase("Rock", 1, 80)]
        [TestCase("Rock", 2, 20)]
        public void Lod_HasTheExpectedTriangleCount(string kind, int lod, int triangles)
        {
            Mesh mesh = Make(kind, lod);
            Assert.That(VegetationPlaceholderBuilder.TriangleCount(mesh), Is.EqualTo(triangles));
            Object.DestroyImmediate(mesh);
        }

        [TestCase("Pine")]
        [TestCase("Palm")]
        [TestCase("Rock")]
        public void EachLod_HasFewerTrianglesThanTheOneBefore(string kind)
        {
            int previous = int.MaxValue;
            for (int lod = 0; lod < VegetationMeshes.LodCount; lod++)
            {
                Mesh mesh = Make(kind, lod);
                int triangles = VegetationPlaceholderBuilder.TriangleCount(mesh);
                Object.DestroyImmediate(mesh);
                Assert.That(triangles, Is.LessThan(previous), $"{kind} LOD{lod}");
                previous = triangles;
            }
        }

        [TestCase("Pine", 0)]
        [TestCase("Pine", 2)]
        [TestCase("Palm", 0)]
        [TestCase("Palm", 2)]
        [TestCase("Rock", 0)]
        [TestCase("Bush", 0)]
        [TestCase("GrassTuft", 0)]
        public void Mesh_HasNoInvalidVertices(string kind, int lod)
        {
            Mesh mesh = Make(kind, lod);
            foreach (Vector3 v in mesh.vertices)
            {
                Assert.That(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), Is.False, $"{kind} LOD{lod}");
            }

            foreach (Vector3 n in mesh.normals)
            {
                Assert.That(n.magnitude, Is.EqualTo(1f).Within(1e-3f), $"{kind} LOD{lod} normal");
            }

            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void Pine_IsAboutTenMetresTall()
        {
            // Reference: trunk 9.6 m, cone tiers up to about 10.6 m; lowest branches 2.4 .. 2.6 m from the trunk,
            // bent in or out by up to 35 % (jag).
            for (int lod = 0; lod < VegetationMeshes.LodCount; lod++)
            {
                Mesh pine = VegetationMeshes.Pine(lod);
                Assert.That(pine.bounds.min.y, Is.EqualTo(0f).Within(1e-3f));
                Assert.That(pine.bounds.max.y, Is.InRange(9.6f, 11f), $"LOD{lod}");
                Assert.That(pine.bounds.extents.x, Is.InRange(2f, 3.6f), $"LOD{lod}");
                Object.DestroyImmediate(pine);
            }
        }

        [Test]
        public void Palm_IsAboutEightMetresTall()
        {
            // Reference: 7.5 m curved trunk, fronds 3.4 .. 4.4 m.
            Mesh palm = VegetationMeshes.Palm(0);
            Assert.That(palm.bounds.max.y, Is.InRange(7.5f, 9f));
            Assert.That(palm.bounds.size.x, Is.InRange(5f, 11f));
            Object.DestroyImmediate(palm);
        }

        [Test]
        public void Rock_IsClosedAndFacesOutward()
        {
            // The signed volume of a closed mesh is positive when every face points outward.
            for (int lod = 0; lod < VegetationMeshes.LodCount; lod++)
            {
                Mesh rock = VegetationMeshes.Rock(lod);
                Vector3[] v = rock.vertices;
                int[] t = rock.triangles;
                float volume = 0f;
                for (int i = 0; i < t.Length; i += 3)
                {
                    volume += Vector3.Dot(v[t[i]], Vector3.Cross(v[t[i + 1]], v[t[i + 2]])) / 6f;
                }

                // A unit sphere squashed to 0.62 and 0.85 holds 4.19 x 0.62 x 0.85 = 2.2 m3 (LOD2's 20 faces about
                // 60 % of that); noise moves it either way.
                Assert.That(volume, Is.InRange(0.8f, 3.5f), $"LOD{lod}");
                Assert.That(rock.bounds.max.y, Is.LessThan(0.62f * 1.375f + 1e-3f));
                Object.DestroyImmediate(rock);
            }
        }

        [Test]
        public void GrassTuft_IsThreeCardsFacingUp()
        {
            Mesh grass = VegetationMeshes.GrassTuft();
            Assert.That(VegetationPlaceholderBuilder.TriangleCount(grass), Is.EqualTo(6));
            Assert.That(Array.TrueForAll(grass.normals, n => n == Vector3.up), Is.True);
            Assert.That(grass.bounds.max.y, Is.EqualTo(0.745f).Within(1e-4f));
            Object.DestroyImmediate(grass);
        }

        [Test]
        public void GrassBlades_AreBladesOnATransparentBackground()
        {
            Color32[] pixels = VegetationMeshes.GrassBlades(VegetationPlaceholderBuilder.GrassTextureSize);
            int opaque = Array.FindAll(pixels, p => p.a == 255).Length;
            float coverage = (float)opaque / pixels.Length;
            Assert.That(coverage, Is.InRange(0.05f, 0.5f));
            Assert.That(VegetationMeshes.GrassBlades(VegetationPlaceholderBuilder.GrassTextureSize), Is.EqualTo(pixels));
        }

        [Test]
        public void Meshes_AreDeterministic()
        {
            Mesh a = VegetationMeshes.Palm(0);
            Mesh b = VegetationMeshes.Palm(0);
            Assert.That(b.vertices, Is.EqualTo(a.vertices));
            Assert.That(b.triangles, Is.EqualTo(a.triangles));
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        [Test]
        public void HslToRgb_MatchesThreeJs()
        {
            // three.js: new Color().setHSL(0.29, 0.35, 0.65) = (0.5912, 0.7725, 0.5275).
            Color c = VegetationPlaceholderBuilder.HslToRgb(0.29f, 0.35f, 0.65f);
            Assert.That(c.r, Is.EqualTo(0.5912f).Within(1e-3f));
            Assert.That(c.g, Is.EqualTo(0.7725f).Within(1e-3f));
            Assert.That(c.b, Is.EqualTo(0.5275f).Within(1e-3f));
        }

        [Test]
        public void LodDistance_FollowsUnitysScreenHeightRule()
        {
            // size / (2 x 0.25 x tan 36 degrees) = 10.6 / 0.3633 = 29.2 m.
            Assert.That(VegetationLibrary.LodDistance(10.6f, 0.25f), Is.EqualTo(29.18f).Within(0.05f));
        }

        private static Mesh Make(string kind, int lod)
        {
            switch (kind)
            {
                case "Pine":
                    return VegetationMeshes.Pine(lod);
                case "Palm":
                    return VegetationMeshes.Palm(lod);
                case "Rock":
                    return VegetationMeshes.Rock(lod);
                case "Bush":
                    return VegetationMeshes.Bush();
                default:
                    return VegetationMeshes.GrassTuft();
            }
        }
    }
}
