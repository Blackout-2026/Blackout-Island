using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor.Tests
{
    public class SceneReportTests
    {
        private Scene scene;
        private Scene otherScene;

        [SetUp]
        public void SetUp()
        {
            // Preview scenes avoid replacing the user's scene setup or saving temporary test assets.
            scene = EditorSceneManager.NewPreviewScene();
            otherScene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            if (otherScene.IsValid()) EditorSceneManager.ClosePreviewScene(otherScene);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }

        [Test]
        public void CaptureIncludesInactiveInstancesButExcludesOtherScenes()
        {
            GameObject first = Cube(scene);
            GameObject second = Cube(scene);
            second.SetActive(false);
            GameObjectUtility.SetStaticEditorFlags(first, StaticEditorFlags.BatchingStatic);
            Cube(otherScene);

            SceneReport.Snapshot result = SceneReport.Capture(scene);

            Assert.That(result.Objects, Is.EqualTo(2));
            Assert.That(result.Renderers, Is.EqualTo(2));
            Assert.That(result.EnabledRenderers, Is.EqualTo(1));
            Assert.That(result.ObjectsWithStaticFlags, Is.EqualTo(1));
            Assert.That(result.BatchFlaggedRenderers, Is.EqualTo(1));
            Mesh mesh = first.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(result.MeshTriangles, Is.EqualTo(2 * SceneReport.TriangleCount(mesh)));
            Assert.That(result.MeshVertices, Is.EqualTo(2 * mesh.vertexCount));
            Assert.That(result.Materials.Count, Is.EqualTo(1), "Shared materials count once, mesh instances count twice.");
            Assert.That(GameObjectUtility.GetStaticEditorFlags(first), Is.EqualTo(StaticEditorFlags.BatchingStatic));
            Assert.That(second.activeSelf, Is.False);
        }

        [Test]
        public void TriangleCountIgnoresLineSubmeshes()
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            try
            {
                mesh.subMeshCount = 2;
                mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
                mesh.SetIndices(new[] { 0, 1, 1, 2, 2, 0 }, MeshTopology.Lines, 1);
                Assert.That(SceneReport.TriangleCount(mesh), Is.EqualTo(1));
                Assert.That(SceneReport.TriangleCount(null), Is.Zero);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void CaptureSeparatesBakeModeAndInactiveLights()
        {
            GameObject first = Cube(scene);
            first.AddComponent<Light>().lightmapBakeType = LightmapBakeType.Mixed;
            GameObject second = Cube(scene);
            second.AddComponent<Light>().lightmapBakeType = LightmapBakeType.Realtime;
            second.SetActive(false);
            Cube(otherScene).AddComponent<Light>();

            SceneReport.Snapshot result = SceneReport.Capture(scene);

            Assert.That(result.Lights["Mixed / enabled"], Is.EqualTo(1));
            Assert.That(result.Lights["Realtime / disabled"], Is.EqualTo(1));
            Assert.That(result.Lights.Count, Is.EqualTo(2));
        }

        private static GameObject Cube(Scene target)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(cube, target);
            return cube;
        }
    }
}
