// Owner: Systems Engineer. K1: exercise the real melee callback, holder and swing lifecycle.
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Blackout.Player;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blackout.Diagnostics.PlayTests
{
    public class KnifeSwingPlayTests
    {
        private GameObject player;
        private WeaponController weapons;
        private KnifeSwing swing;
        private Camera camera;
        private float previousCaptureDelta;
        private Color previousAmbient;
        private UnityEngine.Rendering.AmbientMode previousAmbientMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousCaptureDelta = Time.captureDeltaTime;
            previousAmbient = RenderSettings.ambientLight;
            previousAmbientMode = RenderSettings.ambientMode;
            Time.captureDeltaTime = 1f / 120f;
            Time.timeScale = 1f;
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"),
                new Vector3(1000f, 1000f, 1000f), Quaternion.identity);
            player.GetComponent<PlayerMotor>().enabled = false;
            player.GetComponent<PlayerInputReader>().enabled = false;
            weapons = player.GetComponentInChildren<WeaponController>(true);
            swing = weapons.GetComponentInChildren<KnifeSwing>(true);
            camera = player.GetComponentInChildren<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            camera.aspect = 16f / 9f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.gray;
            Light light = new GameObject("Knife test light").AddComponent<Light>();
            light.transform.SetParent(player.transform);
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = previousCaptureDelta;
            Time.timeScale = 1f;
            RenderSettings.ambientLight = previousAmbient;
            RenderSettings.ambientMode = previousAmbientMode;
            if (player != null) Object.Destroy(player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealKnifeFire_AnimatesArmWithoutExtraHolderKick_AndReturnsToRest()
        {
            Vector3 rest = swing.Group.localPosition;
            Vector3 holderPosition = weapons.transform.localPosition;
            Quaternion holderRotation = weapons.transform.localRotation;
            Capture("rest");
            weapons.DebugFire(); // Same Fire -> MeleeWeapon.Swung path as input, without native mouse capture.
            Assert.That(swing.Swinging, Is.True);
            bool moved = false;
            for (int frame = 0; frame < 55; frame++)
            {
                yield return null;
                moved |= Vector3.Distance(swing.Group.localPosition, rest) > 0.02f;
                Assert.That(Vector3.Distance(weapons.transform.localPosition, holderPosition), Is.LessThan(0.00001f));
                Assert.That(Quaternion.Angle(weapons.transform.localRotation, holderRotation), Is.LessThan(0.001f),
                    "the old recoil spring must not rotate both arms as well as the knife swing");
                if (frame == 5 || frame == 13 || frame == 20 || frame == 35) Capture("frame-" + frame);
            }
            Assert.That(moved, Is.True);
            Assert.That(swing.Swinging, Is.False);
            Assert.That(Vector3.Distance(swing.Group.localPosition, rest), Is.LessThan(0.00001f));
            Capture("finished");
        }

        [UnityTest]
        public IEnumerator PauseAndDisable_ResetCleanly_AndRepeatedSwingsReuseOneGroup()
        {
            Vector3 rest = swing.Group.localPosition;
            weapons.DebugFire();
            for (int i = 0; i < 8; i++) yield return null;
            Time.timeScale = 0f;
            yield return null;
            yield return null;
            Vector3 frozen = swing.Group.localPosition;
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(swing.Group.localPosition, Is.EqualTo(frozen));
            Time.timeScale = 1f;
            swing.gameObject.SetActive(false);
            Assert.That(swing.Swinging, Is.False);
            Assert.That(Vector3.Distance(swing.Group.localPosition, rest), Is.LessThan(0.00001f));
            swing.gameObject.SetActive(true);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                weapons.DebugFire();
                Assert.That(swing.Swinging, Is.True);
                for (int i = 0; i < 55; i++) yield return null;
                Assert.That(swing.Swinging, Is.False);
                Assert.That(Vector3.Distance(swing.Group.localPosition, rest), Is.LessThan(0.00001f));
            }
            Assert.That(swing.transform.Cast<Transform>().Count(t => t.name == "Swing"), Is.EqualTo(1));
        }

        private void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            string folder = Path.Combine(Path.GetTempPath(), "blackout-K1", "runtime-preview");
            Directory.CreateDirectory(folder);
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
#endif
