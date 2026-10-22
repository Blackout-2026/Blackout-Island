// Owner: Systems Engineer. Real Input System events; batch mode's unavailable cursor capture is supplied to Tick explicitly.
#if UNITY_EDITOR
using System.Collections;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Blackout.Diagnostics.PlayTests
{
    public class SniperScopePlayTests
    {
        private GameObject player;
        private GameObject floor;
        private Mouse mouse;
        private Keyboard keyboard;
        private SniperScope scope;
        private WeaponController weapons;
        private Camera camera;
        private PlayerLook look;
        private InputSettings originalSettings, testSettings;
        private HideFlags settingsFlags;
        private CursorLockMode originalCursor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            originalCursor = Cursor.lockState;
            originalSettings = InputSystem.settings;
            settingsFlags = originalSettings.hideFlags;
            originalSettings.hideFlags = HideFlags.DontSave;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = LayerMask.NameToLayer("Level");
            floor.transform.position = new Vector3(-180f, 48.5f, -150f);
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"),
                new Vector3(-180f, 49f, -150f), Quaternion.identity);
            scope = player.GetComponent<SniperScope>();
            weapons = player.GetComponentInChildren<WeaponController>(true);
            camera = player.GetComponentInChildren<Camera>();
            look = player.GetComponent<PlayerLook>();
            weapons.TakeNextLootWeapon();
            weapons.TakeNextLootWeapon();
            Assert.That(weapons.DebugEquip(2), Is.True);
            yield return null;
            scope.SendMessage("OnApplicationFocus", true);
            Cursor.lockState = CursorLockMode.Locked;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (player != null) Object.Destroy(player);
            if (floor != null) Object.Destroy(floor);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings = originalSettings;
            originalSettings.hideFlags = settingsFlags;
            if (testSettings != null) Object.Destroy(testSettings);
            yield return null;
            Cursor.lockState = originalCursor;
            Cursor.visible = originalCursor != CursorLockMode.Locked;
        }

        private IEnumerator HoldScope()
        {
            Cursor.lockState = CursorLockMode.Locked;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return new WaitForSeconds(0.45f);
            scope.Tick(0.45f, true); // Batch mode keeps Cursor.lockState=None, even with a graphics device.
            var input = player.GetComponent<PlayerInputReader>();
            bool focus = (bool)typeof(SniperScope).GetField("focused", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(scope);
            Assert.That(scope.IsScoped, Is.True,
                $"zoom={scope.Zoom}, cursor={Cursor.lockState}, focus={focus}, RMB={mouse.rightButton.isPressed}, aim={input.AimHeld}, " +
                $"input={input.isActiveAndEnabled}, look={look.isActiveAndEnabled}, motor={player.GetComponent<PlayerMotor>().isActiveAndEnabled}, " +
                $"state={player.GetComponent<PlayerMotor>().State}, health={player.GetComponent<Health>().Current}, weapon={weapons.ActiveWeapon?.name}, switching={weapons.IsSwitching}, timeScale={Time.timeScale}");
            Assert.That(camera.fieldOfView, Is.EqualTo(22f).Within(0.1f));
        }

        private void AssertNormal()
        {
            Assert.That(scope.Zoom, Is.Zero);
            Assert.That(camera.fieldOfView, Is.EqualTo(72f).Within(0.01f));
            Assert.That(look.ScopeSensitivityMultiplier, Is.EqualTo(1f));
            Assert.That(player.transform.Find("HUD/SniperScope").gameObject.activeSelf, Is.False);
            Assert.That(player.transform.Find("HUD/Crosshair").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ReloadLowersScope_AndHeldRmbAimsAgainWhenTheMagazineIsReady()
        {
            yield return HoldScope();
            WeaponAmmo ammo = weapons.AmmoOf(weapons.ActiveWeapon);
            int rounds = ammo.Loaded;
            for (int i = 0; i < rounds; i++) ammo.Consume();
            scope.Tick(0.1f, true);
            AssertNormal();
            yield return null; yield return null;
            Assert.That(weapons.GetComponentInChildren<ViewmodelReload>().ShowingReload, Is.True);
            yield return new WaitForSeconds(1.05f);
            scope.Tick(0.45f, true);
            Assert.That(scope.IsScoped, Is.True);
            Assert.That(ammo.Loaded, Is.EqualTo(ammo.MagazineSize));
            Assert.That(weapons.GetComponentInChildren<ViewmodelReload>().ShowingReload, Is.False);
        }

        [UnityTest]
        public IEnumerator HoldingRmb_ZoomsAndHidesViewmodel_ReleasingRestoresEverything()
        {
            yield return HoldScope();
            Assert.That(look.ScopeSensitivityMultiplier, Is.EqualTo(0.35f).Within(0.01f));
            Assert.That(weapons.GetComponent<ViewmodelHands>().Shown, Is.False);
            Assert.That(player.transform.Find("HUD/SniperScope").gameObject.activeSelf, Is.True);
            Assert.That(player.transform.Find("HUD/Crosshair").GetComponent<CanvasGroup>().alpha, Is.Zero);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return new WaitForSeconds(0.45f);
            scope.Tick(0.45f, true);
            AssertNormal();
            Assert.That(weapons.GetComponent<ViewmodelHands>().Shown, Is.True);
        }

        [UnityTest]
        public IEnumerator OnlyTheRifleScopes_AndSwitchingCancelsBeforeTheModelChanges()
        {
            foreach (int slot in new[] { 1, 3 })
            {
                weapons.DebugEquip(slot);
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return new WaitForSeconds(0.2f);
                scope.Tick(0.2f, true);
                AssertNormal();
            }
            weapons.DebugEquip(2);
            yield return HoldScope();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            weapons.SendMessage("Request", (object)0); // Box the index so Unity does not choose the SendMessageOptions overload.
            yield return null;
            yield return null;
            Assert.That(weapons.IsSwitching, Is.True);
            AssertNormal();
        }

        [UnityTest]
        public IEnumerator PauseFocusLossAndDisabledInput_ResetWithoutLeavingAHiddenWeapon()
        {
            yield return HoldScope();
            scope.Tick(0.1f, false);
            AssertNormal();
            yield return HoldScope();
            Time.timeScale = 0f;
            yield return null;
            scope.Tick(0f, true);
            AssertNormal();
            Time.timeScale = 1f;
            yield return HoldScope();
            scope.SendMessage("OnApplicationFocus", false);
            scope.Tick(0.1f, true);
            AssertNormal();
            yield return null;
            AssertNormal();
            scope.SendMessage("OnApplicationFocus", true);
            yield return HoldScope();
            player.GetComponent<PlayerInputReader>().enabled = false;
            yield return null;
            scope.Tick(0.1f, true);
            AssertNormal();
            Assert.That(weapons.GetComponent<ViewmodelHands>().Shown, Is.True);
        }

        [UnityTest]
        public IEnumerator ReenableCheckpointAndDeath_ClearTransientScopeState()
        {
            object saved = weapons.CaptureCheckpoint();
            for (int i = 0; i < 2; i++)
            {
                yield return HoldScope();
                scope.enabled = false;
                AssertNormal();
                scope.enabled = true;
                yield return HoldScope();
                weapons.RestoreCheckpoint(saved);
                AssertNormal();
            }
            yield return HoldScope();
            player.GetComponent<Health>().TakeDamage(new DamageInfo(1000f, player.transform.position, Vector3.forward, null, DamageType.Generic));
            AssertNormal();
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return new WaitForSeconds(2.4f);
            Assert.That(player.GetComponent<Health>().IsAlive, Is.True);
            AssertNormal();
            yield return HoldScope(); // Controls and scope work again after the real respawn.
        }

        [UnityTest]
        public IEnumerator SprintCancelsScope_AndRifleStillFiresThroughTheCameraWhileScoped()
        {
            yield return HoldScope();
            Vector3 origin = camera.transform.position;
            Vector3 direction = camera.transform.forward;
            PelletTrace shot = default;
            int traces = 0;
            weapons.PelletTraced += trace => { shot = trace; traces++; };
            weapons.DebugFire(); // Real shot/recoil path; native LMB capture and ammo accounting remain manual here.
            Assert.That(traces, Is.EqualTo(1));
            Assert.That(Vector3.Distance(shot.Ray.origin, origin), Is.LessThan(0.01f));
            Assert.That(Vector3.Dot(shot.Ray.direction, direction), Is.GreaterThan(0.9999f));
            Assert.That(scope.IsScoped, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return new WaitForSeconds(0.1f);
            scope.Tick(0.1f, true);
            Assert.That(player.GetComponent<PlayerMotor>().State, Is.EqualTo(MovementState.Sprint));
            AssertNormal();
            Assert.That(weapons.GetComponent<ViewmodelHands>().Shown, Is.False, "sprint remains a separate visibility rule");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.15f);
            Assert.That(weapons.GetComponent<ViewmodelHands>().Shown, Is.True);
        }
    }
}
#endif
