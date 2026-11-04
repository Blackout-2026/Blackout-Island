// GN1: only manages the red-dot scope component and its HUD overlay on the Player prefab.
using System;
using Blackout.Hud;
using Blackout.Player;
using Blackout.Weapons;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    public static class RedDotScopeBuilder
    {
        private const string CameraPath = "CameraRig/CameraPivot/PlayerCamera";

        [MenuItem("Blackout/Systems/Build Red Dot Scope")]
        public static void Build()
        {
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                Camera camera = player.transform.Find(CameraPath)?.GetComponent<Camera>();
                WeaponController weapons = player.GetComponentInChildren<WeaponController>(true);
                ViewmodelHands hands = player.GetComponentInChildren<ViewmodelHands>(true);
                Transform hud = player.transform.Find("HUD");
                Transform crosshair = hud != null ? hud.Find("Crosshair") : null;
                Transform smg = weapons != null ? weapons.transform.Find("SMG") : null;
                SmgAimPose pose = smg != null ? smg.GetComponent<SmgAimPose>() : null;
                if (camera == null || weapons == null || hands == null || crosshair == null || pose == null)
                    throw new InvalidOperationException("Build the Player, its HUD and the SMG aim pose (Apply SMG Grip And Sights) before the red-dot scope.");

                Transform existing = hud.Find("RedDotScope");
                GameObject overlay = existing != null ? existing.gameObject
                    : new GameObject("RedDotScope", typeof(RectTransform), typeof(RedDotOverlay));
                if (existing == null)
                {
                    overlay.transform.SetParent(hud, false);
                    overlay.transform.SetAsFirstSibling(); // Health and messages stay readable above the mask.
                }
                var rect = (RectTransform)overlay.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                overlay.layer = hud.gameObject.layer;
                GetOrAdd<CanvasRenderer>(overlay);
                GetOrAdd<RedDotOverlay>(overlay).raycastTarget = false;
                overlay.SetActive(false);
                CanvasGroup visibility = GetOrAdd<CanvasGroup>(crosshair.gameObject); // the sniper scope's group, if it is already there

                RedDotScope scope = GetOrAdd<RedDotScope>(player);
                PlayerPrefabBuilder.Assign(scope, "pose", pose);
                PlayerPrefabBuilder.Assign(scope, "hands", hands);
                PlayerPrefabBuilder.Assign(scope, "playerCamera", camera);
                PlayerPrefabBuilder.Assign(scope, "overlay", overlay);
                PlayerPrefabBuilder.Assign(scope, "crosshair", visibility);
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            Debug.Log("[GN1] Red-dot scope view wired to the SMG's right-mouse aim.");
        }

        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }
    }
}
