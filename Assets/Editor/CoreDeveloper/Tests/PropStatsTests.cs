using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class PropStatsTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Root");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        private GameObject AddPrimitive(PrimitiveType type, Vector3 position, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            return go;
        }

        [Test]
        public void Triangles_CountsACubeAsTwelve()
        {
            AddPrimitive(PrimitiveType.Cube, Vector3.zero, Vector3.one);
            AddPrimitive(PrimitiveType.Cube, Vector3.up, Vector3.one);

            Assert.AreEqual(24, PropStats.Triangles(root));
        }

        [Test]
        public void Triangles_IncludesInactiveChildren()
        {
            AddPrimitive(PrimitiveType.Cube, Vector3.zero, Vector3.one);
            AddPrimitive(PrimitiveType.Cube, Vector3.up, Vector3.one).SetActive(false);

            Assert.AreEqual(24, PropStats.Triangles(root));
        }

        [Test]
        public void Triangles_IsZeroWithoutMeshes()
        {
            Assert.AreEqual(0, PropStats.Triangles(root));
        }

        [Test]
        public void LocalBounds_IsTheModelSizeWhateverTheRootPosition()
        {
            root.transform.position = new Vector3(10f, 5f, -3f);
            AddPrimitive(PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
            AddPrimitive(PrimitiveType.Cube, new Vector3(2f, 1f, 0f), new Vector3(1f, 2f, 3f));

            Bounds bounds = PropStats.LocalBounds(root);

            Assert.AreEqual(new Vector3(3f, 2f, 3f), bounds.size);
            Assert.AreEqual(new Vector3(1f, 1f, 0f), bounds.center);
        }

        [Test]
        public void LocalBounds_LeavesOutInactiveChildren()
        {
            AddPrimitive(PrimitiveType.Cube, Vector3.zero, Vector3.one);
            AddPrimitive(PrimitiveType.Cube, new Vector3(5f, 0f, 0f), Vector3.one).SetActive(false);

            Assert.AreEqual(Vector3.one, PropStats.LocalBounds(root).size);
        }

        [Test]
        public void Materials_CountsDistinctAssetsOnly()
        {
            var a = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var b = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AddPrimitive(PrimitiveType.Cube, Vector3.zero, Vector3.one).GetComponent<Renderer>().sharedMaterial = a;
            AddPrimitive(PrimitiveType.Cube, Vector3.up, Vector3.one).GetComponent<Renderer>().sharedMaterial = a;
            AddPrimitive(PrimitiveType.Cube, Vector3.down, Vector3.one).GetComponent<Renderer>().sharedMaterial = b;

            try
            {
                Assert.AreEqual(2, PropStats.Materials(root));
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void Colliders_CountsEveryColliderBelowTheRoot()
        {
            AddPrimitive(PrimitiveType.Cube, Vector3.zero, Vector3.one); // primitives come with a collider
            AddPrimitive(PrimitiveType.Sphere, Vector3.up, Vector3.one);

            Assert.AreEqual(2, PropStats.Colliders(root));
        }
    }
}
