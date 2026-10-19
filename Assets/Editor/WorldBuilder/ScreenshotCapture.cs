using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Captures the saved Environment scene, then reloads the previous scene setup without saving.
    /// Batch: -batchmode -force-d3d11 -quit -executeMethod Blackout.Editor.ScreenshotCapture.CaptureScreenshots
    /// Requires graphics; do not use -nographics. Owner: World Builder.</summary>
    public static class ScreenshotCapture
    {
        public const int Width = 1920;
        public const int Height = 1080;
        private const float EyeHeight = 1.65f;

        private readonly struct Shot
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 Euler;
            public readonly bool Blackout;

            public Shot(string name, Vector3 position, Vector3 euler, bool blackout = false)
            {
                Name = name;
                Position = position;
                Euler = euler;
                Blackout = blackout;
            }
        }

        // Metres, +Z north. Both generator shots deliberately share the same pose.
        private static Shot[] Viewpoints()
        {
            float forestY = IslandHeightField.Sample(4f, -44f) + EyeHeight;
            var generatorEye = new Vector3(-4.5f, 2.5f + EyeHeight, 106f);
            return new[]
            {
                new Shot("01_Beach", new Vector3(0f, 1.3f + EyeHeight, -121f), Vector3.zero),
                new Shot("02_Forest", new Vector3(4f, forestY, -44f), new Vector3(0f, -47.3f, 0f)),
                new Shot("03_BunkerEntrance", new Vector3(0f, 10f + EyeHeight, 33f), Vector3.zero),
                new Shot("04_Corridor", new Vector3(0f, 2.5f + EyeHeight, 85f), Vector3.zero),
                new Shot("05_GeneratorRoom_powered", generatorEye, new Vector3(0f, 270f, 0f)),
                new Shot("07_Lab", new Vector3(0f, 2.5f + EyeHeight, 126f), Vector3.zero),
                // Last, so the temporary blackout cannot affect any powered view.
                new Shot("06_GeneratorRoom_blackout", generatorEye, new Vector3(0f, 270f, 0f), true),
            };
        }

        [MenuItem("Blackout/World/Capture Screenshots")]
        public static void CaptureScreenshots()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Capture Screenshots requires Edit mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Screenshot capture needs graphics. Use -force-d3d11, without -nographics.");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                throw new InvalidOperationException("Screenshot capture requires the project's URP pipeline.");

            // Never silently discard hand edits or an untitled scene containing the user's work.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene open = SceneManager.GetSceneAt(i);
                if (open.isDirty || (!Application.isBatchMode && string.IsNullOrEmpty(open.path)))
                    throw new InvalidOperationException("Save or close all unsaved scenes before Capture Screenshots.");
            }

            string folder = CreateOutputFolder();
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            var manifest = new StringBuilder("Blackout Island: The Cure - Environment screenshots\n");
            manifest.AppendLine($"Unity {Application.unityVersion}; {SystemInfo.graphicsDeviceType}; {Width}x{Height}; FOV 72; saved quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            manifest.AppendLine("Environment only: gameplay prefabs, player, HUD and agents are in other owners' scenes.");
            manifest.AppendLine("Fixed poses and particle seeds; GPU output and time-based shader/grain pixels may vary.");
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
                var cameraObject = new GameObject("W12_TemporaryCaptureCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = 72f;
                camera.aspect = (float)Width / Height;
                camera.nearClipPlane = 0.08f;
                camera.farClipPlane = 700f;
                camera.allowHDR = true;
                camera.clearFlags = CameraClearFlags.Skybox;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.volumeLayerMask = ~0;
                data.volumeTrigger = camera.transform;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

                // These are loaded scene instances only. No particle prefab or material asset is changed.
                uint seed = 1200;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                        particles.useAutoRandomSeed = false;
                        particles.randomSeed = ++seed;
                        particles.Simulate(4f, false, true, true);
                        particles.Pause(false);
                    }

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false, false);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                foreach (Shot shot in Viewpoints())
                {
                    camera.transform.SetPositionAndRotation(shot.Position, Quaternion.Euler(shot.Euler));
                    if (shot.Blackout)
                        manifest.AppendLine(ScreenshotLightingPreview.ApplyBlackout(scene));
                    camera.backgroundColor = RenderSettings.fogColor;
                    // First submission initialises camera/volume resources; the second is written to disk.
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    pixels.Apply(false, false);
                    string path = Path.Combine(folder, shot.Name + ".png");
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    manifest.AppendLine($"{shot.Name}: eye {shot.Position.ToString("F3")}, Euler {shot.Euler.ToString("F1")}");
                    Debug.Log("[World] Screenshot: " + path);
                }
                File.WriteAllText(Path.Combine(folder, "capture.txt"), manifest.ToString());
                Debug.Log("[World] Captured 7 screenshots: " + folder);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                // Force unloading even when Environment was already open: RestoreSceneManagerSetup alone
                // can retain the loaded instance. Closing without saving discards all preview changes.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path))
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        private static string CreateOutputFolder()
        {
            string folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BlackoutIsland-Screenshots",
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (folder.StartsWith(repo, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The temporary folder is inside the repo; configure TEMP outside it.");
            Directory.CreateDirectory(folder);
            Debug.Log("[World] Screenshot output folder: " + folder);
            return folder;
        }
    }
}
