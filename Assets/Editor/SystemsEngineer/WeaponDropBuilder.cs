using Blackout.Interaction;
using Blackout.Weapons;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds the gun a dead enemy leaves behind (step L1): Assets/Prefabs/Interactables/WeaponDrop.prefab, a WeaponDropPickup that shows the SMG or the sniper rifle
    /// model, and puts an EnemyWeaponDrop on the three enemies that carry a gun (the Security Patrol, the Hunter and the Commander; the robot guard does not).
    /// The models are the real weapon prefabs with their hands, sleeves and muzzle flash switched off, so they look like guns lying on the ground.
    /// Safe to run again: the prefab is rebuilt and each enemy gets the component once.
    /// </summary>
    public static class WeaponDropBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Interactables/WeaponDrop.prefab";
        public const string SmgModelPath = "Assets/Prefabs/Models/Weapon_Smg_Visual.prefab";
        public const string SniperModelPath = "Assets/Prefabs/Models/Weapon_Sniper_Visual.prefab";

        public static readonly string[] EnemyPrefabs =
        {
            "Assets/Prefabs/Agents/SecurityPatrol.prefab",
            "Assets/Prefabs/Agents/Hunter.prefab",
            "Assets/Prefabs/Agents/Commander.prefab",
        };

        private static readonly string[] HiddenPrefixes = { "Glove", "Sleeve", "Wrist", "Fingers", "Thumb", "Hand", "MuzzleFlash" };

        private const float FloatHeight = 0.3f;     // [GUESS] high enough to read as a pickup, low enough to reach with the 3 m ray
        private const float ModelScale = 1.1f;      // [GUESS] a little larger than life so it is easy to spot on a dark floor

        [MenuItem("Blackout/Systems/Build Weapon Drops")]
        public static void Build()
        {
            int layer = LayerMask.NameToLayer("Interactable");
            var smg = AssetDatabase.LoadAssetAtPath<WeaponData>(PlayerWeaponsBuilder.SmgPath);
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponData>(PlayerWeaponsBuilder.RiflePath);
            var smgModel = AssetDatabase.LoadAssetAtPath<GameObject>(SmgModelPath);
            var sniperModel = AssetDatabase.LoadAssetAtPath<GameObject>(SniperModelPath);
            if (layer < 0 || smg == null || rifle == null || smgModel == null || sniperModel == null)
            {
                Debug.LogError("[Blackout] Weapon drops not built: the Interactable layer, the SMG and Rifle data and the two weapon models must exist first.");
                return;
            }

            GameObject drop = BuildDrop(layer, smg, rifle, smgModel, sniperModel);
            foreach (string path in EnemyPrefabs)
            {
                AddDropToEnemy(path, drop);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Blackout] Weapon drops built: " + PrefabPath + " on " + EnemyPrefabs.Length + " enemies.");
        }

        private static GameObject BuildDrop(int layer, WeaponData smg, WeaponData rifle, GameObject smgModel, GameObject sniperModel)
        {
            EnsureFolder("Assets/Prefabs", "Interactables");

            var root = new GameObject("WeaponDrop");
            try
            {
                Transform visual = PlayerPrefabBuilder.CreateChild("Visual", root.transform, new Vector3(0f, FloatHeight, 0f));
                GameObject smgObject = AddModel(smgModel, "Smg", visual);
                GameObject sniperObject = AddModel(sniperModel, "Sniper", visual);
                sniperObject.SetActive(false);

                Transform glowObject = PlayerPrefabBuilder.CreateChild("Glow", root.transform, new Vector3(0f, FloatHeight + 0.3f, 0f));
                var glow = glowObject.gameObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.78f, 0.35f);
                glow.intensity = 0.6f;
                glow.range = 3f;
                glow.shadows = LightShadows.None;

                var zone = root.AddComponent<BoxCollider>();
                zone.isTrigger = true;
                zone.center = new Vector3(0f, FloatHeight, 0f);
                zone.size = new Vector3(1.1f, 0.8f, 0.8f);

                var pickup = root.AddComponent<WeaponDropPickup>();
                PlayerPrefabBuilder.Assign(pickup, "visual", visual);
                PlayerPrefabBuilder.Assign(pickup, "glow", glow);
                AssignShowcases(pickup, new[] { smg, rifle }, new[] { smgObject, sniperObject });

                PlayerPrefabBuilder.SetLayerRecursively(root.transform, layer);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // A viewmodel without its hands, sleeves and muzzle flash: just the gun.
        private static GameObject AddModel(GameObject model, string name, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * ModelScale;
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                if (child != instance.transform && IsHidden(child.name))
                {
                    child.gameObject.SetActive(false);
                }
            }

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            return instance;
        }

        internal static bool IsHidden(string objectName)
        {
            foreach (string prefix in HiddenPrefixes)
            {
                if (objectName.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssignShowcases(WeaponDropPickup pickup, WeaponData[] weapons, GameObject[] models)
        {
            var serialized = new SerializedObject(pickup);
            SerializedProperty list = serialized.FindProperty("showcases");
            list.arraySize = weapons.Length;
            for (int i = 0; i < weapons.Length; i++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("weapon").objectReferenceValue = weapons[i];
                item.FindPropertyRelative("model").objectReferenceValue = models[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddDropToEnemy(string path, GameObject drop)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning("[Blackout] Enemy prefab not found, no weapon drop added: " + path);
                return;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var component = contents.GetComponent<EnemyWeaponDrop>();
                if (component == null)
                {
                    component = contents.AddComponent<EnemyWeaponDrop>();
                }

                PlayerPrefabBuilder.Assign(component, "dropPrefab", drop);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
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
