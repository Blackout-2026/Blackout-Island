using System.Linq;
using Blackout.Environment;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor.Tests
{
    /// <summary>Read the saved integration, including prefab links, collision and the existing exit/light.</summary>
    public class SavedBoatTests
    {
        private Scene scene;
        private Transform boat;

        [SetUp]
        public void OpenSavedEnvironment()
        {
            scene = EditorSceneManager.OpenPreviewScene(WorldSceneSkeleton.ScenePath);
            boat = scene.GetRootGameObjects().Single(r => r.name == "Beach").transform.Find("Boat");
        }

        [TearDown]
        public void CloseWithoutSaving() => EditorSceneManager.ClosePreviewScene(scene);

        [Test]
        public void SavedVisual_UsesOriginalGislingeGeometryAndBothUrpMaterials()
        {
            Transform visual = boat.Find(RowboatPlacement.VisualName);
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject)),
                Is.EqualTo(RowboatAssets.PrefabPath));
            Transform model = visual.Find("Model");
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(model.gameObject)),
                Is.EqualTo(RowboatAssets.ModelPath));
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            Assert.That(renderers.Length, Is.EqualTo(21));
            Assert.That(renderers.SelectMany(r => r.sharedMaterials).Distinct().Count(), Is.EqualTo(2));
            foreach (Renderer renderer in renderers)
            {
                Material material = renderer.sharedMaterial;
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(material.name, Is.EqualTo(renderer.name == "Sail Full" ? "M_Gislinge_Sail" : "M_Gislinge_Hull"));
                foreach (string slot in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                    Assert.That(material.GetTexture(slot), Is.Not.Null, renderer.name + slot);
                Assert.That(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject), Is.EqualTo((StaticEditorFlags)0), renderer.name);
            }
            Assert.That(renderers.Single(r => r.name == "Sail Full").sharedMaterial.GetFloat("_Cull"), Is.Zero);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(visual.GetComponent<LODGroup>().GetLODs()[0].renderers.Length, Is.EqualTo(21));
        }

        [Test]
        public void SavedCollision_FitsNarrowHullAndLeavesDockRouteClear()
        {
            Assert.That(Vector3.Distance(boat.position, new Vector3(3.7f, 0.15f, -122.5f)), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(boat.Find(RowboatPlacement.VisualName).localPosition,
                RowboatPlacement.VisualOffset), Is.LessThan(0.001f));
            Collider[] colliders = boat.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray();
            Assert.That(colliders.Length, Is.EqualTo(22));
            Assert.That(colliders.All(c => c is BoxCollider && c.gameObject.layer == PhysicsLayers.Level), Is.True);
            Assert.That(colliders.All(c => c.bounds.min.x > 1.4f), Is.True, "The fixed dock route must remain clear.");
            Transform collision = boat.Find("RowboatCollision");
            Assert.That(collision.Find("Bench0").GetComponent<Collider>().enabled, Is.False);
            Assert.That(collision.Find("Transom").GetComponent<Collider>().enabled, Is.False);
            Assert.That(collision.Find("Floor4").GetComponent<Collider>().bounds.max.y,
                Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(boat.Find("RowboatBoarding"), Is.Null);
        }

        [Test]
        public void SavedExitAndTorch_KeepExistingWiring()
        {
            GameObject[] roots = scene.GetRootGameObjects();
            Marker exit = roots.SelectMany(r => r.GetComponentsInChildren<Marker>()).Single(m => m.Type == MarkerType.BoatExit);
            Assert.That(exit.transform.position, Is.EqualTo(new Vector3(0f, 1.3f, -124f)));
            Transform fixture = roots.Single(r => r.name == "Lighting").transform.Find("OutdoorLights/OutdoorLights_BoatLantern");
            Renderer lamp = fixture.GetComponent<FlickerLight>().Lamp;
            Assert.That(lamp, Is.Not.Null);
            Assert.That(lamp.transform.IsChildOf(boat.Find("BoatTorch")), Is.True,
                "Existing flicker must still drive the torch embers.");
        }
    }
}
