using System.Linq;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class MaterialApplierTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";

        private GameObject part;
        private Mesh mesh;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(part);
            Object.DestroyImmediate(mesh);
        }

        [TestCase("Laboratory", "Floor", "Tile")]
        [TestCase("Laboratory", "Wall_E_0", "Tile")]
        [TestCase("Laboratory", "Sill_S_D-Lab", "HazardStripe")]
        [TestCase("Corridor", "Floor", "ConcreteStained")]
        [TestCase("Corridor", "Wall_W_1", "Concrete")]
        [TestCase("BunkerEntranceRamp", "Floor", "MetalPlate")]
        [TestCase("BunkerEntranceRamp", "Ceiling", "Concrete")]
        [TestCase("GeneratorRoom", "Wall_N_0", "MetalPlate")]
        [TestCase("GeneratorRoom", "Floor", "MetalGrating")]
        [TestCase("SecurityArea", "Lintel_N_D-Security", "Concrete")]
        public void SurfaceFor_PicksTheSurfaceByRoomAndPart(string room, string partName, string expected)
        {
            Assert.That(MaterialRules.SurfaceFor(room, partName), Is.EqualTo(expected));
        }

        [Test]
        public void SurfaceFor_UnknownPartHasNoSurface()
        {
            Assert.That(MaterialRules.SurfaceFor("Corridor", "Crate"), Is.Null);
        }

        [Test]
        public void Rules_OnlyNameLibrarySurfaces()
        {
            foreach (MaterialRules.Rule rule in MaterialRules.Rules)
            {
                Assert.That(SurfaceLibrary.Find(rule.Surface), Is.Not.Null, rule.Surface);
            }
        }

        [Test]
        public void Rules_CoverEveryPartTheBlockoutBuilds()
        {
            var layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);
            Assert.That(layout, Is.Not.Null, LayoutPath);
            foreach (Room room in layout.rooms)
            {
                foreach (ShellPart shellPart in BunkerShell.BuildRoom(layout, room))
                {
                    Assert.That(MaterialRules.SurfaceFor(room.name, shellPart.Name), Is.Not.Null, $"{room.name}_{shellPart.Name}");
                }
            }
        }

        [Test]
        public void WorldUv_ProjectsAlongTheDominantAxisLikeThePrototype()
        {
            var point = new Vector3(1f, 2f, 3f);

            Assert.That(BoxUvMesh.WorldUv(point, Vector3.up), Is.EqualTo(new Vector2(1f, 3f)));
            Assert.That(BoxUvMesh.WorldUv(point, Vector3.left), Is.EqualTo(new Vector2(3f, 2f)));
            Assert.That(BoxUvMesh.WorldUv(point, Vector3.forward), Is.EqualTo(new Vector2(1f, 2f)));
        }

        [Test]
        public void Build_MakesAUnitBoxWithOutwardFaces()
        {
            BuildPart(new Vector3(10f, 2f, 5f), Quaternion.identity, new Vector3(4f, 3f, 0.4f));

            Assert.That(mesh.name, Is.EqualTo("Corridor_Wall_E_0_BoxUV"));
            Assert.That(mesh.bounds.size.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(mesh.bounds.size.z, Is.EqualTo(1f).Within(1e-5f));
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Assert.That(triangles.Length, Is.EqualTo(36));
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Assert.That(Vector3.Dot(normal, (a + b + c) / 3f), Is.GreaterThan(0f), $"triangle {i / 3} faces inward");
            }
        }

        [Test]
        public void Build_UvsAreWorldMetres()
        {
            BuildPart(new Vector3(10f, 2f, 5f), Quaternion.identity, new Vector3(4f, 3f, 0.4f));

            // The +Z face spans x 8 … 12 and y 0.5 … 3.5 in the world.
            Vector2[] faceUvs = Enumerable.Range(0, mesh.vertexCount)
                .Where(i => mesh.normals[i] == Vector3.forward).Select(i => mesh.uv[i]).ToArray();
            Assert.That(faceUvs.Length, Is.EqualTo(4));
            Assert.That(faceUvs.Min(uv => uv.x), Is.EqualTo(8f).Within(1e-4f));
            Assert.That(faceUvs.Max(uv => uv.x), Is.EqualTo(12f).Within(1e-4f));
            Assert.That(faceUvs.Min(uv => uv.y), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(faceUvs.Max(uv => uv.y), Is.EqualTo(3.5f).Within(1e-4f));
        }

        [Test]
        public void Build_TiltedTopFaceUsesWorldXz()
        {
            BuildPart(new Vector3(0f, 6f, 61f), Quaternion.Euler(-23.56f, 0f, 0f), new Vector3(3f, 0.4f, 18.76f));

            for (int i = 0; i < mesh.vertexCount; i++)
            {
                if (mesh.normals[i] == Vector3.up)
                {
                    Vector3 world = part.transform.TransformPoint(mesh.vertices[i]);
                    Assert.That(mesh.uv[i].x, Is.EqualTo(world.x).Within(1e-3f));
                    Assert.That(mesh.uv[i].y, Is.EqualTo(world.z).Within(1e-3f));
                }
            }
        }

        [Test]
        public void Build_HasLightmapUvsInsideTheUnitSquare()
        {
            BuildPart(Vector3.zero, Quaternion.identity, new Vector3(20f, 0.4f, 18f));

            var uv2 = new System.Collections.Generic.List<Vector2>();
            mesh.GetUVs(1, uv2);
            Assert.That(uv2.Count, Is.EqualTo(mesh.vertexCount));
            Assert.That(uv2.All(uv => uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f), Is.True);
        }

        [Test]
        public void Build_TwiceGivesTheSameMesh()
        {
            BuildPart(new Vector3(3f, 2.5f, 90f), Quaternion.identity, new Vector3(0.4f, 3.8f, 12f));
            Vector2[] firstUv = mesh.uv;
            Vector2[] firstUv2 = mesh.uv2;

            BoxUvMesh.Build(mesh, part.transform);

            Assert.That(mesh.uv, Is.EqualTo(firstUv));
            Assert.That(mesh.uv2, Is.EqualTo(firstUv2));
        }

        private void BuildPart(Vector3 position, Quaternion rotation, Vector3 size)
        {
            part = new GameObject("Corridor_Wall_E_0");
            part.transform.SetPositionAndRotation(position, rotation);
            part.transform.localScale = size;
            mesh = new Mesh();
            BoxUvMesh.Build(mesh, part.transform);
        }
    }
}
