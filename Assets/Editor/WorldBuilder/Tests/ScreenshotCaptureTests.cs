using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Blackout.Editor.Tests
{
    /// <summary>Runs real URP captures. Requires graphics (-force-d3d11), with logs and PNGs outside the repo.
    /// Opens saved scenes without saving; refuses to replace unsaved user work.</summary>
    public class ScreenshotCaptureTests
    {
        private SceneSetup[] previous;
        private readonly List<string> outputFolders = new List<string>();
        private const string OutputPrefix = "[World] Screenshot output folder: ";

        [SetUp]
        public void OpenSavedEnvironment()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null),
                "These integration tests require graphics; omit -nographics.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene open = SceneManager.GetSceneAt(i);
                Assert.That(open.isDirty, Is.False, "Save user work before running capture tests.");
                // The batch test runner creates its own untitled scene with runner objects.
                Assert.That(Application.isBatchMode || !string.IsNullOrEmpty(open.path) || open.rootCount == 0, Is.True,
                    "Close populated untitled scenes before running capture tests.");
            }
            previous = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            outputFolders.Clear();
            Application.logMessageReceived += RecordOutputFolder;
        }

        [TearDown]
        public void RestoreEditorSetup()
        {
            Application.logMessageReceived -= RecordOutputFolder;
            if (previous == null) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            previous = null;
        }

        [Test]
        public void DirtySceneIsRejectedBeforeCreatingOutputOrDiscardingEdits()
        {
            Scene scene = SceneManager.GetActiveScene();
            var handEdit = new GameObject("W12_TestUnsavedHandEdit");
            EditorSceneManager.MarkSceneDirty(scene);
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(ScreenshotCapture.CaptureScreenshots);
            Assert.That(error.Message, Does.Contain("Save or close"));
            Assert.That(handEdit != null, Is.True, "The user's unsaved object must survive refusal.");
            Assert.That(scene.isDirty, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene));
            Assert.That(outputFolders, Is.Empty);
        }

        [Test]
        public void RepeatedCapturesProduceSevenImagesAndRestoreSavedSceneAndAssets()
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            string state = SceneState();
            var dependencies = new Dictionary<string, string>();
            foreach (string path in AssetDatabase.GetDependencies(WorldSceneSkeleton.ScenePath, true))
            {
                dependencies[path] = Hash(path);
                if (File.Exists(path + ".meta")) dependencies[path + ".meta"] = Hash(path + ".meta");
            }

            Dictionary<string, string> firstOutput = null;
            for (int run = 0; run < 2; run++)
            {
                ScreenshotCapture.CaptureScreenshots();
                Assert.That(outputFolders.Count, Is.EqualTo(run + 1));
                string folder = outputFolders[run];
                VerifyImages(folder);
                SceneSetup[] restored = EditorSceneManager.GetSceneManagerSetup();
                Assert.That(restored.Length, Is.EqualTo(setup.Length));
                for (int i = 0; i < setup.Length; i++)
                {
                    Assert.That(restored[i].path, Is.EqualTo(setup[i].path));
                    Assert.That(restored[i].isLoaded, Is.EqualTo(setup[i].isLoaded));
                    Assert.That(restored[i].isActive, Is.EqualTo(setup[i].isActive));
                }
                Assert.That(SceneManager.GetActiveScene().isDirty, Is.False);
                Assert.That(SceneState(), Is.EqualTo(state), "Restore lights, volumes, transforms, particles and emission blocks.");
                foreach (var entry in dependencies)
                    Assert.That(Hash(entry.Key), Is.EqualTo(entry.Value), "Saved asset changed: " + entry.Key);

                if (run == 0)
                    firstOutput = Directory.GetFiles(folder).ToDictionary(path => path, Hash);
                else
                {
                    Assert.That(folder, Is.Not.EqualTo(outputFolders[0]), "A rerun must preserve the previous captures.");
                    foreach (var entry in firstOutput)
                        Assert.That(Hash(entry.Key), Is.EqualTo(entry.Value));
                }
            }
            Debug.Log($"[World] Capture verification passed: two sets of seven PNGs; scene state and {dependencies.Count} dependency/meta hashes unchanged.");
        }

        private static void VerifyImages(string folder)
        {
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(folder).StartsWith(repo, StringComparison.OrdinalIgnoreCase), Is.False);
            string[] names = { "01_Beach", "02_Forest", "03_BunkerEntrance", "04_Corridor",
                "05_GeneratorRoom_powered", "06_GeneratorRoom_blackout", "07_Lab" };
            Assert.That(Directory.GetFiles(folder, "*.png").Select(Path.GetFileNameWithoutExtension), Is.EquivalentTo(names));
            var brightness = new Dictionary<string, double>();
            foreach (string name in names)
            {
                var image = new Texture2D(2, 2);
                try
                {
                    Assert.That(image.LoadImage(File.ReadAllBytes(Path.Combine(folder, name + ".png"))), Is.True, name);
                    Assert.That(image.width, Is.EqualTo(1920), name);
                    Assert.That(image.height, Is.EqualTo(1080), name);
                    Color32[] pixels = image.GetPixels32();
                    double total = 0, min = 255, max = 0;
                    int samples = 0;
                    for (int i = 0; i < pixels.Length; i += 421)
                    {
                        Color32 p = pixels[i];
                        double value = 0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b;
                        total += value;
                        min = Math.Min(min, value);
                        max = Math.Max(max, value);
                        samples++;
                    }
                    Assert.That(max - min, Is.GreaterThan(15), name + " must contain a rendered view, not a flat clear colour.");
                    brightness[name] = total / samples;
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
            }
            Assert.That(brightness["06_GeneratorRoom_blackout"], Is.LessThan(brightness["05_GeneratorRoom_powered"] * 0.8),
                "The same-pose blackout view should be visibly darker than the powered view.");
            string manifest = File.ReadAllText(Path.Combine(folder, "capture.txt"));
            foreach (string name in names) Assert.That(manifest, Does.Contain(name + ": eye "));
            Assert.That(manifest, Does.Contain("Blackout preview: settled"));
        }

        private static string SceneState()
        {
            var text = new StringBuilder();
            text.AppendLine($"{RenderSettings.fog} {RenderSettings.fogMode} {RenderSettings.fogColor} {RenderSettings.fogDensity:R}");
            text.AppendLine($"{RenderSettings.ambientMode} {RenderSettings.ambientSkyColor} {RenderSettings.ambientEquatorColor} {RenderSettings.ambientGroundColor}");
            var block = new MaterialPropertyBlock();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    text.AppendLine($"{item.name} {item.gameObject.activeSelf} {item.localPosition:F4} {item.localRotation:F4} {item.localScale:F4}");
                    foreach (Light light in item.GetComponents<Light>())
                        text.AppendLine($"Light {light.enabled} {light.intensity:R} {light.color}");
                    foreach (ReflectionProbe probe in item.GetComponents<ReflectionProbe>())
                        text.AppendLine($"Probe {probe.intensity:R}");
                    foreach (Volume volume in item.GetComponents<Volume>())
                        text.AppendLine($"Volume {volume.weight:R}");
                    foreach (ParticleSystem particles in item.GetComponents<ParticleSystem>())
                        text.AppendLine($"Particle {particles.useAutoRandomSeed} {(particles.useAutoRandomSeed ? 0 : particles.randomSeed)}");
                    foreach (Renderer renderer in item.GetComponents<Renderer>())
                    {
                        block.Clear();
                        renderer.GetPropertyBlock(block);
                        text.AppendLine($"Renderer {renderer.enabled} {renderer.HasPropertyBlock()} {block.GetColor("_EmissionColor")}");
                    }
                }
            return text.ToString();
        }

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(stream));
        }

        private void RecordOutputFolder(string message, string trace, LogType type)
        {
            if (message.StartsWith(OutputPrefix, StringComparison.Ordinal))
                outputFolders.Add(message.Substring(OutputPrefix.Length));
        }
    }
}
