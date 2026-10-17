using System.Collections.Generic;
using System.IO;
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
    /// <summary>Checks the committed scene, not just temporary generator output. Never saves or bakes it.</summary>
    public class SavedDressingTests
    {
        private Scene scene;
        private GameObject[] roots;
        private Transform dressing;
        private DressingSpec[] available;
        private List<MarkerSpec> markers;

        [OneTimeSetUp]
        public void OpenSavedSceneForReading()
        {
            scene = EditorSceneManager.OpenPreviewScene(WorldSceneSkeleton.ScenePath);
            roots = scene.GetRootGameObjects();
            dressing = roots.Single(r => r.name == "Props").transform.Find(BunkerDresser.ContainerName);
            Assert.That(dressing, Is.Not.Null, "Run Dress Bunker and save Environment first.");
            available = DressingLayout.Parse(File.ReadAllText(DressingLayout.DocumentPath))
                .Where(s => AssetDatabase.LoadAssetAtPath<GameObject>(s.PrefabPath) != null).ToArray();
            Assert.That(available.Length, Is.GreaterThan(0));
            markers = roots.SelectMany(r => r.GetComponentsInChildren<Marker>(true))
                .Select(m => new MarkerSpec(m.Type, m.Id, m.Owner, m.transform.position, m.transform.eulerAngles.y)).ToList();
        }

        [OneTimeTearDown]
        public void CloseWithoutSaving()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }

        private Transform Instance(DressingSpec spec)
        {
            Transform instance = dressing.Find(spec.Room + "/" + spec.Room + "_" + spec.Id);
            Assert.That(instance, Is.Not.Null, spec.Id);
            return instance;
        }

        [Test]
        public void SavedInstances_KeepPrefabLinksAndTableTransforms()
        {
            foreach (DressingSpec spec in available)
            {
                Transform instance = Instance(spec);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject), Is.SameAs(prefab), spec.Id);
                Assert.That(Vector3.Distance(instance.position, spec.Position), Is.LessThan(0.001f), spec.Id);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(instance.eulerAngles.y, spec.Yaw)), Is.LessThan(0.001f), spec.Id);
                Assert.That(Vector3.Distance(instance.localScale, Vector3.Scale(prefab.transform.localScale, spec.Scale)),
                    Is.LessThan(0.001f), spec.Id);
                Assert.That(BunkerDresser.HasDynamicContent(instance.gameObject), Is.False, spec.Id);
            }
        }

        [Test]
        public void SavedBounds_LeaveRoutesAndActualMarkersFree()
        {
            foreach (DressingSpec spec in available)
            {
                Assert.That(BunkerDresser.TryBounds(Instance(spec).gameObject, out Bounds bounds), Is.True, spec.Id);
                Assert.That(DressingLayout.ClearanceIssue(bounds, markers), Is.Null, spec.Id);
            }
        }

        [Test]
        public void OpaqueFurniture_IsStaticWhileGlassAndEmissionAvoidLightmaps()
        {
            int opaque = 0, emissive = 0;
            foreach (Renderer renderer in dressing.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool glows = materials.Any(m => m != null && m.HasProperty("_EmissionColor")
                    && m.GetColor("_EmissionColor").maxColorComponent > 0);
                bool transparent = materials.Any(m => m != null && m.renderQueue >= 3000);
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if (glows)
                {
                    emissive++;
                    Assert.That(flags, Is.EqualTo((StaticEditorFlags)0), renderer.name);
                }
                else if (transparent) Assert.That(flags, Is.EqualTo(StaticFlagRules.SeeThrough), renderer.name);
                else
                {
                    opaque++;
                    Assert.That(flags, Is.EqualTo(StaticFlagRules.Environment), renderer.name);
                }
            }
            Assert.That(opaque, Is.GreaterThan(0));
            Assert.That(emissive, Is.GreaterThan(0));
        }

        [Test]
        public void GeneratedMonitorScreens_AreWiredOnceAndRemainDynamic()
        {
            PowerStateLighting power = roots.Single(r => r.name == LightingSetup.LightingRootName).GetComponent<PowerStateLighting>();
            Assert.That(power, Is.Not.Null);
            var serialized = new SerializedObject(power);
            SerializedProperty screens = serialized.FindProperty("screens");
            var assigned = new List<Renderer>();
            for (int i = 0; i < screens.arraySize; i++) assigned.Add(screens.GetArrayElementAtIndex(i).objectReferenceValue as Renderer);
            foreach (DressingSpec spec in available.Where(s => s.Prefab == "Prop_Monitor" || s.Prefab == "Prop_WallMonitor"))
            {
                Renderer screen = Instance(spec).GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Screen");
                Assert.That(assigned.Count(r => r == screen), Is.EqualTo(1), spec.Id);
                Assert.That(GameObjectUtility.GetStaticEditorFlags(screen.gameObject), Is.EqualTo((StaticEditorFlags)0), spec.Id);
            }
        }

        [Test]
        public void MovingBoatAndMarkerObjects_KeepTheirDynamicFlags()
        {
            Transform boat = roots.Single(r => r.name == "Beach").transform.Find("Boat");
            Assert.That(boat, Is.Not.Null);
            foreach (Renderer renderer in boat.GetComponentsInChildren<Renderer>(true))
                Assert.That(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject), Is.EqualTo((StaticEditorFlags)0), renderer.name);
            foreach (Marker marker in roots.SelectMany(r => r.GetComponentsInChildren<Marker>(true)))
                Assert.That(GameObjectUtility.GetStaticEditorFlags(marker.gameObject), Is.EqualTo((StaticEditorFlags)0), marker.Id);
        }

        [Test]
        public void SavedLightingSettings_KeepDirectLightControllableAtBlackout()
        {
            LightingSettings settings = Lightmapping.GetLightingSettingsForScene(scene);
            Assert.That(settings, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(settings), Is.EqualTo(LightingSetup.LightingSettingsPath));
            Assert.That(settings.bakedGI, Is.True);
            Assert.That(settings.realtimeGI, Is.False);
            Assert.That(settings.mixedBakeMode, Is.EqualTo(MixedLightingMode.IndirectOnly));
            Assert.That(settings.lightmapResolution, Is.EqualTo(10));
            Assert.That(settings.lightmapMaxSize, Is.EqualTo(1024));
        }
    }
}
