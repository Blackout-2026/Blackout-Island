using System;
using System.IO;
using Blackout.Hud;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Adds health, respawn and the uGUI HUD to the Player prefab (step Y4): Health, CheckpointTracker and
    /// PlayerHealth on the root, the camera's shake/death links, and a screen-space HUD canvas laid out like the
    /// prototype (docs/reference/systems-engineer.md section 9). Its placeholder textures are generated here.
    /// Re-runnable: existing textures are kept, and a prefab that already has a PlayerHealth is not changed.
    /// Run Build Player Prefab and Build Player Weapons first.
    /// </summary>
    public static class PlayerHudBuilder
    {
        public const string HudFolder = PlayerPrefabBuilder.Folder + "/Hud";

        private const string HolderPath = "CameraRig/CameraPivot/PlayerCamera/WeaponHolder";
        private const string RigPath = "CameraRig";
        private const string Events = EventChannelAssetBuilder.Folder;

        // Prototype layout (ui/style.css), in pixels at the 1920 x 1080 reference resolution.
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private const float Margin = 34f;
        private const float EdgeTop = 30f;
        private const float SlotWidth = 104f;
        private const float SlotGap = 8f;
        private static readonly Color VignetteRed = new Color(150f / 255f, 0f, 0f, 0f);
        private static readonly Color AlertRed = new Color(190f / 255f, 20f / 255f, 10f / 255f, 0.85f);

        [MenuItem("Blackout/Systems/Build Player HUD")]
        public static void Build()
        {
            EnsureFolder(PlayerPrefabBuilder.Folder, "Hud");
            var textures = new Textures
            {
                Vignette = CreateTextureIfMissing("DamageVignette", 256, 256, VignetteAlpha),
                Arc = CreateTextureIfMissing("DamageArc", 256, 256, ArcAlpha),
                Ring = CreateTextureIfMissing("CrosshairRing", 64, 64, RingAlpha),
                Band = CreateTextureIfMissing("AlertBand", 256, 4, BandAlpha),
            };

            var channels = new Channels
            {
                PlayerDied = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(Events + "/PlayerDied.asset"),
                PlayerRespawned = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(Events + "/PlayerRespawned.asset"),
                CureTaken = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(Events + "/CureTaken.asset"),
                Objective = AssetDatabase.LoadAssetAtPath<StringEventChannel>(Events + "/ObjectiveChanged.asset"),
            };
            WeaponData[] weapons =
            {
                AssetDatabase.LoadAssetAtPath<WeaponData>(PlayerWeaponsBuilder.SmgPath),
                AssetDatabase.LoadAssetAtPath<WeaponData>(PlayerWeaponsBuilder.RiflePath),
                AssetDatabase.LoadAssetAtPath<WeaponData>(PlayerWeaponsBuilder.KnifePath),
            };
            if (channels.PlayerDied == null || channels.PlayerRespawned == null || channels.CureTaken == null
                || channels.Objective == null || Array.IndexOf(weapons, null) >= 0)
            {
                Debug.LogError("[Blackout] Missing event channels or weapon assets; run Create Event Channels and " +
                               "Build Player Weapons first. HUD not built.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                if (root.GetComponent<PlayerHealth>() != null)
                {
                    Debug.Log("[Blackout] Player prefab already has a PlayerHealth; left unchanged.");
                    return;
                }

                var controller = root.transform.Find(HolderPath)?.GetComponent<WeaponController>();
                if (controller == null)
                {
                    Debug.LogError("[Blackout] Player prefab has no WeaponController; run Build Player Weapons first.");
                    return;
                }

                PlayerHealth playerHealth = AddHealth(root, controller.gameObject, channels);
                BuildHud(root, playerHealth, controller, weapons, textures, channels);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabBuilder.PrefabPath, out bool saved);
                Debug.Log(saved
                    ? $"[Blackout] Added health, respawn and the HUD to {PlayerPrefabBuilder.PrefabPath}."
                    : $"[Blackout] Could not save {PlayerPrefabBuilder.PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private struct Textures
        {
            public Texture2D Vignette;
            public Texture2D Arc;
            public Texture2D Ring;
            public Texture2D Band;
        }

        private struct Channels
        {
            public VoidEventChannel PlayerDied;
            public VoidEventChannel PlayerRespawned;
            public VoidEventChannel CureTaken;
            public StringEventChannel Objective;
        }

        // ---- health and respawn -----------------------------------------------------------------------------

        private static PlayerHealth AddHealth(GameObject root, GameObject weaponHolder, Channels channels)
        {
            if (root.GetComponent<Health>() == null)
            {
                root.AddComponent<Health>(); // 100 HP by default
            }

            var checkpoints = root.AddComponent<CheckpointTracker>();
            PlayerPrefabBuilder.Assign(checkpoints, "cureTaken", channels.CureTaken);

            var playerHealth = root.AddComponent<PlayerHealth>();
            PlayerPrefabBuilder.Assign(playerHealth, "input", root.GetComponent<PlayerInputReader>());
            PlayerPrefabBuilder.Assign(playerHealth, "motor", root.GetComponent<PlayerMotor>());
            PlayerPrefabBuilder.Assign(playerHealth, "look", root.GetComponent<PlayerLook>());
            PlayerPrefabBuilder.Assign(playerHealth, "checkpoints", checkpoints);
            PlayerPrefabBuilder.Assign(playerHealth, "weaponHolder", weaponHolder);
            PlayerPrefabBuilder.Assign(playerHealth, "playerDied", channels.PlayerDied);
            PlayerPrefabBuilder.Assign(playerHealth, "playerRespawned", channels.PlayerRespawned);

            var effects = root.transform.Find(RigPath)?.GetComponent<PlayerCameraEffects>();
            if (effects != null)
            {
                PlayerPrefabBuilder.Assign(effects, "health", root.GetComponent<Health>());
                PlayerPrefabBuilder.Assign(effects, "playerHealth", playerHealth);
            }

            return playerHealth;
        }

        // ---- HUD ------------------------------------------------------------------------------------------------

        /// <summary>
        /// HUD (Canvas + every HUD component)
        ///   DamageVignette, DamageArcs, Crosshair, HitMarker, Health, Weapons, Objective, Checkpoint,
        ///   Prompt, AlertBanner, AlertLabel, Toast
        /// </summary>
        private static void BuildHud(GameObject root, PlayerHealth playerHealth, WeaponController controller,
            WeaponData[] weapons, Textures textures, Channels channels)
        {
            var hud = new GameObject("HUD", typeof(RectTransform));
            hud.transform.SetParent(root.transform, false);
            var canvas = hud.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = hud.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            Transform h = hud.transform;
            Health health = root.GetComponent<Health>();

            // Damage feedback sits behind everything else.
            RawImage vignette = Raw(h, "DamageVignette", textures.Vignette, VignetteRed);
            Stretch(vignette.rectTransform);
            RectTransform arcsRoot = Rect(h, "DamageArcs", Center, Center, Vector2.zero, Vector2.zero);
            var arcs = new RawImage[4];
            for (int i = 0; i < arcs.Length; i++)
            {
                arcs[i] = Raw(arcsRoot, "Arc " + i, textures.Arc, Transparent(HudColors.Danger));
                SetRect(arcs[i].rectTransform, Center, Center, Vector2.zero, new Vector2(256f, 256f));
            }

            var damage = hud.AddComponent<DamageIndicator>();
            PlayerPrefabBuilder.Assign(damage, "health", health);
            PlayerPrefabBuilder.Assign(damage, "playerHealth", playerHealth);
            PlayerPrefabBuilder.Assign(damage, "player", root.transform);
            PlayerPrefabBuilder.Assign(damage, "vignette", vignette);
            AssignArray(damage, "arcs", arcs);

            BuildCrosshair(hud, playerHealth, controller, textures.Ring);
            BuildHitMarker(hud, controller);
            BuildHealthBar(hud, health);
            BuildWeaponSlots(hud, controller, weapons);
            BuildObjective(hud, channels.Objective);
            BuildPrompt(hud, playerHealth);
            BuildMessages(hud, root, playerHealth, channels.CureTaken, textures.Band);

            PlayerPrefabBuilder.SetLayerRecursively(h, LayerMask.NameToLayer("UI"));
        }

        private static void BuildCrosshair(GameObject hud, PlayerHealth playerHealth, WeaponController controller,
            Texture2D ring)
        {
            RectTransform root = Rect(hud.transform, "Crosshair", Center, Center, Vector2.zero, Vector2.zero);
            Img(root, "Dot", Color.white, Vector2.zero, new Vector2(3f, 3f));

            // Four 2 x 8 px lines with a 6 px gap (prototype .xhair).
            RectTransform lines = Rect(root, "Lines", Center, Center, Vector2.zero, Vector2.zero);
            Img(lines, "Top", HudColors.Text, new Vector2(0f, 10f), new Vector2(2f, 8f));
            Img(lines, "Bottom", HudColors.Text, new Vector2(0f, -10f), new Vector2(2f, 8f));
            Img(lines, "Left", HudColors.Text, new Vector2(-10f, 0f), new Vector2(8f, 2f));
            Img(lines, "Right", HudColors.Text, new Vector2(10f, 0f), new Vector2(8f, 2f));

            RawImage ringImage = Raw(root, "Ring", ring, HudColors.Text);
            SetRect(ringImage.rectTransform, Center, Center, Vector2.zero, new Vector2(22f, 22f));

            var crosshair = hud.AddComponent<Crosshair>();
            PlayerPrefabBuilder.Assign(crosshair, "weapons", controller);
            PlayerPrefabBuilder.Assign(crosshair, "playerHealth", playerHealth);
            PlayerPrefabBuilder.Assign(crosshair, "root", root.gameObject);
            PlayerPrefabBuilder.Assign(crosshair, "lines", lines.gameObject);
            PlayerPrefabBuilder.Assign(crosshair, "ring", ringImage.gameObject);
        }

        private static void BuildHitMarker(GameObject hud, WeaponController controller)
        {
            RectTransform root = Rect(hud.transform, "HitMarker", Center, Center, Vector2.zero, Vector2.zero);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            // An X of four 10 x 2 px lines, each on a diagonal 9 px out from the centre.
            var lines = new Graphic[4];
            lines[0] = Img(root, "TopRight", Color.white, new Vector2(9f, 9f), new Vector2(10f, 2f), 45f);
            lines[1] = Img(root, "TopLeft", Color.white, new Vector2(-9f, 9f), new Vector2(10f, 2f), -45f);
            lines[2] = Img(root, "BottomLeft", Color.white, new Vector2(-9f, -9f), new Vector2(10f, 2f), 45f);
            lines[3] = Img(root, "BottomRight", Color.white, new Vector2(9f, -9f), new Vector2(10f, 2f), -45f);

            var marker = hud.AddComponent<HitMarker>();
            PlayerPrefabBuilder.Assign(marker, "weapons", controller);
            PlayerPrefabBuilder.Assign(marker, "group", group);
            AssignArray(marker, "lines", lines);
        }

        private static void BuildHealthBar(GameObject hud, Health health)
        {
            RectTransform root = Rect(hud.transform, "Health", BottomLeft, BottomLeft, new Vector2(Margin, EdgeTop),
                new Vector2(300f, 44f));
            Label(root, "Label", "HEALTH", 12, HudColors.Dim, TextAnchor.LowerLeft, new Vector2(0f, 14f),
                new Vector2(200f, 20f), BottomLeft);
            Text value = Label(root, "Value", "100", 26, HudColors.Text, TextAnchor.LowerRight, new Vector2(0f, 12f),
                new Vector2(100f, 32f), BottomRight, FontStyle.Bold);

            RectTransform track = Rect(root, "Track", BottomLeft, BottomLeft, Vector2.zero, new Vector2(300f, 8f));
            Img(track, "Background", new Color(1f, 1f, 1f, 0.1f), Vector2.zero, Vector2.zero).rectTransform.Fill();
            Image ghost = Img(track, "Ghost", HudColors.Ghost, Vector2.zero, Vector2.zero);
            ghost.rectTransform.Fill();
            Image fill = Img(track, "Fill", HudColors.HealthFill, Vector2.zero, Vector2.zero);
            fill.rectTransform.Fill();

            var bar = hud.AddComponent<HealthBar>();
            PlayerPrefabBuilder.Assign(bar, "health", health);
            PlayerPrefabBuilder.Assign(bar, "fill", fill.rectTransform);
            PlayerPrefabBuilder.Assign(bar, "ghost", ghost.rectTransform);
            PlayerPrefabBuilder.Assign(bar, "value", value);
        }

        private static void BuildWeaponSlots(GameObject hud, WeaponController controller, WeaponData[] weapons)
        {
            float width = weapons.Length * SlotWidth + (weapons.Length - 1) * SlotGap;
            RectTransform root = Rect(hud.transform, "Weapons", BottomRight, BottomRight,
                new Vector2(-Margin, EdgeTop), new Vector2(width, 66f));
            var slots = new WeaponSlot[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                WeaponData data = weapons[i];
                RectTransform slot = Rect(root, "Slot " + data.slot, BottomLeft, new Vector2(0f, 0f),
                    new Vector2(i * (SlotWidth + SlotGap), 0f), new Vector2(SlotWidth, 58f));
                slot.gameObject.AddComponent<Image>().color = HudColors.Panel;
                var group = slot.gameObject.AddComponent<CanvasGroup>();
                Label(slot, "Key", data.slot.ToString(), 11, HudColors.Accent, TextAnchor.UpperLeft,
                    new Vector2(6f, -4f), new Vector2(30f, 16f), TopLeft);
                Label(slot, "Name", data.displayName.ToUpperInvariant(), 10, HudColors.Dim, TextAnchor.LowerRight,
                    new Vector2(-6f, 4f), new Vector2(SlotWidth - 12f, 16f), BottomRight);
                Image underline = Img(slot, "Underline", HudColors.Accent, Vector2.zero, Vector2.zero);
                SetRect(underline.rectTransform, BottomLeft, BottomLeft, Vector2.zero, new Vector2(0f, 2f));
                underline.rectTransform.anchorMax = new Vector2(1f, 0f);

                var slotComponent = slot.gameObject.AddComponent<WeaponSlot>();
                PlayerPrefabBuilder.Assign(slotComponent, "weapon", data);
                PlayerPrefabBuilder.Assign(slotComponent, "group", group);
                PlayerPrefabBuilder.Assign(slotComponent, "underline", underline);
                slots[i] = slotComponent;
            }

            var slotsComponent = hud.AddComponent<WeaponSlots>();
            PlayerPrefabBuilder.Assign(slotsComponent, "weapons", controller);
            AssignArray(slotsComponent, "slots", slots);
        }

        private static void BuildObjective(GameObject hud, StringEventChannel objectiveChanged)
        {
            RectTransform root = Rect(hud.transform, "Objective", TopLeft, TopLeft, new Vector2(Margin, -EdgeTop),
                new Vector2(420f, 60f));
            Label(root, "Label", "OBJECTIVE", 12, HudColors.Accent, TextAnchor.UpperLeft, Vector2.zero,
                new Vector2(420f, 18f), TopLeft);
            Text text = Label(root, "Text", string.Empty, 20, HudColors.Text, TextAnchor.UpperLeft,
                new Vector2(0f, -20f), new Vector2(420f, 60f), TopLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            var panel = hud.AddComponent<ObjectivePanel>();
            PlayerPrefabBuilder.Assign(panel, "objectiveChanged", objectiveChanged);
            PlayerPrefabBuilder.Assign(panel, "text", text);
            PlayerPrefabBuilder.Assign(panel, "root", root.gameObject);
            root.gameObject.SetActive(false); // shown when an objective arrives
        }

        private static void BuildPrompt(GameObject hud, PlayerHealth playerHealth)
        {
            // Centred at 60 % of the screen height from the top: 108 px below the centre at 1080 p.
            RectTransform root = Rect(hud.transform, "Prompt", Center, Center, new Vector2(0f, -108f),
                new Vector2(420f, 40f));
            root.gameObject.AddComponent<Image>().color = HudColors.Panel;
            Text key = Label(root, "Key", "E", 17, HudColors.Accent, TextAnchor.MiddleCenter, new Vector2(12f, 0f),
                new Vector2(30f, 28f), MiddleLeft, FontStyle.Bold);
            Text label = Label(root, "Text", string.Empty, 17, HudColors.Text, TextAnchor.MiddleLeft,
                new Vector2(52f, 0f), new Vector2(356f, 28f), MiddleLeft);

            var prompt = hud.AddComponent<InteractionPrompt>();
            PlayerPrefabBuilder.Assign(prompt, "playerHealth", playerHealth);
            PlayerPrefabBuilder.Assign(prompt, "root", root.gameObject);
            PlayerPrefabBuilder.Assign(prompt, "key", key);
            PlayerPrefabBuilder.Assign(prompt, "label", label);
            root.gameObject.SetActive(false); // shown by interactables (Y5)
        }

        private static void BuildMessages(GameObject hud, GameObject player, PlayerHealth playerHealth,
            VoidEventChannel cureTaken, Texture2D band)
        {
            Transform h = hud.transform;

            RectTransform checkpoint = Rect(h, "Checkpoint", TopRight, TopRight, new Vector2(-Margin, -EdgeTop),
                new Vector2(300f, 40f));
            Label(checkpoint, "Label", "CHECKPOINT", 12, HudColors.Dim, TextAnchor.UpperRight, Vector2.zero,
                new Vector2(300f, 18f), TopRight);
            Text checkpointName = Label(checkpoint, "Name", string.Empty, 14, HudColors.Text, TextAnchor.UpperRight,
                new Vector2(0f, -18f), new Vector2(300f, 20f), TopRight);

            RectTransform banner = Rect(h, "AlertBanner", TopCenter, TopCenter, new Vector2(0f, -92f),
                new Vector2(900f, 64f));
            var bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            Raw(banner, "Band", band, AlertRed).rectTransform.Fill();
            Label(banner, "Title", "SECURITY ALERT", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 8f),
                new Vector2(900f, 36f), Center, FontStyle.Bold);
            Label(banner, "Subtitle", "THE CURE HAS BEEN STOLEN — ALL UNITS CONVERGING", 12, Color.white,
                TextAnchor.MiddleCenter, new Vector2(0f, -18f), new Vector2(900f, 18f), Center);

            RectTransform label = Rect(h, "AlertLabel", TopCenter, TopCenter, new Vector2(0f, -58f),
                new Vector2(260f, 22f));
            Raw(label, "Band", band, AlertRed).rectTransform.Fill();
            Label(label, "Text", "SECURITY ALERT", 13, Color.white, TextAnchor.MiddleCenter, Vector2.zero,
                new Vector2(260f, 22f), Center, FontStyle.Bold);

            // 28 % of the height from the top: 302 px at 1080 p.
            Text toast = Label(h, "Toast", string.Empty, 15, HudColors.Text, TextAnchor.MiddleCenter,
                new Vector2(0f, -302f), new Vector2(1000f, 24f), TopCenter);

            var messages = hud.AddComponent<HudMessages>();
            PlayerPrefabBuilder.Assign(messages, "checkpoints", player.GetComponent<CheckpointTracker>());
            PlayerPrefabBuilder.Assign(messages, "playerHealth", playerHealth);
            PlayerPrefabBuilder.Assign(messages, "cureTaken", cureTaken);
            PlayerPrefabBuilder.Assign(messages, "checkpointRoot", checkpoint.gameObject);
            PlayerPrefabBuilder.Assign(messages, "checkpointName", checkpointName);
            PlayerPrefabBuilder.Assign(messages, "toast", toast);
            PlayerPrefabBuilder.Assign(messages, "alertBanner", bannerGroup);
            PlayerPrefabBuilder.Assign(messages, "alertLabel", label.gameObject);
            // Hidden until their events, so the prefab preview shows the HUD as it starts.
            checkpoint.gameObject.SetActive(false);
            banner.gameObject.SetActive(false);
            label.gameObject.SetActive(false);
        }

        // ---- UI helpers -------------------------------------------------------------------------------------------

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 MiddleLeft = new Vector2(0f, 0.5f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        /// <summary>A child RectTransform anchored at one point (anchor = pivot), at a pixel offset and size.</summary>
        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            SetRect(rect, anchor, pivot, position, size);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Fills the parent: used for bars, whose right anchor the HealthBar then moves.</summary>
        private static void Fill(this RectTransform rect)
        {
            Stretch(rect);
            rect.pivot = new Vector2(0f, 0.5f);
        }

        private static Image Img(Transform parent, string name, Color colour, Vector2 position, Vector2 size,
            float rotation = 0f)
        {
            RectTransform rect = Rect(parent, name, Center, Center, position, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage Raw(Transform parent, string name, Texture texture, Color colour)
        {
            RectTransform rect = Rect(parent, name, Center, Center, Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(Transform parent, string name, string text, int size, Color colour,
            TextAnchor alignment, Vector2 position, Vector2 rectSize, Vector2 anchor,
            FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = Rect(parent, name, anchor, anchor, position, rectSize);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = colour;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>(); // the prototype's text-shadow, for readability
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return label;
        }

        private static Color Transparent(Color colour)
        {
            colour.a = 0f;
            return colour;
        }

        private static void AssignArray(Object component, string field, Object[] values)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- generated placeholder textures (white with alpha; the HUD tints them) ----------------------------------

        /// <summary>Transparent inside 45 % of the way to the corners, then fading in (the prototype's radial gradient).</summary>
        private static float VignetteAlpha(float u, float v)
        {
            float r = new Vector2(u - 0.5f, v - 0.5f).magnitude / 0.7071f;
            return Mathf.Clamp01((r - 0.45f) / 0.55f);
        }

        /// <summary>A soft arc at the top of the circle, 25° each side of up, at about 92 px radius in a 256 px image.</summary>
        private static float ArcAlpha(float u, float v)
        {
            float x = (u - 0.5f) * 2f;
            float y = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(x * x + y * y);
            float radial = Mathf.Clamp01(1f - Mathf.Abs(r - 0.72f) / 0.06f);
            float angle = Mathf.Abs(Mathf.Atan2(x, y) * Mathf.Rad2Deg);
            float sideways = Mathf.Clamp01((30f - angle) / 10f);
            return radial * sideways * 0.85f;
        }

        private static float RingAlpha(float u, float v)
        {
            float r = new Vector2(u - 0.5f, v - 0.5f).magnitude * 2f;
            return Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.1f);
        }

        /// <summary>Solid in the middle, fading to nothing at both ends (the banner's red band).</summary>
        private static float BandAlpha(float u, float v)
        {
            return Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2f) * 1.6f;
        }

        private static Texture2D CreateTextureIfMissing(string name, int width, int height, Func<float, float, float> alpha)
        {
            string path = $"{HudFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float a = Mathf.Clamp01(alpha((x + 0.5f) / width, (y + 0.5f) / height));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            Debug.Log($"[Blackout] Generated {path}.");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
