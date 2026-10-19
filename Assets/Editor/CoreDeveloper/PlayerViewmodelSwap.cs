using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/Art/Use Smg Visual In Player: puts the real viewmodel (<c>Weapon_Smg_Visual</c>, the Blender model with hands)
    /// into the Player prefab in place of the cube and cylinder stand-in that Build Player Weapons made. The weapon object
    /// keeps its scripts, its Muzzle and its FlashQuads (the Systems Engineer's, at the same muzzle point), so shooting does not
    /// change; only the meshes do. The model is nested as a child called "Visual" at the weapon's origin, which is the gun
    /// frame of docs/art-pipeline.md section 14. Re-running keeps the model and only re-applies the framing.
    /// To go back, delete the Visual child and run Build Player Weapons on a fresh Player prefab.
    /// The knife is nested under its swinging "Hand" (the whole hand swings), and the Hand is moved to <see cref="KnifeHandPosition"/>
    /// and <see cref="KnifeHandEuler"/> because the Blender knife is longer than the placeholder blade and would otherwise sit
    /// mostly below the screen edge (preview_knife.py, view knife_player).
    /// </summary>
    public static class PlayerViewmodelSwap
    {
        public const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        public const string SmgVisualPath = "Assets/Prefabs/Models/Weapon_Smg_Visual.prefab";
        public const string KnifeVisualPath = "Assets/Prefabs/Models/Weapon_Knife_Visual.prefab";
        public const string SniperVisualPath = "Assets/Prefabs/Models/Weapon_Sniper_Visual.prefab";
        public const string VisualName = "Visual";

        // How each viewmodel is framed in the WeaponHolder (before its 0.85 scale): a weapon seen straight from behind is a long
        // tube with its hands hidden or foreshortened to nubs, so every weapon is turned inward (yaw) like a real first-person model,
        // which shows its left side, the supporting hand, and the right hand at the grip. Found with Art/Blender/Scripts/preview_game.py
        // (the 72 degree game camera); run it with the same numbers to see the same picture.
        public static readonly Vector3 SmgPosition = new Vector3(0.13f, -0.12f, 0.42f);
        public static readonly Vector3 SmgEuler = new Vector3(4f, -20f, -4f);
        public static readonly Vector3 RiflePosition = new Vector3(0.16f, -0.135f, 0.52f);
        public static readonly Vector3 RifleEuler = new Vector3(4f, -16f, -4f);

        // Where the Hand of the Knife sits and how it is turned: the blade points up and to the left across the screen and the fist
        // and the camouflaged forearm come in from below, so the knife is held ready to stab. The roll (-35) shows the flat of the
        // blade (a blade held edge-on looks like a needle from behind) with the fist wrapped round the handle under it.
        public static readonly Vector3 KnifeHandPosition = new Vector3(0.207f, -0.166f, 0.447f);
        public static readonly Vector3 KnifeHandEuler = new Vector3(-30f, -33f, -35f);

        private const string HolderPath = "CameraRig/CameraPivot/PlayerCamera/WeaponHolder";

        // The primitives PlayerWeaponsBuilder.BuildSmg makes under "SMG".
        private static readonly string[] SmgStandIns = { "Body", "Barrel", "Magazine", "Grip", "Sight" };

        // The primitives PlayerWeaponsBuilder.BuildRifle makes under "Rifle".
        private static readonly string[] RifleStandIns = { "Body", "Barrel", "Stock", "Butt", "Magazine", "Scope" };

        // The three boxes PlayerWeaponsBuilder.BuildKnife makes under "Knife/Hand".
        private static readonly string[] KnifeStandIns = { "Handle", "Guard", "Blade" };

        [MenuItem("Blackout/Art/Use Smg Visual In Player")]
        public static void SwapSmg()
        {
            Swap("SMG", SmgVisualPath, SmgStandIns, SmgPosition, SmgEuler);
        }

        [MenuItem("Blackout/Art/Use Rifle Visual In Player")]
        public static void SwapRifle()
        {
            Swap("Rifle", SniperVisualPath, RifleStandIns, RiflePosition, RifleEuler);
        }

        /// <summary>Does all three swaps (SMG, rifle, knife); each one does nothing when its weapon already has the model.</summary>
        [MenuItem("Blackout/Art/Use All Weapon Visuals In Player")]
        public static void SwapAll()
        {
            SwapSmg();
            SwapRifle();
            SwapKnife();
        }

        [MenuItem("Blackout/Art/Use Knife Visual In Player")]
        public static void SwapKnife()
        {
            Swap("Knife/Hand", KnifeVisualPath, KnifeStandIns, KnifeHandPosition, KnifeHandEuler);
        }

        private static void Swap(string weaponName, string visualPath, string[] standIns, Vector3? position, Vector3? euler)
        {
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
            if (visual == null)
            {
                Debug.LogError($"[PlayerViewmodel] {visualPath} is missing; run Blackout > Art > Build Weapon Prefabs first.");
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(PlayerPrefabPath) == null)
            {
                Debug.LogError($"[PlayerViewmodel] {PlayerPrefabPath} is missing; run the Systems Engineer's Build Player Prefab first.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Transform weapon = root.transform.Find($"{HolderPath}/{weaponName}");
                if (weapon == null)
                {
                    Debug.LogError($"[PlayerViewmodel] The Player prefab has no {HolderPath}/{weaponName}; run Build Player Weapons first.");
                    return;
                }

                if (position.HasValue)
                {
                    weapon.localPosition = position.Value;
                }

                if (euler.HasValue)
                {
                    weapon.localRotation = Quaternion.Euler(euler.Value);
                }

                if (weapon.Find(VisualName) != null)
                {
                    // The model is already there: only the framing above can have changed.
                    PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                    Debug.Log($"[PlayerViewmodel] {weaponName} already uses {visualPath}; its framing was updated.");
                    return;
                }

                foreach (string standIn in standIns)
                {
                    Transform part = weapon.Find(standIn);
                    if (part != null)
                    {
                        Object.DestroyImmediate(part.gameObject);
                    }
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(visual, weapon);
                instance.name = VisualName;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                SetLayerRecursively(instance.transform, root.layer);
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    // Same as the stand-ins: a viewmodel casts no shadow and receives none.
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath, out bool saved);
                Debug.Log(saved
                    ? $"[PlayerViewmodel] {weaponName} in {PlayerPrefabPath} now uses {visualPath}."
                    : $"[PlayerViewmodel] Could not save {PlayerPrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetLayerRecursively(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            foreach (Transform child in target)
            {
                SetLayerRecursively(child, layer);
            }
        }
    }
}
