// GN1: the red-dot view through the real saved Player: real right-mouse input, cursor capture supplied explicitly in batch mode.
#if UNITY_EDITOR
using System.Collections;
using Blackout.Hud;
using Blackout.Player;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Blackout.Diagnostics.PlayTests
{
    public class RedDotScopePlayTests
    {
        [UnityTest]
        public IEnumerator HeldRightMouse_ShowsTheDotAndHidesTheGun_ReleaseSwitchAndReloadRestoreEverything()
        {
            InputSettings original = InputSystem.settings;
            HideFlags flags = original.hideFlags;
            original.hideFlags = HideFlags.DontSave;
            var settings = Object.Instantiate(original);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = LayerMask.NameToLayer("Level");
            floor.transform.position = new Vector3(-180, 48.5f, -150);
            floor.transform.localScale = new Vector3(100, 1, 100);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), new Vector3(-180, 49, -150), Quaternion.identity);
            try
            {
                Time.timeScale = 1;
                var weapons = player.GetComponentInChildren<WeaponController>(true);
                weapons.TakeNextLootWeapon();
                weapons.TakeNextLootWeapon();
                weapons.DebugEquip(1);
                yield return null;
                var pose = weapons.GetComponentInChildren<SmgAimPose>(true);
                pose.SendMessage("OnApplicationFocus", true);
                var scope = player.GetComponent<RedDotScope>();
                var hands = weapons.GetComponent<ViewmodelHands>();
                Camera camera = player.GetComponentInChildren<Camera>();
                float normal = camera.fieldOfView;
                GameObject overlay = player.transform.Find("HUD/RedDotScope").gameObject;
                var crosshair = player.transform.Find("HUD/Crosshair").GetComponent<CanvasGroup>();
                Assert.That(overlay.activeSelf, Is.False);

                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return null;
                pose.Tick(1, true);
                scope.Tick();
                Assert.That(scope.IsScoped, Is.True, "a fully raised aim looks through the dot");
                Assert.That(overlay.activeSelf, Is.True);
                Assert.That(camera.fieldOfView, Is.LessThan(normal - 5f), "the view narrows");
                Assert.That(crosshair.alpha, Is.Zero);
                Assert.That(hands.Shown, Is.False, "the gun and arms are hidden");
                Assert.That(hands.ShownIgnoringSight, Is.True);
                pose.Tick(.1f, true);
                Assert.That(pose.Blend, Is.EqualTo(1), "hiding the gun must not cancel the aim that raised it");

                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                pose.Tick(1, true);
                scope.Tick();
                Assert.That(scope.IsScoped, Is.False);
                Assert.That(overlay.activeSelf, Is.False);
                Assert.That(camera.fieldOfView, Is.EqualTo(normal).Within(.01f));
                Assert.That(crosshair.alpha, Is.EqualTo(1f));
                Assert.That(hands.Shown, Is.True);

                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return null;
                pose.Tick(1, true);
                scope.Tick();
                Assert.That(scope.IsScoped, Is.True);
                WeaponAmmo ammo = weapons.AmmoOf(weapons.ActiveWeapon);
                int rounds = ammo.Loaded;
                for (int i = 0; i < rounds; i++) Assert.That(ammo.Consume(), Is.True);
                Assert.That(ammo.Reloading, Is.True);
                pose.Tick(.1f, true);
                scope.Tick();
                Assert.That(scope.IsScoped, Is.False, "reloading ends the view so the reload can be seen");
                Assert.That(hands.Shown, Is.True);

                // Release first: with the button held the rifle's own scope would (rightly) start to zoom once it is the held gun.
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                weapons.DebugEquip(2);
                yield return null;
                pose.Tick(1, true);
                scope.Tick();
                Assert.That(camera.fieldOfView, Is.EqualTo(normal).Within(.01f), "switching away leaves the normal view");
                Assert.That(overlay.activeSelf, Is.False);
            }
            finally
            {
                Time.timeScale = 1;
                Object.Destroy(player);
                Object.Destroy(floor);
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings = original;
                original.hideFlags = flags;
                Object.DestroyImmediate(settings);
            }
        }
    }
}
#endif
