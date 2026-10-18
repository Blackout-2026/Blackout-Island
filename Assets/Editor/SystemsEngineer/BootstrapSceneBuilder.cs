using System.Collections.Generic;
using System.IO;
using Blackout.Hud;
using Blackout.Mission;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/Bootstrap.unity, the scene the game starts from (docs/reference/systems-engineer.md
    /// section 11): SceneBootstrap, MissionDirector, the Player (inactive until the level has loaded), the UI
    /// EventSystem, and a canvas with the loading screen and the Mission Complete screen. Then puts the scenes in
    /// Build Settings: Bootstrap first, then Environment, Interactables and Agents (if it exists yet).
    /// Re-runnable: a root object that already exists (same name) is kept as it is, only its empty references are
    /// filled; other Build Settings entries are kept after ours.
    /// </summary>
    public static class BootstrapSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap.unity";

        private static readonly string[] LevelScenes =
        {
            "Assets/Scenes/Environment.unity",
            "Assets/Scenes/Interactables.unity",
            "Assets/Scenes/Agents.unity",
        };

        private const string Events = EventChannelAssetBuilder.Folder;
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Color Backdrop = new Color(0.02f, 0.03f, 0.04f, 0.82f);
        private static readonly Color ButtonText = new Color(0.05f, 0.06f, 0.07f, 1f);

        [MenuItem("Blackout/Systems/Build Bootstrap Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = OpenOrCreateScene();

            // Loaded only after opening the scene: OpenScene(Single) unloads unused assets.
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogError("[Blackout] Missing Player.prefab; run Build Player Prefab first.");
                return;
            }

            int added = 0;
            GameObject ui = FindRoot(scene, "UI");
            if (ui == null)
            {
                ui = BuildCanvas(scene);
                added++;
            }

            GameObject director = FindOrCreate(scene, "MissionDirector", ref added, typeof(MissionDirector));
            GameObject player = FindRoot(scene, "Player");
            if (player == null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
                player.name = "Player";
                player.SetActive(false); // SceneBootstrap turns it on at PlayerStart once the ground exists
                added++;
            }

            GameObject bootstrap = FindOrCreate(scene, "Bootstrap", ref added, typeof(SceneBootstrap));
            if (Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(eventSystem, scene);
                added++;
            }

            Wire(bootstrap, director, player, ui);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Blackout] Build Bootstrap Scene: added {added} objects, existing ones kept. Saved {ScenePath}.");
            SetBuildScenes();
        }

        private static Scene OpenOrCreateScene()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ScenePath) != null)
            {
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Blackout] Created {ScenePath}.");
            return scene;
        }

        /// <summary>Fills the cross-references that are still empty; never replaces one set by hand.</summary>
        private static void Wire(GameObject bootstrap, GameObject director, GameObject player, GameObject ui)
        {
            var loader = bootstrap.GetComponent<SceneBootstrap>();
            var mission = director.GetComponent<MissionDirector>();
            var complete = ui.GetComponentInChildren<MissionCompleteScreen>(true);
            Transform loading = ui.transform.Find("LoadingScreen");

            FillIfEmpty(loader, "player", player);
            FillIfEmpty(loader, "director", mission);
            FillIfEmpty(loader, "loadingScreen", loading != null ? loading.gameObject : null);

            FillIfEmpty(mission, "generatorDisabled", Channel<VoidEventChannel>("GeneratorDisabled"));
            FillIfEmpty(mission, "cureTaken", Channel<VoidEventChannel>("CureTaken"));
            FillIfEmpty(mission, "playerDied", Channel<VoidEventChannel>("PlayerDied"));
            FillIfEmpty(mission, "missionComplete", Channel<VoidEventChannel>("MissionComplete"));
            FillIfEmpty(mission, "objectiveChanged", Channel<StringEventChannel>("ObjectiveChanged"));
            FillIfEmpty(mission, "completeScreen", complete);
        }

        private static T Channel<T>(string name) where T : Object
        {
            var channel = AssetDatabase.LoadAssetAtPath<T>($"{Events}/{name}.asset");
            if (channel == null)
            {
                Debug.LogError($"[Blackout] Missing {name}.asset; run Create Event Channels first.");
            }

            return channel;
        }

        // ---- UI -----------------------------------------------------------------------------------------------

        /// <summary>
        /// UI (Canvas above the player's HUD)
        ///   LoadingScreen (black, "LOADING…")
        ///   MissionComplete (MissionCompleteScreen)
        ///     Root (backdrop, title, subtitle, panel with TIME / KILLS / DEATHS and RESTART)
        /// </summary>
        private static GameObject BuildCanvas(Scene scene)
        {
            var ui = new GameObject("UI", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(ui, scene);
            var canvas = ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // the HUD is 10
            var scaler = ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            ui.AddComponent<GraphicRaycaster>();

            RectTransform loading = Stretched(ui.transform, "LoadingScreen");
            Img(loading.gameObject, Color.black, true);
            Label(loading, "Text", "LOADING…", 22, HudColors.Dim, Vector2.zero, new Vector2(400f, 40f));
            loading.gameObject.SetActive(false); // SceneBootstrap shows it while loading

            RectTransform completeObject = Stretched(ui.transform, "MissionComplete");
            var complete = completeObject.gameObject.AddComponent<MissionCompleteScreen>();
            RectTransform root = Stretched(completeObject, "Root");
            Img(root.gameObject, Backdrop, true); // also blocks clicks to anything behind
            Label(root, "Title", "MISSION COMPLETE", 64, HudColors.Text, new Vector2(0f, 200f),
                new Vector2(1200f, 80f), FontStyle.Bold);
            Label(root, "Subtitle", "THE CURE IS OFF THE ISLAND", 18, HudColors.Accent, new Vector2(0f, 140f),
                new Vector2(1200f, 30f));

            RectTransform panel = Rect(root, "Panel", new Vector2(0f, -40f), new Vector2(440f, 280f));
            Img(panel.gameObject, HudColors.Panel, false);
            RectTransform line = Rect(panel, "AccentLine", Vector2.zero, Vector2.zero);
            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(0.5f, 1f);
            line.sizeDelta = new Vector2(0f, 2f);
            Img(line.gameObject, HudColors.Accent, false);

            Text time = StatRow(panel, "Time", "TIME", 90f);
            Text kills = StatRow(panel, "Kills", "KILLS", 50f);
            Text deaths = StatRow(panel, "Deaths", "DEATHS", 10f);

            RectTransform buttonRect = Rect(panel, "RestartButton", new Vector2(0f, -80f), new Vector2(220f, 52f));
            Image buttonImage = Img(buttonRect.gameObject, HudColors.Accent, true);
            var button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            Label(buttonRect, "Text", "RESTART", 20, ButtonText, Vector2.zero, new Vector2(220f, 52f), FontStyle.Bold);

            PlayerPrefabBuilder.Assign(complete, "root", root.gameObject);
            PlayerPrefabBuilder.Assign(complete, "timeValue", time);
            PlayerPrefabBuilder.Assign(complete, "killsValue", kills);
            PlayerPrefabBuilder.Assign(complete, "deathsValue", deaths);
            PlayerPrefabBuilder.Assign(complete, "restartButton", button);
            root.gameObject.SetActive(false);

            PlayerPrefabBuilder.SetLayerRecursively(ui.transform, LayerMask.NameToLayer("UI"));
            return ui;
        }

        /// <summary>A dim label on the left of the panel's centre and its value on the right, as in the prototype.</summary>
        private static Text StatRow(Transform panel, string name, string label, float y)
        {
            Text title = Label(panel, name + "Label", label, 15, HudColors.Dim, new Vector2(-90f, y), new Vector2(160f, 30f));
            title.alignment = TextAnchor.MiddleRight;
            Text value = Label(panel, name + "Value", "0", 20, HudColors.Text, new Vector2(90f, y), new Vector2(160f, 30f));
            value.alignment = TextAnchor.MiddleLeft;
            return value;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Stretched(Transform parent, string name)
        {
            RectTransform rect = Rect(parent, name, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image Img(GameObject target, Color colour, bool blocksClicks)
        {
            var image = target.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = blocksClicks;
            return image;
        }

        private static Text Label(Transform parent, string name, string text, int size, Color colour, Vector2 position,
            Vector2 rectSize, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = Rect(parent, name, position, rectSize);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = colour;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        // ---- scene and Build Settings helpers ------------------------------------------------------------------

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name)
                {
                    return go;
                }
            }

            return null;
        }

        private static GameObject FindOrCreate(Scene scene, string name, ref int added, System.Type component)
        {
            GameObject existing = FindRoot(scene, name);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(name, component);
            SceneManager.MoveGameObjectToScene(go, scene);
            added++;
            return go;
        }

        private static void FillIfEmpty(Object target, string field, Object value)
        {
            if (target == null || value == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null && property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Bootstrap first (the game starts there), then the level scenes that exist. Entries already in the list
        /// that are not ours stay, after ours, with their enabled state.
        /// </summary>
        private static void SetBuildScenes()
        {
            var ours = new List<string> { ScenePath };
            foreach (string path in LevelScenes)
            {
                if (File.Exists(path))
                {
                    ours.Add(path);
                }
                else
                {
                    Debug.LogWarning($"[Blackout] {path} does not exist yet; not added to Build Settings. " +
                                     "Run Build Bootstrap Scene again once it does.");
                }
            }

            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in ours)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (!ours.Contains(existing.path))
                {
                    scenes.Add(existing);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[Blackout] Build Settings: {string.Join(", ", ours)} (+{scenes.Count - ours.Count} kept).");
        }
    }
}
