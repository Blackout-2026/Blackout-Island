// Owner: Systems Engineer.
using Blackout.Hud;
using Blackout.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Editor
{
    /// <summary>
    /// Step F2, the parts of the ammunition and the HUD that live in prefabs. Added in place; nothing that exists is rebuilt:
    /// - Assets/Prefabs/Interactables/AmmoDrop.prefab: three small magazines and a trigger (an AmmoPickup), and it is given to the four enemies'
    ///   EnemyWeaponDrop, so every body leaves 2 SMG magazines and 1 sniper magazine to take;
    /// - the rounds on the weapon slots of the Player's HUD (the loaded rounds and the spare ones, or RELOAD);
    /// - the medkit counter of MC1 reduced to a medkit symbol and the number.
    /// Safe to run again.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.AmmoLootBuilder.Build
    /// </summary>
    public static class AmmoLootBuilder
    {
        public const string DropPath = "Assets/Prefabs/Interactables/AmmoDrop.prefab";
        private const string MaterialFolder = "Assets/Materials/Models";
        private static readonly string[] Enemies = { "SecurityPatrol", "Hunter", "TacticalGuard", "Commander" };
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BottomLeft = Vector2.zero;

        [MenuItem("Blackout/Systems/Add Ammunition Loot And HUD")]
        public static void Build()
        {
            GameObject drop = BuildDrop();
            foreach (string enemy in Enemies)
            {
                string path = "Assets/Prefabs/Agents/" + enemy + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var component = root.GetComponent<EnemyWeaponDrop>();
                    if (component == null)
                    {
                        component = root.AddComponent<EnemyWeaponDrop>();
                    }

                    PlayerPrefabBuilder.Assign(component, "ammoPrefab", drop);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            BuildPlayerHud();
            AssetDatabase.SaveAssets();
            Debug.Log("[Blackout] F2: AmmoDrop on four enemy prefabs; rounds on the weapon slots; medkit counter reduced to a symbol and a number.");
        }

        // ---- the drop -----------------------------------------------------------------------------------------------

        private static GameObject BuildDrop()
        {
            Material steel = SaveMaterial("M_AmmoMag_Steel", new Color(0.16f, 0.17f, 0.18f), 0.55f, 0.6f);
            Material brass = SaveMaterial("M_AmmoMag_Brass", new Color(0.72f, 0.55f, 0.22f), 0.9f, 0.55f);

            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(DropPath) != null;
            GameObject root = existing ? PrefabUtility.LoadPrefabContents(DropPath) : new GameObject("AmmoDrop");
            try
            {
                Transform visual = root.transform.Find("Visual");
                if (visual != null)
                {
                    Object.DestroyImmediate(visual.gameObject); // rebuilt below, so a re-run never stacks magazines
                }

                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                visual.localPosition = Vector3.up * 0.3f;
                // two SMG magazines (long, slightly curved) and one shorter sniper magazine
                AddMagazine(visual, "SmgMagazineA", new Vector3(-0.14f, 0f, 0f), new Vector3(0.07f, 0.24f, 0.11f), -8f, steel, brass);
                AddMagazine(visual, "SmgMagazineB", new Vector3(0f, 0.01f, 0.03f), new Vector3(0.07f, 0.24f, 0.11f), 6f, steel, brass);
                AddMagazine(visual, "SniperMagazine", new Vector3(0.15f, -0.02f, 0f), new Vector3(0.06f, 0.17f, 0.08f), 0f, steel, brass);

                Transform glowTransform = root.transform.Find("Glow");
                if (glowTransform == null)
                {
                    glowTransform = new GameObject("Glow").transform;
                    glowTransform.SetParent(root.transform, false);
                    glowTransform.localPosition = Vector3.up * 0.6f;
                }

                var glow = GetOrAdd<Light>(glowTransform.gameObject);
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.78f, 0.35f);
                glow.intensity = 0.5f;
                glow.range = 3f;
                glow.shadows = LightShadows.None;

                var trigger = GetOrAdd<CapsuleCollider>(root);
                trigger.isTrigger = true;
                trigger.radius = 1.1f;
                trigger.height = 2f;
                trigger.center = Vector3.up * 0.5f;

                var pickup = GetOrAdd<AmmoPickup>(root);
                PlayerPrefabBuilder.Assign(pickup, "visual", visual);
                PlayerPrefabBuilder.Assign(pickup, "glow", glow);
                root.layer = LayerMask.NameToLayer("Interactable");
                return PrefabUtility.SaveAsPrefabAsset(root, DropPath);
            }
            finally
            {
                if (existing)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void AddMagazine(Transform parent, string name, Vector3 position, Vector3 size, float roll, Material steel, Material brass)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = name;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(parent, false);
            body.transform.localPosition = position;
            body.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            body.transform.localScale = size;
            body.GetComponent<MeshRenderer>().sharedMaterial = steel;

            // the brass of the top round, seen over the lips of the magazine
            var round = GameObject.CreatePrimitive(PrimitiveType.Cube);
            round.name = name + "Rounds";
            Object.DestroyImmediate(round.GetComponent<Collider>());
            round.transform.SetParent(body.transform, false);
            round.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            round.transform.localScale = new Vector3(0.8f, 0.08f, 0.8f);
            round.GetComponent<MeshRenderer>().sharedMaterial = brass;
        }

        private static Material SaveMaterial(string name, Color colour, float metallic, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            }

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (created)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        // ---- the HUD ------------------------------------------------------------------------------------------------

        private static void BuildPlayerHud()
        {
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                Transform hud = player.transform.Find("HUD");
                if (hud == null)
                {
                    throw new System.InvalidOperationException("Build Player HUD first.");
                }

                AddAmmoLabels(hud);
                ReduceMedkitCounter(hud);
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        // The slot is 104 x 58: the key top left, the name bottom right. The rounds go in the middle: the loaded ones large, the spare ones after them.
        private static void AddAmmoLabels(Transform hud)
        {
            Transform weapons = hud.Find("Weapons");
            if (weapons == null)
            {
                return;
            }

            foreach (WeaponSlot slot in weapons.GetComponentsInChildren<WeaponSlot>(true))
            {
                Text loaded = Label(slot.transform, "AmmoLoaded", string.Empty, 20, HudColors.Text, TextAnchor.MiddleRight,
                    new Vector2(-4f, 4f), new Vector2(46f, 26f), FontStyle.Bold);
                Text reserve = Label(slot.transform, "AmmoReserve", string.Empty, 11, HudColors.Dim, TextAnchor.MiddleLeft,
                    new Vector2(4f, 2f), new Vector2(50f, 18f), FontStyle.Normal);
                PlayerPrefabBuilder.Assign(slot, "ammoLoaded", loaded);
                PlayerPrefabBuilder.Assign(slot, "ammoReserve", reserve);
                SetUi(slot.transform);
            }
        }

        // "MEDKITS", the Q hint and the panel's wide frame go; a medkit (a white case with a red cross) and the number stay.
        private static void ReduceMedkitCounter(Transform hud)
        {
            var panel = hud.Find("Medkits") as RectTransform;
            if (panel == null)
            {
                return;
            }

            foreach (string obsolete in new[] { "Label", "Hint" })
            {
                Transform old = panel.Find(obsolete);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            panel.anchorMin = panel.anchorMax = BottomLeft;
            panel.pivot = BottomLeft;
            panel.sizeDelta = new Vector2(100f, 46f);

            RectTransform symbol = Rect(panel, "Symbol", Center, Center, new Vector2(-26f, 0f), new Vector2(32f, 26f));
            GetOrAdd<Image>(symbol.gameObject).color = new Color(0.92f, 0.92f, 0.9f);
            RectTransform bar = Rect(symbol, "CrossBar", Center, Center, Vector2.zero, new Vector2(18f, 6f));
            GetOrAdd<Image>(bar.gameObject).color = new Color(0.8f, 0.1f, 0.1f);
            RectTransform post = Rect(symbol, "CrossPost", Center, Center, Vector2.zero, new Vector2(6f, 18f));
            GetOrAdd<Image>(post.gameObject).color = new Color(0.8f, 0.1f, 0.1f);

            var count = panel.Find("Count") as RectTransform;
            if (count != null)
            {
                count.anchorMin = count.anchorMax = Center;
                count.pivot = Center;
                count.anchoredPosition = new Vector2(20f, 0f);
                count.sizeDelta = new Vector2(52f, 36f);
                var text = count.GetComponent<Text>();
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleLeft;
                text.fontSize = 28;
            }

            SetUi(panel);
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

        private static Text Label(Transform parent, string name, string text, int size, Color colour, TextAnchor alignment, Vector2 position,
            Vector2 dimensions, FontStyle style)
        {
            RectTransform rect = Rect(parent, name, Center, Center, position, dimensions);
            var label = GetOrAdd<Text>(rect.gameObject);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = colour;
            label.alignment = alignment;
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
                foreach (Graphic graphic in child.GetComponents<Graphic>())
                {
                    graphic.raycastTarget = false;
                }
            }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component =>
            target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
    }
}
