using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Renders review pictures of a character's visual prefab from several sides, in Unity itself (URP materials, real skinning), the counterpart of
    /// Art/Blender/Scripts/preview_humans.py. The pictures go to a folder outside the project. Needs a graphics device, so run it without -nographics:
    /// Unity.exe -batchmode -quit -projectPath "..." -executeMethod Blackout.Editor.CharacterPreviewRenderer.RenderFromCommandLine
    ///           -previewPrefab Assets/Prefabs/Models/Char_Patrol_Visual.prefab -previewOut "%TEMP%/preview"
    /// Or from the menu: Blackout > Art > Render Patrol Preview (writes to the temporary folder and prints where).
    /// </summary>
    public static class CharacterPreviewRenderer
    {
        private const int Size = 800;

        // name, camera position, look-at point, field of view. The characters stand at the origin facing +Z.
        private static readonly (string name, Vector3 position, Vector3 target, float fov)[] Views =
        {
            ("front", new Vector3(0f, 1.1f, 3.6f), new Vector3(0f, 0.95f, 0f), 30f),
            ("front34", new Vector3(2.0f, 1.45f, 2.8f), new Vector3(0f, 0.95f, 0.1f), 34f),
            ("side", new Vector3(3.4f, 1.15f, 0.3f), new Vector3(0f, 0.95f, 0.2f), 30f),
            ("back", new Vector3(0f, 1.2f, -3.6f), new Vector3(0f, 0.95f, 0f), 30f),
            ("head", new Vector3(0.45f, 1.8f, 1.0f), new Vector3(0f, 1.72f, 0f), 24f),
            ("rifle", new Vector3(0.9f, 1.5f, 1.5f), new Vector3(0.05f, 1.3f, 0.3f), 30f),
        };

        [MenuItem("Blackout/Art/Render Patrol Preview")]
        public static void RenderPatrolFromMenu()
        {
            string folder = Path.Combine(Path.GetTempPath(), "blackout-preview");
            Render("Assets/Prefabs/Models/Char_Patrol_Visual.prefab", folder);
            Debug.Log($"[CharacterPreview] pictures are in {folder}");
        }

        public static void RenderFromCommandLine()
        {
            string prefab = Argument("-previewPrefab") ?? "Assets/Prefabs/Models/Char_Patrol_Visual.prefab";
            string folder = Argument("-previewOut") ?? Path.Combine(Path.GetTempPath(), "blackout-preview");
            Render(prefab, folder);
        }

        public static void Render(string prefabPath, string folder)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null)
            {
                Debug.LogError($"[CharacterPreview] {prefabPath} not found.");
                return;
            }

            Directory.CreateDirectory(folder);
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.6f, 0.68f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            var key = new GameObject("Key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.shadows = LightShadows.None;
            key.transform.rotation = Quaternion.Euler(45f, -150f, 0f);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.5f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(25f, 40f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.55f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset); // in the active scene, and removed again below
            foreach (SkinnedMeshRenderer skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinned.updateWhenOffscreen = true;
            }

            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var picture = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            foreach (var view in Views)
            {
                camera.transform.position = view.position;
                camera.transform.rotation = Quaternion.LookRotation(view.target - view.position, Vector3.up);
                camera.fieldOfView = view.fov;
                camera.Render();
                RenderTexture.active = target;
                picture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                picture.Apply();
                RenderTexture.active = null;
                string file = Path.Combine(folder, view.name + ".png");
                File.WriteAllBytes(file, picture.EncodeToPNG());
                Debug.Log($"[CharacterPreview] {file}");
            }

            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(picture);
            UnityEngine.Object.DestroyImmediate(instance);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(key.gameObject);
            UnityEngine.Object.DestroyImmediate(fill.gameObject);
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
