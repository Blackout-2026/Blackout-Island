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

        [Test]
        public void CaptureIncludesTheSavedEnvironmentsSkyboxMaterial()
        {
            Scene environment = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            bool opened = !environment.IsValid() || !environment.isLoaded;
            if (opened) environment = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Additive);
            try
            {
                Material skybox = AssetDatabase.LoadAssetAtPath<Material>(LightingSetup.SkyMaterialPath);
                Assert.That(skybox, Is.Not.Null);

                SceneReport.Snapshot result = SceneReport.Capture(environment);

                Assert.That(result.Materials, Does.Contain(skybox), "The skybox is a scene dependency, not a Renderer material.");
                Assert.That(environment.isDirty, Is.False, "The report must not dirty the scene.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(environment, true); }
        }

        [Test]
        public void BatchingAuditReportsMissingAndUnsafeFlagsWithoutChangingThem()
        {
            var bunker = new GameObject("Bunker");
            var beach = new GameObject("Beach");
            SceneManager.MoveGameObjectToScene(bunker, scene);
            SceneManager.MoveGameObjectToScene(beach, scene);
            GameObject room = Cube(scene);
            room.name = "Room";
            room.transform.SetParent(bunker.transform);
            GameObject boat = Cube(scene);
            boat.name = "Boat";
            boat.transform.SetParent(beach.transform);
            GameObjectUtility.SetStaticEditorFlags(boat, StaticEditorFlags.BatchingStatic);
            Cube(otherScene);

            SceneBatchingAudit.Result result = SceneBatchingAudit.Capture(scene, new Material[0]);

            Assert.That(result.RuleTargets, Is.EqualTo(2));
            Assert.That(result.RuleMismatches, Does.Contain("Bunker/Room: BatchingStatic should be True"));
            Assert.That(result.RuleMismatches, Does.Contain("Beach/Boat: BatchingStatic should be False"));
            Assert.That(GameObjectUtility.GetStaticEditorFlags(room), Is.EqualTo((StaticEditorFlags)0));
            Assert.That(GameObjectUtility.GetStaticEditorFlags(boat), Is.EqualTo(StaticEditorFlags.BatchingStatic));
        }

        [Test]
        public void BatchingAuditCountsPropertyBlocksAndDeduplicatesConfiguredTargets()
        {
            GameObject lamp = Cube(scene);
            Renderer renderer = lamp.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_EmissionColor", Color.red);
            renderer.SetPropertyBlock(block);
            lamp.AddComponent<Blackout.Environment.FlickerLight>().Configure(
                Blackout.Environment.FlickerLight.Pattern.FaultyTube, 1, renderer);
            Cube(scene).AddComponent<Blackout.Environment.FlickerLight>().Configure(
                Blackout.Environment.FlickerLight.Pattern.FaultyTube, 2, renderer);

            SceneBatchingAudit.Result result = SceneBatchingAudit.Capture(scene, new Material[0]);

            Assert.That(result.PropertyBlocks, Is.EqualTo(1));
            Assert.That(result.ConfiguredPropertyBlockTargets, Is.EqualTo(1));
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetColor("_EmissionColor"), Is.EqualTo(Color.red));
        }

        [TestCase(0, null, SceneBatchingAudit.CompatibilityStatus.Compatible)]
        [TestCase(1, "Not initialized ()", SceneBatchingAudit.CompatibilityStatus.Unknown)]
        [TestCase(2, null, SceneBatchingAudit.CompatibilityStatus.Unknown)]
        [TestCase(2, "Material property is outside UnityPerMaterial", SceneBatchingAudit.CompatibilityStatus.Incompatible)]
        public void UninitialisedShaderDataIsNotReportedAsIncompatible(int code, string reason,
            SceneBatchingAudit.CompatibilityStatus expected)
        {
            Assert.That(SceneBatchingAudit.ClassifyCompatibility(code, reason), Is.EqualTo(expected));
        }

        private static GameObject Cube(Scene target)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(cube, target);
            return cube;
        }
    }
}
