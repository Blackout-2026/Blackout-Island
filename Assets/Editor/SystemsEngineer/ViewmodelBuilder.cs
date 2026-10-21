using System;
using System.IO;
using Blackout.Player;
using Blackout.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Step VM1, the Systems Engineer's part: the player's two hands and the knife swing. Adds ViewmodelHands to the WeaponHolder (the arms of
    /// every weapon are hidden while sprinting) and KnifeSwing to the Knife (the knife and its hand swing when the player attacks) in the Player
    /// prefab. The models themselves (SM_Weapon_Smg, _Knife and _Sniper, with the downloaded arms) and the framing in the holder are the Core
    /// Developer's: Blackout > Art > Use All Weapon Visuals In Player. Re-runnable: a component that is there is kept and only re-wired.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.ViewmodelBuilder.Build
    /// </summary>
    public static class ViewmodelBuilder
    {
        public const string HolderPath = "CameraRig/CameraPivot/PlayerCamera/WeaponHolder";
        public const string CameraPath = "CameraRig/CameraPivot/PlayerCamera";

        [MenuItem("Blackout/Systems/Build Viewmodel Hands And Knife Swing")]
        public static void Build()
        {
            if (AssetDatabase.LoadMainAssetAtPath(PlayerPrefabBuilder.PrefabPath) == null)
            {
                Debug.LogError("[Blackout] " + PlayerPrefabBuilder.PrefabPath + " is missing; run Build Player Prefab and Build Player Weapons first.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                Transform holder = root.transform.Find(HolderPath);
                Transform knife = holder != null ? holder.Find("Knife") : null;
                Transform hand = knife != null ? knife.Find("Hand") : null;
                if (hand == null)
                {
                    Debug.LogError("[Blackout] The Player prefab has no " + HolderPath + "/Knife/Hand; run Build Player Weapons first.");
                    return;
                }

                var hands = GetOrAdd<ViewmodelHands>(holder.gameObject);
                PlayerPrefabBuilder.Assign(hands, "motor", root.GetComponent<PlayerMotor>());

                var swing = GetOrAdd<KnifeSwing>(knife.gameObject);
                PlayerPrefabBuilder.Assign(swing, "weapon", knife.GetComponent<MeleeWeapon>());
                PlayerPrefabBuilder.Assign(swing, "hand", hand);
                var swingFields = new SerializedObject(swing);
                swingFields.FindProperty("pivot").vector3Value = KnifeSwing.DefaultPivot;
                swingFields.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Blackout] ViewmodelHands and KnifeSwing are in " + PlayerPrefabBuilder.PrefabPath + ".");
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        /// <summary>
        /// Review pictures of what the player sees, from the Player prefab's own camera: each gun, the knife at rest and at four moments of its swing,
        /// and the SMG while sprinting (no hands). Needs a graphics device, so run it without -nographics:
        /// Unity.exe -batchmode -quit -projectPath "..." -executeMethod Blackout.Editor.ViewmodelBuilder.RenderPreview -previewOut "%TEMP%/viewmodel-preview"
        /// </summary>
        public static void RenderPreview()
        {
            string folder = Argument("-previewOut") ?? Path.Combine(Path.GetTempPath(), "blackout-viewmodel-preview");
            Directory.CreateDirectory(folder);
            // A scene of its own (an empty one, not a prefab's preview scene, which the render pipeline does not draw).
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); // the swing regroups parts, which an instance forbids in Edit mode
            var temporary = new System.Collections.Generic.List<GameObject>();
            RenderTexture target = null;
            Texture2D picture = null;
            try
            {
                Transform holder = root.transform.Find(HolderPath);
                Camera camera = root.transform.Find(CameraPath).GetComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.30f, 0.33f, 0.37f);
                camera.aspect = 16f / 9f;
                foreach (var (rotation, intensity) in new[] { (Quaternion.Euler(50f, -30f, 0f), 1.1f), (Quaternion.Euler(40f, 150f, 0f), 0.7f) })
                {
                    var light = new GameObject("PreviewLight").AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = intensity;
                    light.shadows = LightShadows.None;
                    light.transform.rotation = rotation;
                    temporary.Add(light.gameObject);
                }

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);

                const int width = 1280, height = 720;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                camera.targetTexture = target;

                var hands = holder.GetComponent<ViewmodelHands>();
                var swing = holder.Find("Knife").GetComponent<KnifeSwing>();
                swing.Prepare();
                hands.Collect();
                float hit = 0.11f;
                var shots = new (string name, string weapon, float swingTime, bool sprint)[]
                {
                    ("smg", "SMG", -1f, false),
                    ("rifle", "Rifle", -1f, false),
                    ("knife_rest", "Knife", 0f, false),
                    ("knife_windup", "Knife", 0.35f * hit, false),
                    ("knife_hit", "Knife", 1f * hit, false),
                    ("knife_follow", "Knife", 1.5f * hit, false),
                    ("knife_back", "Knife", 2.4f * hit, false),
                    ("smg_sprint", "SMG", -1f, true),
                    ("knife_sprint", "Knife", 0f, true),
                };
                foreach (var shot in shots)
                {
                    foreach (Transform child in holder)
                    {
                        if (child.name == "SMG" || child.name == "Rifle" || child.name == "Knife")
                        {
                            child.gameObject.SetActive(child.name == shot.weapon);
                        }
                    }

                    hands.Apply(!shot.sprint);
                    swing.ApplyAt(Mathf.Max(shot.swingTime, 0f));
                    camera.Render();
                    RenderTexture.active = target;
                    picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    picture.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(folder, shot.name + ".png"), picture.EncodeToPNG());
                }

                camera.targetTexture = null;
            }
            finally
            {
                RenderTexture.active = null;
                if (picture != null)
                {
                    UnityEngine.Object.DestroyImmediate(picture);
                }

                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }

                foreach (GameObject go in temporary)
                {
                    if (go != null)
                    {
                        UnityEngine.Object.DestroyImmediate(go);
                    }
                }

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            Debug.Log("[Blackout] Viewmodel preview pictures: " + folder);
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
