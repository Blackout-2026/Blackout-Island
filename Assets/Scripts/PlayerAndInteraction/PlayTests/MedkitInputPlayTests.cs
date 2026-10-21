// Owner: Systems Engineer. Exercise the configured action asset and the real player's Update path.
#if UNITY_EDITOR
using System.Collections;
using Blackout.Player;
using Blackout.Interaction;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Blackout.Diagnostics.PlayTests
{
    public class MedkitInputPlayTests
    {
        private GameObject player;
        private Keyboard keyboard;
        private GameObject drop;
        private InputSettings originalSettings;
        private InputSettings testSettings;
        private HideFlags originalSettingsFlags;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Batch mode has no focused Game view. Use an in-memory copy; never save project settings.
            originalSettings = InputSystem.settings;
            originalSettingsFlags = originalSettings.hideFlags;
            // Input System destroys its default HideAndDontSave settings when replaced.
            // Keep that original alive so teardown can put back the exact object.
            originalSettings.hideFlags = HideFlags.DontSave;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (player != null) Object.Destroy(player);
            if (drop != null) Object.Destroy(drop);
            InputSystem.settings = originalSettings;
            originalSettings.hideFlags = originalSettingsFlags;
            if (testSettings != null) Object.Destroy(testSettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Q_UsesOneKitPerPress_AndDisabledInputCannotHeal()
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"),
                new Vector3(-180f, 50f, -150f), Quaternion.identity);
            player.GetComponent<PlayerMotor>().enabled = false;
            player.GetComponent<PlayerLook>().enabled = false;
            var input = player.GetComponent<PlayerInputReader>();
            var inventory = player.GetComponent<MedkitInventory>();
            var health = player.GetComponent<Health>();
            health.TakeDamage(new DamageInfo(60f, Vector3.zero, Vector3.forward, null, DamageType.Generic));
            inventory.AddMedkit();
            inventory.AddMedkit();
            yield return null;
            Assert.That(input.isActiveAndEnabled, Is.True, "real player input must be active");
            var actions = (InputActionAsset)new SerializedObject(input).FindProperty("actions").objectReferenceValue;
            var use = actions.FindAction("Player/UseMedkit", true);
            Assert.That(use.enabled, Is.True, "the configured map must be enabled");
            Assert.That(Time.timeScale, Is.GreaterThan(0f));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return null;
            yield return null;
            Assert.That(keyboard.qKey.isPressed, Is.True, "queued Q must reach the virtual keyboard; enabled=" + keyboard.enabled);
            Assert.That(use.IsPressed(), Is.True, "the configured Q action must receive the key");
            Assert.That(health.Current, Is.EqualTo(65f));
            Assert.That(inventory.Count, Is.EqualTo(1));
            Assert.That(player.transform.Find("HUD/Medkits/Count").GetComponent<Text>().text, Is.EqualTo("1"));
            yield return null; // held Q must not repeat
            Assert.That(inventory.Count, Is.EqualTo(1));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            input.enabled = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return null;
            yield return null;
            Assert.That(health.Current, Is.EqualTo(65f));
            Assert.That(inventory.Count, Is.EqualTo(1));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            input.enabled = true;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return null;
            yield return null;
            Assert.That(health.Current, Is.EqualTo(90f));
            Assert.That(inventory.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator WalkingOverDrop_CollectsAtFullHealth_AndHudReenableShowsCount()
        {
            Vector3 start = new Vector3(-180f, 50f, -150f);
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), start, Quaternion.identity);
            player.GetComponent<PlayerMotor>().enabled = false;
            player.GetComponent<PlayerLook>().enabled = false;
            player.GetComponent<PlayerInputReader>().enabled = false;
            drop = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/MedkitDrop.prefab"), start, Quaternion.identity);
            var inventory = player.GetComponent<MedkitInventory>();
            var body = player.GetComponent<CharacterController>();
            Physics.SyncTransforms();
            for (int i = 0; i < 3; i++)
            {
                body.Move(Vector3.right * 0.01f);
                yield return new WaitForFixedUpdate();
            }
            yield return null;
            Assert.That(inventory.Count, Is.EqualTo(1));
            Assert.That(drop.GetComponent<MedkitPickup>().IsTaken, Is.True);
            Assert.That(player.GetComponent<Health>().Current, Is.EqualTo(100f));
            Transform hud = player.transform.Find("HUD");
            hud.gameObject.SetActive(false);
            inventory.AddMedkit();
            hud.gameObject.SetActive(true);
            Assert.That(hud.Find("Medkits/Count").GetComponent<Text>().text, Is.EqualTo("2"));
        }
    }
}
#endif
