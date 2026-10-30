// SG1: real Aim input through the saved player; cursor capture supplied explicitly in batch mode.
#if UNITY_EDITOR
using System.Collections;
using System.IO;
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
    public class SmgAimPlayTests
    {
        [UnityTest]
        public IEnumerator AimReleaseReloadSwitchAndDeath_RestoreTheHipPose()
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
            floor.transform.position = new Vector3(-180,48.5f,-150);
            floor.transform.localScale = new Vector3(100,1,100);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"),new Vector3(-180,49,-150),Quaternion.identity);
            try
            {
                Time.timeScale = 1;
                var weapons = player.GetComponentInChildren<WeaponController>(true);
                weapons.TakeNextLootWeapon();
                weapons.TakeNextLootWeapon();
                weapons.DebugEquip(1);
                yield return null;
                var pose = weapons.GetComponentInChildren<SmgAimPose>(true);
                pose.SendMessage("OnApplicationFocus",true);
                Transform smg = pose.transform;
                Vector3 hip = smg.localPosition;
                Quaternion hipRotation = smg.localRotation;
                Camera camera = player.GetComponentInChildren<Camera>();
                Capture(camera,"hip");
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));
                yield return null;
                Assert.That(player.GetComponent<PlayerInputReader>().AimHeld,Is.True);
                pose.Tick(1,true);
                Assert.That(pose.Blend,Is.EqualTo(1));
                Capture(camera,"aim");
                Vector3 sight = camera.WorldToViewportPoint(smg.Find("AimReference").position);
                Assert.That(sight.x,Is.EqualTo(.5f).Within(.001f));
                Assert.That(sight.y,Is.EqualTo(.5f).Within(.001f));
                Quaternion kick = Quaternion.Euler(-4,1,0);
                weapons.transform.localRotation = kick;
                pose.Tick(1,true);
                Assert.That(Quaternion.Angle(smg.rotation,camera.transform.rotation*kick),Is.LessThan(.01f),"ADS must not cancel holder recoil");
                weapons.transform.localRotation = Quaternion.identity;
                InputSystem.QueueStateEvent(mouse,new MouseState());
                yield return null;
                pose.Tick(1,true);
                Assert.That(smg.localPosition,Is.EqualTo(hip));
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));
                yield return null;
                pose.Tick(1,true);
                WeaponAmmo ammo = weapons.AmmoOf(weapons.ActiveWeapon);
                int rounds = ammo.Loaded;
                for(int i=0;i<rounds;i++) Assert.That(ammo.Consume(),Is.True);
                pose.Tick(.1f,true);
                Assert.That(ammo.Reloading,Is.True);
                Assert.That(smg.localPosition,Is.EqualTo(hip));
                yield return new WaitForSeconds(.45f);
                Capture(camera,"reload");
                yield return new WaitForSeconds(.65f);
                pose.Tick(1,true);
                Assert.That(pose.Blend,Is.EqualTo(1),"held RMB resumes after ammo reload");
                Time.timeScale = 0;
                pose.Tick(.1f,true);
                Assert.That(pose.Blend,Is.Zero);
                Time.timeScale = 1;
                pose.Tick(1,true);
                pose.SendMessage("OnApplicationFocus",false);
                Assert.That(smg.localPosition,Is.EqualTo(hip));
                pose.SendMessage("OnApplicationFocus",true);
                pose.Tick(1,true);
                var hands = weapons.GetComponent<ViewmodelHands>();
                hands.SetCinematicHidden(true);
                pose.Tick(.1f,true);
                Assert.That(pose.Blend,Is.Zero);
                hands.SetCinematicHidden(false);
                pose.Tick(1,true);
                pose.Tick(.1f,false);
                Assert.That(pose.Blend,Is.Zero,"lost cursor capture cancels ADS");
                pose.Tick(1,true);
                weapons.DebugEquip(2);
                Assert.That(pose.Blend,Is.Zero);
                Assert.That(smg.localPosition,Is.EqualTo(hip));
                weapons.DebugEquip(1);
                yield return null;
                pose.Tick(1,true);
                player.GetComponent<Health>().TakeDamage(new DamageInfo(1000,player.transform.position,Vector3.zero,null,DamageType.Generic));
                pose.Tick(.1f,true);
                Assert.That(pose.Blend,Is.Zero);
                Assert.That(smg.localPosition,Is.EqualTo(hip));
                Assert.That(Quaternion.Angle(smg.localRotation,hipRotation),Is.LessThan(.001f));
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
            yield return null;
        }

        // Optional graphics-enabled evidence from the live player, including sampled reload skinning.
        private static void Capture(Camera camera, string name)
        {
            Capture(camera,name,1280,720);
            Capture(camera,name+"-4x3",960,720);
            Capture(camera,name+"-wide",1920,720);
        }

        private static void Capture(Camera camera, string name, int width, int height)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args,"-smgPreviewOut");
            if (index < 0 || index + 1 >= args.Length) return;
            string folder = args[index+1];
            Directory.CreateDirectory(folder);
            var target = new RenderTexture(width,height,24);
            var image = new Texture2D(width,height,TextureFormat.RGB24,false);
            RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
            float previousAspect = camera.aspect;
            var light = new GameObject("SMG test review light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35,-40,0);
            try
            {
                camera.targetTexture = target;
                camera.aspect = width/(float)height;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,width,height),0,0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                camera.aspect = previousAspect;
                RenderTexture.active = active;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(light.gameObject);
            }
        }
    }
}
#endif
