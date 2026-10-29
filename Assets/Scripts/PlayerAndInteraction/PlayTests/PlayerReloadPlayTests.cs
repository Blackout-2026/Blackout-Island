// Owner: Systems Engineer. RL1: active-prefab reload/ammo lifecycle, without native cursor capture.
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Blackout.Shared;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blackout.Diagnostics.PlayTests
{
    public class PlayerReloadPlayTests
    {
        private GameObject player, floor;
        private WeaponController weapons;
        private Color ambient;
        private UnityEngine.Rendering.AmbientMode ambientMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            ambient = RenderSettings.ambientLight;
            ambientMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.gray;
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = LayerMask.NameToLayer("Level");
            floor.transform.position = new Vector3(-180f, 48.5f, -150f);
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"),
                new Vector3(-180f, 49f, -150f), Quaternion.identity);
            weapons = player.GetComponentInChildren<WeaponController>(true);
            var light = new GameObject("Reload test light").AddComponent<Light>();
            light.transform.SetParent(player.transform);
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            weapons.TakeNextLootWeapon(); weapons.TakeNextLootWeapon();
            weapons.DebugEquip(1);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientMode = ambientMode;
            if (player != null) Object.Destroy(player);
            if (floor != null) Object.Destroy(floor);
            yield return null;
        }

        private ViewmodelReload Visual => weapons.GetComponentInChildren<ViewmodelReload>();
        private WeaponAmmo Ammo => weapons.AmmoOf(weapons.ActiveWeapon);
        private void EmptyMagazine()
        {
            int loaded = Ammo.Loaded;
            for (int i = 0; i < loaded; i++) Assert.That(Ammo.Consume(), Is.True);
        }

        [UnityTest]
        public IEnumerator BothGuns_UseTheRealAutomaticAmmoTimer_AndRefillOnce()
        {
            foreach (int slot in new[] { 1, 2 })
            {
                weapons.DebugEquip(slot);
                yield return null;
                CaptureCamera(slot + "-normal");
                WeaponAmmo ammo = Ammo;
                int spares = ammo.SpareMagazines;
                EmptyMagazine();
                Assert.That(ammo.CanFire, Is.False);
                yield return null; yield return null;
                Assert.That(Visual.ShowingReload, Is.True);
                CaptureCamera(slot + "-start");
                // WA1 retains an inactive legacy rig: inspect the visual actually driven by this component.
                var reloadRoot = (GameObject)new SerializedObject(Visual).FindProperty("reloadVisual").objectReferenceValue;
                Transform magazine = Find(reloadRoot.transform, "Magazine");
                Vector3 before = magazine.position;
                yield return new WaitForSeconds(0.4f);
                Assert.That(Vector3.Distance(magazine.position, before), Is.GreaterThan(0.01f), "real LateUpdate moves the rig");
                CaptureCamera(slot + "-reload");
                yield return new WaitForSeconds(0.65f);
                Assert.That(Visual.ShowingReload, Is.False);
                Assert.That(ammo.Loaded, Is.EqualTo(ammo.MagazineSize));
                Assert.That(ammo.SpareMagazines, Is.EqualTo(spares - 1));
                Assert.That(ammo.CanFire, Is.True);
                CaptureCamera(slot + "-finish");
            }
        }

        [UnityTest]
        public IEnumerator SwitchAwayAndBack_ResumesTheTimerPhase_WithoutStackingVisuals()
        {
            var smg = Visual;
            var ammo = Ammo;
            EmptyMagazine();
            yield return null; yield return null;
            Assert.That(smg.ShowingReload, Is.True);
            weapons.DebugEquip(3);
            Assert.That(smg.ShowingReload, Is.False);
            yield return new WaitForSeconds(0.3f);
            weapons.DebugEquip(1);
            yield return null; yield return null;
            Assert.That(smg.ShowingReload, Is.True);
            Assert.That(ammo.ReloadRemaining, Is.LessThan(0.8f));
            yield return new WaitForSeconds(1f);
            Assert.That(smg.ShowingReload, Is.False);
            EmptyMagazine();
            yield return null; yield return null;
            Assert.That(smg.ShowingReload, Is.True, "the same component can play the next reload");
        }

        [UnityTest]
        public IEnumerator PauseFreezesReload_AndCheckpointRestoreClearsOrResumesIt()
        {
            object loaded = weapons.CaptureCheckpoint();
            EmptyMagazine();
            yield return new WaitForSeconds(0.3f);
            object midReload = weapons.CaptureCheckpoint();
            Time.timeScale = 0f;
            yield return null; yield return null; // Let the current frame's LateUpdate catch up before measuring the frozen pose.
            float remaining = Ammo.ReloadRemaining;
            var reloadRoot = (GameObject)new SerializedObject(Visual).FindProperty("reloadVisual").objectReferenceValue;
            Transform wrist = Find(reloadRoot.transform, "HandL");
            Vector3 before = wrist.localPosition;
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Ammo.ReloadRemaining, Is.EqualTo(remaining));
            Assert.That(Vector3.Distance(wrist.localPosition, before), Is.LessThan(0.000001f));
            Time.timeScale = 1f;
            weapons.RestoreCheckpoint(loaded);
            yield return null; yield return null;
            Assert.That(Visual.ShowingReload, Is.False);
            weapons.RestoreCheckpoint(midReload);
            yield return null; yield return null;
            Assert.That(Visual.ShowingReload, Is.True);
            yield return new WaitForSeconds(1f);
            Assert.That(Visual.ShowingReload, Is.False);
        }

        [UnityTest]
        public IEnumerator NoSpareStaysEmpty_PickupStartsReload_DeathAndRespawnRestoreVisuals()
        {
            Ammo.Restore(new WeaponAmmo.State { Loaded = 1, SpareMagazines = 0 });
            EmptyMagazine();
            yield return null; yield return null;
            Assert.That(Visual.ShowingReload, Is.False);
            Assert.That(Ammo.CanFire, Is.False);
            weapons.AddMagazinesFromBody();
            yield return null; yield return null;
            Assert.That(Visual.ShowingReload, Is.True);
            var reload = Visual;
            player.GetComponent<Health>().TakeDamage(new DamageInfo(1000f, player.transform.position, Vector3.forward, null, DamageType.Generic));
            Assert.That(reload.ShowingReload, Is.False);
            yield return new WaitForSeconds(2.4f);
            Assert.That(player.GetComponent<Health>().IsAlive, Is.True);
            weapons.DebugEquip(1);
            yield return new WaitForSeconds(1.1f);
            Assert.That(Visual.ShowingReload, Is.False);
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            Assert.Fail("Missing bone " + name); return null;
        }

        // Optional evidence from the actual runtime SkinnedMeshRenderers, rather than the editor's baked review copies.
        private void CaptureCamera(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Camera camera = player.GetComponentInChildren<Camera>();
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                string folder = Path.Combine(Path.GetTempPath(), "blackout-RL1-runtime-preview");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = previousTarget;
                target.Release();
                Object.Destroy(target); Object.Destroy(image);
            }
        }
    }
}
#endif
