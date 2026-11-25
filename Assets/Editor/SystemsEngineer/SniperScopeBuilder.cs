// Owner: Systems Engineer. SC1: only manages the scope component, scope HUD and Aim action.
using System;
using System.IO;
using Blackout.Hud;
using Blackout.Player;
using Blackout.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blackout.Editor
{
    public static class SniperScopeBuilder
    {
        private const string ActionsPath = "Assets/Scripts/PlayerAndInteraction/Input/PlayerControls.inputactions";
        private const string CameraPath = "CameraRig/CameraPivot/PlayerCamera";

        [MenuItem("Blackout/Systems/Build Sniper Scope")]
        public static void Build()
        {
            AddAimAction();
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                Camera camera = player.transform.Find(CameraPath)?.GetComponent<Camera>();
                WeaponController weapons = player.GetComponentInChildren<WeaponController>(true);
                ViewmodelHands hands = player.GetComponentInChildren<ViewmodelHands>(true);
                Transform hud = player.transform.Find("HUD");
                Transform crosshair = hud != null ? hud.Find("Crosshair") : null;
                WeaponData rifle = AssetDatabase.LoadAssetAtPath<WeaponData>(PlayerWeaponsBuilder.RiflePath);
                if (camera == null || weapons == null || hands == null || crosshair == null || rifle == null)
                    throw new InvalidOperationException("Build the existing Player, weapon visuals and HUD before the sniper scope.");

                Transform existing = hud.Find("SniperScope");
                GameObject overlay = existing != null ? existing.gameObject
                    : new GameObject("SniperScope", typeof(RectTransform), typeof(ScopeOverlay));
                if (existing == null)
                {
                    overlay.transform.SetParent(hud, false);
                    overlay.transform.SetAsFirstSibling(); // Health/messages stay readable above the scope mask.
                }
                var rect = (RectTransform)overlay.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                overlay.layer = hud.gameObject.layer;
                GetOrAdd<CanvasRenderer>(overlay);
                GetOrAdd<ScopeOverlay>(overlay).raycastTarget = false;
                overlay.SetActive(false);
                CanvasGroup visibility = GetOrAdd<CanvasGroup>(crosshair.gameObject);
                visibility.alpha = 1f;

                SniperScope scope = GetOrAdd<SniperScope>(player);
                PlayerPrefabBuilder.Assign(scope, "input", player.GetComponent<PlayerInputReader>());
                PlayerPrefabBuilder.Assign(scope, "look", player.GetComponent<PlayerLook>());
                PlayerPrefabBuilder.Assign(scope, "motor", player.GetComponent<PlayerMotor>());
                PlayerPrefabBuilder.Assign(scope, "weapons", weapons);
                PlayerPrefabBuilder.Assign(scope, "rifle", rifle);
                PlayerPrefabBuilder.Assign(scope, "playerCamera", camera);
                PlayerPrefabBuilder.Assign(scope, "hands", hands);
                PlayerPrefabBuilder.Assign(scope, "overlay", overlay);
                PlayerPrefabBuilder.Assign(scope, "crosshair", visibility);
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Blackout] Sniper hold-to-scope wired to the existing Player prefab and Aim action (SC1).");
        }

        private static void AddAimAction()
        {
            // InputActionAsset's JSON API preserves existing action/binding IDs and the importer's GUID.
            var actions = InputActionAsset.FromJson(File.ReadAllText(ActionsPath));
            try
            {
                InputActionMap map = actions.FindActionMap("Player", true);
                InputAction aim = map.FindAction("Aim");
                bool changed = false;
                if (aim == null) { aim = map.AddAction("Aim", InputActionType.Button); aim.expectedControlType = "Button"; changed = true; }
                bool bound = false;
                foreach (InputBinding binding in aim.bindings)
                    if (binding.path == "<Mouse>/rightButton") bound = true;
                if (!bound) { aim.AddBinding("<Mouse>/rightButton", groups: "Keyboard&Mouse"); changed = true; }
                if (changed)
                {
                    File.WriteAllText(ActionsPath, actions.ToJson());
                    AssetDatabase.ImportAsset(ActionsPath, ImportAssetOptions.ForceSynchronousImport);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(actions); }
        }

        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        /// <summary>Batch-only static review of the actual Player camera/HUD; never saves scenes or assets.</summary>
        public static void RenderPreview()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run the scope preview in a separate batch, with graphics enabled.");
            string folder = Path.Combine(Path.GetTempPath(), "blackout-SC1-preview");
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath));
            Camera camera = player.transform.Find(CameraPath).GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.65f, 0f), Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.22f, 0.27f);
            var canvas = player.transform.Find("HUD").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.3f;
            Transform holder = camera.transform.Find("WeaponHolder");
            foreach (string weapon in new[] { "SMG", "Knife", "Rifle" }) holder.Find(weapon).gameObject.SetActive(weapon == "Rifle");
            ViewmodelHands hands = holder.GetComponent<ViewmodelHands>();
            hands.Collect();
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(0.7f, 0.55f, 0.28f));
            for (int i = -1; i <= 1; i++)
            {
                var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.transform.position = new Vector3(i * 1.3f, 1.65f, 12f);
                target.transform.localScale = new Vector3(0.6f, 1.4f, 0.6f);
                target.GetComponent<Renderer>().sharedMaterial = material;
            }
            var texture = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture;
                foreach (bool scoped in new[] { false, true })
                {
                    camera.fieldOfView = scoped ? 22f : 72f;
                    hands.SetScopeHidden(scoped);
                    player.transform.Find("HUD/SniperScope").gameObject.SetActive(scoped);
                    player.transform.Find("HUD/Crosshair").GetComponent<CanvasGroup>().alpha = scoped ? 0f : 1f;
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture.active = texture;
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(folder, scoped ? "scoped.png" : "normal.png"), image.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                UnityEngine.Object.DestroyImmediate(material);
            }
            Debug.Log("[Blackout] Static sniper scope previews: " + folder);
        }
    }
}
