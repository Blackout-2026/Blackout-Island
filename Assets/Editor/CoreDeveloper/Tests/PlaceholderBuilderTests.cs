using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class PlaceholderBuilderTests
    {
        private GameObject root;
        private PartMaterials materials;

        [SetUp]
        public void SetUp()
        {
            TestAssetFolders.Create();
            materials = PropMaterialBuilder.BuildAll(TestAssetFolders.Materials, TestAssetFolders.Textures);
            root = new GameObject("Root");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            TestAssetFolders.Delete();
        }

        private Bounds Build(ModelDefinition definition)
        {
            PlaceholderBuilder.Build(root.transform, definition, materials);
            return PropStats.LocalBounds(root);
        }

        [Test]
        public void Box_HasTheGivenExtentsAtTheGivenCentre()
        {
            Bounds bounds = Build(new ModelDefinition("T").Box("B", 1f, 2f, 3f, 0.5f, 1f, 2f, "Steel"));

            Assert.AreEqual(new Vector3(0.5f, 1f, 2f), bounds.size);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), bounds.center);
        }

        [TestCase(PartShape.CylinderX, 4f, 0.5f, 0.5f)]
        [TestCase(PartShape.CylinderY, 0.5f, 4f, 0.5f)]
        [TestCase(PartShape.CylinderZ, 0.5f, 0.5f, 4f)]
        public void Cylinder_RunsAlongItsAxisWithTheGivenLength(PartShape shape, float sx, float sy, float sz)
        {
            var definition = new ModelDefinition("T").Cylinder("C", shape, 0f, 0f, 0f, 0.25f, 4f, Vector3.zero, "Steel");

            Vector3 size = Build(definition).size;

            Assert.AreEqual(sx, size.x, 0.01f);
            Assert.AreEqual(sy, size.y, 0.01f);
            Assert.AreEqual(sz, size.z, 0.01f);
        }

        [Test]
        public void Parts_UseTheMaterialOfTheirPaintKey()
        {
            Build(new ModelDefinition("T").Box("B", 0f, 0f, 0f, 1f, 1f, 1f, "Wood"));

            Assert.AreEqual(materials.Get("Wood"), root.transform.Find("B").GetComponent<Renderer>().sharedMaterial);
        }

        [Test]
        public void Parts_HaveNoColliders()
        {
            Build(new ModelDefinition("T").Box("B", 0f, 0f, 0f, 1f, 1f, 1f, "Steel").Sphere("S", 0f, 0f, 0f, 0.5f, "Steel"));

            Assert.AreEqual(0, PropStats.Colliders(root));
        }

        [Test]
        public void Pivots_NestAndMoveTheirParts()
        {
            var definition = new ModelDefinition("T")
                .Pivot("Hand", 0f, 1f, 0f)
                .Pivot("Arm", 0f, 0f, 2f, Vector3.zero, "Hand")
                .Box("Glove", 0f, 0f, 0f, 0.2f, 0.2f, 0.2f, "Steel", "Arm");

            Build(definition);

            Transform glove = root.transform.Find("Hand/Arm/Glove");
            Assert.IsNotNull(glove);
            Assert.AreEqual(new Vector3(0f, 1f, 2f), glove.position);
        }

        [Test]
        public void APartOnAnUndeclaredPivot_ThrowsWithTheNames()
        {
            var definition = new ModelDefinition("T").Box("Glove", 0f, 0f, 0f, 1f, 1f, 1f, "Steel", "Nowhere");

            var error = Assert.Throws<KeyNotFoundException>(() => PlaceholderBuilder.Build(root.transform, definition, materials));
            StringAssert.Contains("Glove", error.Message);
            StringAssert.Contains("Nowhere", error.Message);
        }

        [Test]
        public void AddColliders_AddsBoxAndCapsuleToTheRoot()
        {
            var definition = new ModelDefinition("T").BoxCollider(0f, 1f, 0f, 2f, 2f, 2f).CapsuleCollider(0f, 0.5f, 0f, 0.3f, 1f);

            PlaceholderBuilder.AddColliders(root, definition);

            var box = root.GetComponent<BoxCollider>();
            var capsule = root.GetComponent<CapsuleCollider>();
            Assert.AreEqual(new Vector3(2f, 2f, 2f), box.size);
            Assert.AreEqual(new Vector3(0f, 1f, 0f), box.center);
            Assert.AreEqual(0.3f, capsule.radius);
            Assert.AreEqual(1f, capsule.height);
            Assert.AreEqual(1, capsule.direction);
        }

        [Test]
        public void AddColliders_LeavesARootThatAlreadyHasOne()
        {
            root.AddComponent<SphereCollider>();

            PlaceholderBuilder.AddColliders(root, new ModelDefinition("T").BoxCollider(0f, 0f, 0f, 1f, 1f, 1f));

            Assert.AreEqual(1, PropStats.Colliders(root));
            Assert.IsNull(root.GetComponent<BoxCollider>());
        }
    }
}
