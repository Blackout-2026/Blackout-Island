// Owner: Systems Engineer.
using Blackout.Hud;
using Blackout.Interaction;
using Blackout.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Editor
{
    /// <summary>Adds MC1 content in place. Existing HUD, weapons, animation and prefab variants are retained.</summary>
    public static class MedkitCompassBuilder
    {
        public const string DropPath = "Assets/Prefabs/Interactables/MedkitDrop.prefab";
        private const string VisualPath = "Assets/Prefabs/Models/Medkit_Visual.prefab";
        private static readonly string[] Enemies =
        {
            "SecurityPatrol", "Hunter", "TacticalGuard", "Commander"
        };
        private static readonly Vector2 Top = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        [MenuItem("Blackout/Systems/Add Medkits And Compass")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath) == null)
                throw new System.InvalidOperationException("Missing existing Medkit_Visual prefab.");
            GameObject drop = BuildDrop();
            foreach (string enemy in Enemies)
            {
                string path = "Assets/Prefabs/Agents/" + enemy + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var component = GetOrAdd<EnemyWeaponDrop>(root);
                    PlayerPrefabBuilder.Assign(component, "medkitPrefab", drop);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            BuildPlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[Blackout] MC1: stored medkits on four enemy prefabs; Q, inventory HUD and compass wired.");
        }

        private static GameObject BuildDrop()
        {
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(DropPath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(DropPath) : new GameObject("MedkitDrop");
            try
            {
                Transform visual = root.transform.Find("Visual");
                if (visual == null)
                {
                    visual = new GameObject("Visual").transform;
                    visual.SetParent(root.transform, false);
                    visual.localPosition = Vector3.up * 0.35f;
                    PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath), visual);
                }
                Transform glowTransform = root.transform.Find("Glow");
                if (glowTransform == null)
                {
                    glowTransform = new GameObject("Glow").transform;
                    glowTransform.SetParent(root.transform, false);
                    glowTransform.localPosition = Vector3.up * 0.65f;
                }
                var glow = GetOrAdd<Light>(glowTransform.gameObject);
                glow.type = LightType.Point;
                glow.color = new Color(0.38f, 1f, 0.56f);
                glow.intensity = 0.5f;
                glow.range = 3f;
                glow.shadows = LightShadows.None;
                var trigger = GetOrAdd<CapsuleCollider>(root);
                trigger.isTrigger = true;
                trigger.radius = 1.1f;
                trigger.height = 2f;
                trigger.center = Vector3.up * 0.5f;
                var pickup = GetOrAdd<MedkitPickup>(root);
                PlayerPrefabBuilder.Assign(pickup, "visual", visual);
                PlayerPrefabBuilder.Assign(pickup, "glow", glow);
                // The nested art stays intact; only the gameplay root receives the interaction layer.
                root.layer = LayerMask.NameToLayer("Interactable");
                return PrefabUtility.SaveAsPrefabAsset(root, DropPath);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        private static void BuildPlayer()
        {
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                Transform hud = player.transform.Find("HUD");
                if (hud == null) throw new System.InvalidOperationException("Build Player HUD first.");
                var inventory = GetOrAdd<MedkitInventory>(player);
                PlayerPrefabBuilder.Assign(inventory, "input", player.GetComponent<PlayerInputReader>());
                BuildCounter(hud, inventory);
                BuildCompass(hud, player);
                // The original compact alert at y=58 would cover the compass's distance row.
                var alert = hud.Find("AlertLabel") as RectTransform;
                if (alert != null && Mathf.Approximately(alert.anchoredPosition.y, -58f))
                    alert.anchoredPosition = new Vector2(alert.anchoredPosition.x, -82f);
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        private static void BuildCounter(Transform hud, MedkitInventory inventory)
        {
            Vector2 bottomLeft = Vector2.zero;
            RectTransform panel = Rect(hud, "Medkits", bottomLeft, bottomLeft, new Vector2(34f, 100f), new Vector2(210f, 62f));
            GetOrAdd<Image>(panel.gameObject).color = HudColors.Panel;
            // F2: only the number here; AmmoLootBuilder adds the medkit symbol (no MEDKITS caption, no Q hint).
            Text count = Label(panel, "Count", "0", 26, HudColors.Text, new Vector2(70f, -5f), new Vector2(60f, 32f));
            var counter = GetOrAdd<MedkitCounter>(hud.gameObject);
            PlayerPrefabBuilder.Assign(counter, "inventory", inventory);
            PlayerPrefabBuilder.Assign(counter, "value", count);
            SetUi(panel);
        }

        private static void BuildCompass(Transform hud, GameObject player)
        {
            RectTransform root = Rect(hud, "Compass", Top, Top, new Vector2(0f, -22f), new Vector2(460f, 54f));
            RectTransform viewport = Rect(root, "Viewport", Top, Top, Vector2.zero, new Vector2(460f, 30f));
            GetOrAdd<RectMask2D>(viewport.gameObject);
            RectTransform strip = Rect(viewport, "Strip", TopLeft, TopLeft, Vector2.zero, new Vector2(2160f, 30f));
            string[] cardinal = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            for (int degree = -360; degree <= 720; degree += 15)
            {
                int normal = (degree + 360) % 360;
                bool major = normal % 45 == 0;
                Text tick = Label(strip, "Tick_" + degree, major ? cardinal[normal / 45] : normal.ToString("000"),
                    major ? 14 : 11, major ? HudColors.Text : HudColors.Dim,
                    new Vector2(degree * CompassMath.PixelsPerDegree, -2f), new Vector2(42f, 20f));
                tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = TopLeft;
            }
            RectTransform centre = Rect(viewport, "Centre", Top, Top, new Vector2(0f, -22f), new Vector2(2f, 8f));
            GetOrAdd<Image>(centre.gameObject).color = HudColors.Accent;
            Text marker = Label(viewport, "Objective", "▼", 14, HudColors.Accent, new Vector2(0f, -18f), new Vector2(18f, 16f));
            Text heading = Label(root, "Heading", "000°", 13, HudColors.Text, new Vector2(-145f, -32f), new Vector2(90f, 20f));
            Text destination = Label(root, "Destination", string.Empty, 12, HudColors.Accent,
                new Vector2(60f, -32f), new Vector2(300f, 20f));
            var compass = GetOrAdd<CompassHud>(hud.gameObject);
            PlayerPrefabBuilder.Assign(compass, "player", player.transform);
            PlayerPrefabBuilder.Assign(compass, "playerHealth", player.GetComponent<PlayerHealth>());
            PlayerPrefabBuilder.Assign(compass, "root", root.gameObject);
            PlayerPrefabBuilder.Assign(compass, "strip", strip);
            PlayerPrefabBuilder.Assign(compass, "objectiveMarker", marker.rectTransform);
            PlayerPrefabBuilder.Assign(compass, "heading", heading);
            PlayerPrefabBuilder.Assign(compass, "destination", destination);
            SetUi(root);
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var result = parent.Find(name) as RectTransform;
            if (result == null)
            {
                result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                result.SetParent(parent, false);
            }
            result.anchorMin = result.anchorMax = anchor;
            result.pivot = pivot;
            result.anchoredPosition = position;
            result.sizeDelta = size;
            return result;
        }

        private static Text Label(Transform parent, string name, string text, int size, Color colour, Vector2 position, Vector2 dimensions)
        {
            var rect = Rect(parent, name, Top, Top, position, dimensions);
            var label = GetOrAdd<Text>(rect.gameObject);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.raycastTarget = false;
            var shadow = GetOrAdd<Shadow>(rect.gameObject);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return label;
        }

        private static void SetUi(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = LayerMask.NameToLayer("UI");
                foreach (Graphic graphic in child.GetComponents<Graphic>()) graphic.raycastTarget = false;
            }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component =>
            target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
    }
}
