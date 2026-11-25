// Owner: Systems Engineer. RL1: scoped imports and saved wiring for the player's fitted reload rigs.
using System;
using System.IO;
using System.Linq;
using Blackout.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    public sealed class PlayerReloadImporter : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => 100; // After the existing common static-model rules.
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Models/Custom/SM_Reload_", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
        }
    }

    public static class PlayerReloadBuilder
    {
        public const string SmgModel = "Assets/Models/Custom/SM_Reload_Smg.fbx";
        public const string RifleModel = "Assets/Models/Custom/SM_Reload_Rifle.fbx";
        public const string SourceFolder = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/";

        [MenuItem("Blackout/Systems/Add Manual Reload Input")]
        public static void BuildManualReloadInput()
        {
            // Edit the assigned actions through the Input System JSON API; existing IDs/GUID stay intact.
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath);
            var reader = player.GetComponent<Blackout.Player.PlayerInputReader>();
            var assigned = new SerializedObject(reader).FindProperty("actions").objectReferenceValue;
            string path = AssetDatabase.GetAssetPath(assigned);
            var actions = InputActionAsset.FromJson(File.ReadAllText(path));
            try
            {
                var map = actions.FindActionMap("Player", true);
                var reload = map.FindAction("Reload");
                bool changed = false;
                if(reload == null) { reload = map.AddAction("Reload", InputActionType.Button); reload.expectedControlType = "Button"; changed = true; }
                if(!reload.bindings.Any(b => b.path == "<Keyboard>/r"))
                { reload.AddBinding("<Keyboard>/r", groups: "Keyboard&Mouse"); changed = true; }
                if(changed)
                {
                    File.WriteAllText(path, actions.ToJson());
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
            }
            finally { Object.DestroyImmediate(actions); }
            Debug.Log("[MR1] Reload action uses R; existing input references retained.");
        }

        public static AnimationClip ImportedClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        }

        [MenuItem("Blackout/Systems/Build Player Reload Animations")]
        public static void Build()
        {
            // Reapply the scoped animation rules even when these FBXs were previously imported as static models.
            AssetDatabase.ImportAsset(SmgModel, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RifleModel, ImportAssetOptions.ForceUpdate);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SmgModel) == null || AssetDatabase.LoadAssetAtPath<GameObject>(RifleModel) == null)
                throw new InvalidOperationException("Run Art/Blender/Scripts/build_reload_viewmodels.py first.");
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                BuildGun(root, "SMG", SmgModel, "AssaultRifle/HumanM@AssaultRifle_Reload01.fbx");
                BuildGun(root, "Rifle", RifleModel, "Rifle/HumanM@Rifle_Reload01.fbx");
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[RL1] Fitted soldier-pack reloads wired to the existing Player and ammo timers.");
        }

        private static void BuildGun(GameObject player, string name, string modelPath, string sourcePath)
        {
            Transform holder = player.transform.Find(ViewmodelBuilder.HolderPath);
            Transform gun = holder.Find(name);
            Transform normal = gun.Find("Visual");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            AnimationClip clip = ImportedClip(modelPath);
            AnimationClip original = ImportedClip(SourceFolder + sourcePath);
            if (normal == null || clip == null || original == null)
                throw new InvalidOperationException("Missing gun visual or imported reload clips for " + name);
            Transform reload = gun.Find("ReloadVisual");
            if (reload == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, gun);
                instance.name = "ReloadVisual";
                reload = instance.transform;
                reload.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                reload.localScale = Vector3.one;
            }
            foreach (Transform part in reload.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = player.layer;
            foreach (Renderer renderer in reload.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string materialName = materials[i].name.Split('.')[0];
                    materials[i] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Models/" + materialName + ".mat");
                    if (materials[i] == null) throw new InvalidOperationException("Missing existing material: " + materialName);
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            var animator = reload.GetComponentInChildren<Animator>(true);
            if (animator == null) throw new InvalidOperationException("Reload model has no Generic Animator: " + modelPath);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = false; // ViewmodelReload samples transform curves explicitly on the ammo clock.
            var component = gun.GetComponent<ViewmodelReload>();
            if (component == null) component = gun.gameObject.AddComponent<ViewmodelReload>();
            PlayerPrefabBuilder.Assign(component, "weapons", holder.GetComponent<WeaponController>());
            PlayerPrefabBuilder.Assign(component, "data", gun.GetComponent<HitscanWeapon>().Data);
            PlayerPrefabBuilder.Assign(component, "normalVisual", normal.gameObject);
            PlayerPrefabBuilder.Assign(component, "reloadVisual", reload.gameObject);
            PlayerPrefabBuilder.Assign(component, "animator", animator);
            PlayerPrefabBuilder.Assign(component, "clip", clip);
            PlayerPrefabBuilder.Assign(component, "sourceClip", original);
            reload.gameObject.SetActive(false);
            normal.gameObject.SetActive(true);
            Debug.Log($"[RL1] {name}: {clip.name}, {clip.length:0.000}s, source {original.name}.");
        }

        public static void BuildAndRenderPreview()
        {
            Build();
            RenderPreview();
        }

        /// <summary>Static actual-camera frames at start, magazine removal, insertion and return; no assets/scenes saved.</summary>
        public static void RenderPreview()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run reload review in a separate graphics-enabled batch.");
            string folder = Path.Combine(Path.GetTempPath(), "blackout-RL1-preview");
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath));
            Camera camera = player.GetComponentInChildren<Camera>(true);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.3f, 0.33f, 0.37f);
            camera.aspect = 16f / 9f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f);
            var light = new GameObject("Reload review light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            Transform holder = player.transform.Find(ViewmodelBuilder.HolderPath);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var target = new RenderTexture(1280, 720, 24);
            try
            {
                camera.targetTexture = target;
                foreach (string weapon in new[] { "SMG", "Rifle" })
                {
                    foreach (string other in new[] { "SMG", "Rifle", "Knife" }) holder.Find(other).gameObject.SetActive(other == weapon);
                    ViewmodelReload reload = holder.Find(weapon).GetComponent<ViewmodelReload>();
                    var skins = holder.Find(weapon + "/ReloadVisual").GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    // Edit-mode Camera.Render does not update GPU skinning. Draw BakeMesh copies of these actual poses.
                    var meshes = new Mesh[skins.Length];
                    var proxies = new GameObject[skins.Length];
                    for (int i = 0; i < skins.Length; i++)
                    {
                        meshes[i] = new Mesh();
                        proxies[i] = new GameObject("Reload review mesh", typeof(MeshFilter), typeof(MeshRenderer));
                        proxies[i].transform.SetParent(skins[i].transform, false);
                        proxies[i].GetComponent<MeshFilter>().sharedMesh = meshes[i];
                        proxies[i].GetComponent<MeshRenderer>().sharedMaterials = skins[i].sharedMaterials;
                        skins[i].enabled = false;
                    }
                    foreach (float progress in new[] { 0f, 0.15f, 0.28f, 0.45f, 0.6f, 0.78f, 1f })
                    {
                        reload.ApplyAt(progress);
                        Vector3 holderScale = holder.localScale;
                        holder.localScale = Vector3.one;
                        for (int i = 0; i < skins.Length; i++) skins[i].BakeMesh(meshes[i]);
                        holder.localScale = holderScale;
                        camera.Render();
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                        image.Apply();
                        File.WriteAllBytes(Path.Combine(folder, weapon.ToLowerInvariant() + "-" + Mathf.RoundToInt(progress * 100).ToString("00") + ".png"), image.EncodeToPNG());
                    }
                    for (int i = 0; i < skins.Length; i++)
                    {
                        Object.DestroyImmediate(proxies[i]);
                        Object.DestroyImmediate(meshes[i]);
                    }
                    reload.ResetVisual();
                }
            }
            finally
            {
                foreach (ViewmodelReload reload in player.GetComponentsInChildren<ViewmodelReload>(true)) reload.ResetVisual();
                RenderTexture.active = null;
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            Debug.Log("[RL1] Reload camera review: " + folder);
        }
    }
}
