using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Removes the shared white fallback's alpha channel, leaving smoothness to the terrain layer.
    /// Does not change lighting, terrain shape/painting, vegetation, navigation or supplied textures.</summary>
    public static class TerrainColourCorrection
    {
        private const int Size = 4;
        internal static Texture2D CreateWhiteTexture()
        {
            // RGB has no alpha: Terrain Lit then uses the layer's smoothness slider. An RGBA white texture
            // supplies alpha=1 as smoothness, making soil mirror the blue sky despite a rough layer setting.
            var texture = new Texture2D(Size, Size, TextureFormat.RGB24, false, false) { name = "TerrainLayerWhite" };
            FillWhite(texture);
            return texture;
        }

        private static void FillWhite(Texture2D texture)
        {
            var pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply(false, false);
        }

        internal static bool RemoveFallbackAlpha(Texture2D texture)
        {
            if (texture.format == TextureFormat.RGB24) return false;
            if (texture.width != Size || texture.height != Size || texture.format != TextureFormat.RGBA32
                || !texture.isReadable || texture.GetPixels32().Any(pixel => pixel.r != 255 || pixel.g != 255 || pixel.b != 255))
                throw new InvalidOperationException("The generated white fallback was customised; inspect it before correcting it.");
            if (!texture.Reinitialize(Size, Size, TextureFormat.RGB24, false))
                throw new InvalidOperationException("Could not convert the fallback to RGB24.");
            FillWhite(texture);
            return true;
        }

        private static UnityEngine.Terrain FindTerrain(Scene scene)
        {
            Transform island = WorldSceneSkeleton.FindRoot(scene, "Terrain")?.transform.Find("IslandTerrain");
            var terrain = island != null ? island.GetComponent<UnityEngine.Terrain>() : null;
            if (terrain == null || terrain.terrainData == null) throw new InvalidOperationException("Open the generated Environment terrain first.");
            return terrain;
        }

        [MenuItem("Blackout/World/Correct Terrain Colours")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Correct terrain colours outside Play mode.");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainPainter.WhiteTexturePath);
            if (texture == null) throw new InvalidOperationException("Paint Terrain And Forest first to create the white fallback.");
            bool changed = RemoveFallbackAlpha(texture);
            if (changed)
            {
                EditorUtility.SetDirty(texture);
                AssetDatabase.SaveAssetIfDirty(texture);
            }
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (scene.IsValid() && scene.isLoaded) FindTerrain(scene).Flush();
            Debug.Log("[World] Terrain colours: white fallback RGB24, no alpha; " + (changed ? "converted in place" : "already corrected")
                + "; existing layer colours/smoothness and lighting retained.");
        }

        public static void ApplyAndVerify()
        {
            string[] protectedPaths = new[] { WorldSceneSkeleton.ScenePath, "Assets/Environment/Terrain/IslandTerrainData.asset",
                "Assets/Environment/NavMesh/EnvironmentNavMesh.asset", "Assets/Scenes/Interactables.unity", "Assets/Scenes/Agents.unity",
                TerrainPainter.WhiteTexturePath + ".meta" }.Concat(TerrainSplat.Layers.SelectMany(surface =>
                    new[] { TerrainPainter.LayerPath(surface), TerrainPainter.LayerPath(surface) + ".meta" })).ToArray();
            byte[][] protectedBytes = protectedPaths.Select(File.ReadAllBytes).ToArray();
            Apply();
            byte[] first = File.ReadAllBytes(TerrainPainter.WhiteTexturePath);
            Apply();
            if (!first.SequenceEqual(File.ReadAllBytes(TerrainPainter.WhiteTexturePath))) throw new InvalidOperationException("Fallback changed on repeat.");
            for (int i = 0; i < protectedPaths.Length; i++)
                if (!protectedBytes[i].SequenceEqual(File.ReadAllBytes(protectedPaths[i]))) throw new InvalidOperationException("Protected file changed: " + protectedPaths[i]);
            Debug.Log($"[World] Terrain colour verification: repeat texture identical; {protectedPaths.Length} protected scene/data/layer/meta files unchanged.");
        }

        [MenuItem("Blackout/World/Preview Terrain Colour Correction")]
        public static void CaptureComparison()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Preview requires graphics; omit -nographics.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty || (!Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)))
                    throw new InvalidOperationException("Save or close unsaved scenes before previewing.");
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BlackoutIsland-TerrainColour",
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            if (folder.StartsWith(repo, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Preview output must be outside the repo.");
            Directory.CreateDirectory(folder);
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            var temporary = new List<UnityEngine.Object>();
            RenderTexture previousTarget = RenderTexture.active;
            RenderTexture target = null;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
                UnityEngine.Terrain terrain = FindTerrain(scene);
                TerrainData original = terrain.terrainData;
                TerrainData clone = UnityEngine.Object.Instantiate(original);
                temporary.Add(clone);
                var cameraObject = new GameObject("TemporaryTerrainColourCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = 72f;
                camera.nearClipPlane = 0.08f;
                camera.farClipPlane = 700f;
                camera.allowHDR = true;
                camera.clearFlags = CameraClearFlags.Skybox;
                var additional = camera.GetUniversalAdditionalCameraData();
                additional.renderPostProcessing = true;
                additional.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                additional.volumeTrigger = camera.transform;
                additional.volumeLayerMask = ~0;
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                target.Create();
                var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                temporary.Add(pixels);
                camera.aspect = 1920f / 1080f;
                Capture(camera, target, pixels, folder, "Before");
                TerrainLayer[] layers = original.terrainLayers;
                if (layers.Length != TerrainSplat.Layers.Length) throw new InvalidOperationException("Expected four terrain layers.");
                Texture2D white = CreateWhiteTexture();
                temporary.Add(white);
                for (int i = 0; i < layers.Length; i++)
                {
                    string surface = TerrainSplat.Layers[i];
                    TerrainLayer layer = UnityEngine.Object.Instantiate(layers[i]);
                    temporary.Add(layer);
                    Debug.Log($"[World] Preview {surface}: format={layers[i].diffuseTexture.format}, smoothness={layers[i].smoothness:F2}, remap={layers[i].diffuseRemapMax}");
                    if (AssetDatabase.GetAssetPath(layers[i].diffuseTexture) == TerrainPainter.WhiteTexturePath)
                        layer.diffuseTexture = white; // Keep the original palette remap and roughness values.
                    layers[i] = layer;
                }
                clone.terrainLayers = layers;
                terrain.terrainData = clone;
                clone.SetBaseMapDirty();
                terrain.Flush();
                Capture(camera, target, pixels, folder, "After");
                Debug.Log("[World] Terrain colour comparison: " + folder + "; temporary copies only, no assets or scenes saved; lighting identical.");
            }
            finally
            {
                RenderTexture.active = previousTarget;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i] != null) UnityEngine.Object.DestroyImmediate(temporary[i]);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path)) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        private static void Capture(Camera camera, RenderTexture target, Texture2D pixels, string folder, string phase)
        {
            Vector3[] positions = { new Vector3(0f, 2.95f, -121f), new Vector3(4f, IslandHeightField.Sample(4f, -44f) + 1.65f, -44f) };
            float[] yaw = { 0f, -47.3f };
            string[] names = { "Beach", "Forest" };
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            for (int i = 0; i < positions.Length; i++)
            {
                camera.transform.SetPositionAndRotation(positions[i], Quaternion.Euler(0f, yaw[i], 0f));
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
                pixels.Apply(false, false);
                File.WriteAllBytes(Path.Combine(folder, phase + "_" + names[i] + ".png"), pixels.EncodeToPNG());
            }
        }
    }
}
