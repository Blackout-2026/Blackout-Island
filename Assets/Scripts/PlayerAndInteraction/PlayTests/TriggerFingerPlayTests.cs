// Owner: Systems Engineer. TF1: saved mesh wiring, real fire input and visual lifecycle.
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
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
    public class TriggerFingerPlayTests
    {
        private GameObject player;
        private WeaponController weapons;
        private Mouse mouse;
        private InputSettings original, settings;
        private HideFlags flags;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            original = InputSystem.settings; flags = original.hideFlags; original.hideFlags = HideFlags.DontSave;
            settings = Object.Instantiate(original);
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = settings;
            mouse = InputSystem.AddDevice<Mouse>();
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), new Vector3(-180,49,-150), Quaternion.identity);
            weapons = player.GetComponentInChildren<WeaponController>(true);
            weapons.TakeNextLootWeapon(); weapons.TakeNextLootWeapon(); weapons.DebugEquip(1);
            yield return null;
            Cursor.lockState = CursorLockMode.None;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            Object.Destroy(player);
            yield return null;
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings = original; original.hideFlags = flags;
            Object.DestroyImmediate(settings);
        }

        private TriggerFinger Active => weapons.GetComponentInChildren<TriggerFinger>();

        [UnityTest]
        public IEnumerator RealFireInput_PullsOnlyEquippedGun_AndDryFireDoesNotPull()
        {
            foreach (int slot in new[] { 1,2 })
            {
                weapons.DebugEquip(slot);
                yield return null;
                var finger = Active;
                var ammo = weapons.AmmoOf(weapons.ActiveWeapon);
                int before = ammo.Loaded;
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
                yield return null;
                weapons.Tick(.001f,true);
                Assert.That(ammo.Loaded,Is.EqualTo(before-1));
                Assert.That(finger.Pull,Is.EqualTo(1));
                finger.Tick(.25f);
                Assert.That(finger.Pull,Is.EqualTo(1),"a slow firing frame must not erase the pull before it renders");
                Assert.That(weapons.GetComponentsInChildren<TriggerFinger>(true).Where(f=>f!=finger).All(f=>f.Pull==0),Is.True);
                yield return new WaitForSeconds(.2f);
                Assert.That(finger.Pull,Is.EqualTo(1),"an accepted shot keeps the trigger pressed while LMB is held");
                Capture("held-"+slot);
                InputSystem.QueueStateEvent(mouse,new MouseState());
                yield return null;
                Capture("shot-"+slot);
                yield return new WaitForSeconds(.2f);
                Assert.That(finger.Pull,Is.Zero);
                Capture("rest-"+slot);
                while(ammo.Consume()) { }
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
                yield return null;
                weapons.Tick(.001f,true);
                Assert.That(finger.Pull,Is.Zero,"an empty gun emits no accepted-shot motion");
                InputSystem.QueueStateEvent(mouse,new MouseState());
                yield return null;
            }
            weapons.DebugEquip(3);
            Assert.That(Active,Is.Null,"knife is unchanged");
        }

        [UnityTest]
        public IEnumerator LastShot_AutoReloadsBothGunsWithoutR_AndRefillsOnce()
        {
            foreach(int slot in new[] { 1,2 })
            {
                weapons.DebugEquip(slot);
                var ammo = weapons.AmmoOf(weapons.ActiveWeapon);
                ammo.Restore(new WeaponAmmo.State { Loaded=1, SpareMagazines=1 });
                yield return null;
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
                yield return null;
                weapons.Tick(.001f,true);
                Assert.That(player.GetComponent<PlayerInputReader>().ReloadPressed,Is.False);
                Assert.That(ammo.Loaded,Is.Zero);
                Assert.That(ammo.Reloading,Is.True);
                Assert.That(Active.Pull,Is.Zero,"reload owns the hand pose on the last shot");
                InputSystem.QueueStateEvent(mouse,new MouseState());
                yield return null;
                Assert.That(weapons.GetComponentInChildren<ViewmodelReload>().ShowingReload,Is.True);
                yield return new WaitForSeconds(1.1f);
                Assert.That(ammo.Loaded,Is.EqualTo(ammo.MagazineSize));
                Assert.That(ammo.SpareMagazines,Is.Zero);
                Assert.That(ammo.Reloading,Is.False);
            }
        }

        [UnityTest]
        public IEnumerator LiveRenderer_DrawsDifferentFingerPixelsWhileHeld_ThenReturns()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Requires the graphics-enabled verification run.");
            var finger = Active;
            var skin = finger.GetComponentInChildren<SkinnedMeshRenderer>();
            foreach(var renderer in weapons.GetComponentsInChildren<Renderer>(true)) renderer.enabled = renderer == skin;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            skin.sharedMaterial = material; // A plain silhouette excludes muzzle lighting from this pixel check.
            var mesh = new Mesh();
            skin.SetBlendShapeWeight(0,0); skin.BakeMesh(mesh,true);
            Vector3[] rest = mesh.vertices;
            skin.SetBlendShapeWeight(0,100); skin.BakeMesh(mesh,true);
            Vector3[] pull = mesh.vertices;
            Vector3 centre = Vector3.zero;
            int count = 0;
            for(int i=0;i<rest.Length;i++)
                if(Vector3.Distance(rest[i],pull[i])>.001f) { centre+=rest[i]; count++; }
            Assert.That(count,Is.GreaterThan(0));
            skin.SetBlendShapeWeight(0,0);
            var cameraObject = new GameObject("Live finger pixel check");
            cameraObject.transform.SetParent(skin.transform,false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled=false; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            camera.orthographic=true; camera.orthographicSize=.045f; camera.nearClipPlane=.001f;
            Vector3 point = skin.transform.TransformPoint(centre/count);
            camera.transform.position=point+skin.transform.TransformDirection(new Vector3(.25f,.02f,.02f));
            camera.transform.LookAt(point,skin.transform.up);
            var target = new RenderTexture(256,256,24);
            var image = new Texture2D(256,256,TextureFormat.RGB24,false);
            camera.targetTexture=target;
            RenderTexture previous=RenderTexture.active;
            string folder=Path.Combine(Path.GetTempPath(),"blackout-TF1-revision","live-finger");
            Directory.CreateDirectory(folder);
            Color32[] Read(string name)
            {
                camera.Render(); RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,256,256),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
                return image.GetPixels32();
            }
            try
            {
                yield return null;
                Color32[] before=Read("rest");
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left));
                yield return null;
                weapons.Tick(.001f,true);
                yield return new WaitForSeconds(.2f);
                Assert.That(finger.Pull,Is.EqualTo(1));
                Color32[] fired=Read("held");
                int changed=before.Zip(fired,(a,b)=>a.Equals(b)?0:1).Sum();
                Assert.That(changed,Is.GreaterThan(10),"the live GPU renderer must show movement, not just a changed property or CPU bake");
                InputSystem.QueueStateEvent(mouse,new MouseState());
                yield return new WaitForSeconds(.25f);
                Assert.That(finger.Pull,Is.Zero);
                Read("released");
                File.WriteAllText(Path.Combine(folder,"result.txt"),"Changed live pixels: "+changed);
            }
            finally
            {
                camera.targetTexture=null; RenderTexture.active=previous; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(image); Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(material);
            }
        }

        [UnityTest]
        public IEnumerator ReloadSwitchCheckpointPauseHideAndDisable_ResetWithoutStacking()
        {
            var finger = Active;
            for(int i=0;i<3;i++)
            {
                weapons.DebugFire(); Assert.That(finger.Pull,Is.EqualTo(1));
                finger.Tick(.25f);
                Assert.That(finger.Pull,Is.EqualTo(1),"a fresh shot renders even on a slow frame, without held input");
                finger.enabled = false; Assert.That(finger.Pull,Is.Zero);
                finger.enabled = true;
            }
            var saved = weapons.CaptureCheckpoint();
            weapons.DebugFire(); weapons.RestoreCheckpoint(saved); Assert.That(finger.Pull,Is.Zero);
            weapons.DebugFire(); Time.timeScale = 0; yield return null; Assert.That(finger.Pull,Is.Zero);
            Time.timeScale = 1;
            weapons.DebugFire();
            weapons.GetComponent<ViewmodelHands>().SetCinematicHidden(true);
            yield return null; Assert.That(finger.Pull,Is.Zero);
            weapons.GetComponent<ViewmodelHands>().SetCinematicHidden(false);
            weapons.DebugFire(); weapons.DebugEquip(2); Assert.That(finger.Pull,Is.Zero);
            weapons.DebugEquip(1);
            var ammo = weapons.AmmoOf(weapons.ActiveWeapon); ammo.Consume();
            weapons.DebugFire(); Assert.That(weapons.TryReload(),Is.True);
            Assert.That(finger.Pull,Is.Zero);
            yield return null;
            Assert.That(finger.isActiveAndEnabled,Is.False,"existing sampled reload owns the hands");
            Assert.That(weapons.GetComponentInChildren<ViewmodelReload>().ShowingReload,Is.True);
            yield return new WaitForSeconds(1.1f);
            Assert.That(finger.isActiveAndEnabled,Is.True);
            Assert.That(finger.Pull,Is.Zero);
            weapons.DebugFire(); Assert.That(finger.Pull,Is.EqualTo(1));
        }

        [Test]
        public void SavedRenderer_DeformsAndReturnsWithoutCumulativeChanges()
        {
            foreach (var finger in weapons.GetComponentsInChildren<TriggerFinger>(true))
            {
                var skin = finger.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var baked = new Mesh();
                try
                {
                    skin.SetBlendShapeWeight(0,0); skin.BakeMesh(baked,true);
                    Vector3[] rest = baked.vertices;
                    skin.SetBlendShapeWeight(0,100); skin.BakeMesh(baked,true);
                    Vector3[] pulled = baked.vertices;
                    Assert.That(rest.Zip(pulled,Vector3.Distance).Max(),Is.GreaterThan(.0005f),"saved renderer really deforms");
                    skin.SetBlendShapeWeight(0,0); skin.BakeMesh(baked,true);
                    Assert.That(rest.Zip(baked.vertices,Vector3.Distance).Max(),Is.LessThan(1e-6f),"returns without cumulative deformation");
                }
                finally { Object.DestroyImmediate(baked); }
            }
        }

        private void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = player.GetComponentInChildren<Camera>();
            var target = new RenderTexture(1280,720,24);
            var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            RenderTexture previous = RenderTexture.active, previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                string folder = Path.Combine(Path.GetTempPath(),"blackout-TF1-revision","runtime");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previous; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
#endif
