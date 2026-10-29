// WA1: visual-only overrides on the existing Player. Original nested assets remain available.
using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Blackout.Weapons;

namespace Blackout.Editor
{
    public sealed class WinterArmsImporter : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => 110;

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(WinterArmsBuilder.ModelPrefix, StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = assetPath.EndsWith("_Smg.fbx", StringComparison.Ordinal) || assetPath.EndsWith("_Rifle.fbx", StringComparison.Ordinal);
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false;
            importer.skinWeights = ModelImporterSkinWeights.Standard;
        }
    }

    public static class WinterArmsBuilder
    {
        public const string ModelPrefix = "Assets/Models/Custom/SM_WinterArms_";
        private const string MaterialPath = "Assets/Materials/Models/M_PlayerWinter_Body.mat";

        [MenuItem("Blackout/Systems/Use Winter Soldier FPS Arms")]
        public static void Build()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) throw new InvalidOperationException("Missing existing cinematic body material.");
            foreach (string name in new[] { "Source", "Smg", "Rifle", "Knife" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefix + name + ".fbx") == null)
                    throw new InvalidOperationException("Run build_winter_arms.py before installing the arms.");

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                Transform holder = root.transform.Find(ViewmodelBuilder.HolderPath);
                foreach (var pair in new[] { ("SMG", "Smg"), ("Rifle", "Rifle"), ("Knife", "Knife") })
                {
                    Transform weapon = holder.Find(pair.Item1);
                    Transform normal = weapon.Find(pair.Item1 == "Knife" ? "Hand/Visual" : "Visual");
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefix + pair.Item2 + ".fbx");
                    if (pair.Item1 != "Knife") InstallReload(weapon, normal, model, pair.Item2, material);
                    foreach (string side in new[] { "L", "R" })
                    {
                        var source = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                            .Single(r => r.name == "Winter" + pair.Item2 + side);
                        Transform glove = Find(normal, "Glove" + side);
                        glove.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                        glove.GetComponent<MeshRenderer>().sharedMaterials = new[] { material };
                        // This mesh includes its own sleeve. Retain the old renderer as a reversible prefab override.
                        Find(normal, "Sleeve" + side).GetComponent<Renderer>().enabled = false;

                        if (pair.Item1 == "Knife") continue;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[WA1] Winter Soldier meshes/material installed. Camera, weapons, clips and cinematic actor retained.");
        }

        private static void InstallReload(Transform weapon, Transform normal, GameObject model, string label, Material body)
        {
            Transform legacy = weapon.Find("ReloadVisual");
            if (legacy != null)
            {
                // Keep the previous reload intact for rollback, including any earlier mesh-only installation.
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(label == "Smg" ? PlayerReloadBuilder.SmgModel : PlayerReloadBuilder.RifleModel);
                foreach (string side in new[] { "L", "R" })
                {
                    var source = Find(original.transform, "ReloadArm" + side).GetComponent<SkinnedMeshRenderer>();
                    var target = Find(legacy, source.name).GetComponent<SkinnedMeshRenderer>();
                    target.sharedMesh = source.sharedMesh;
                    target.bones = source.bones.Select(b => Find(legacy, b.name)).ToArray();
                    target.rootBone = Find(legacy, source.rootBone.name);
                    target.localBounds = source.localBounds;
                    target.sharedMaterials = new[] { AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Models/M_Weapon_Hands.mat") };
                }
                legacy.gameObject.SetActive(false);
            }
            Transform visual = weapon.Find("WinterReloadVisual");
            if (visual == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, weapon);
                instance.name = "WinterReloadVisual";
                visual = instance.transform;
            }
            foreach (Transform t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = weapon.gameObject.layer;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.name == "Winter" + label + "L" || renderer.name == "Winter" + label + "R"
                    ? new[] { body }
                    : renderer.sharedMaterials.Select(m => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Models/" + m.name.Split('.')[0] + ".mat")).ToArray();
                if (renderer.sharedMaterials.Any(m => m == null)) throw new InvalidOperationException("Missing weapon material.");
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
            }
            Animator animator = visual.GetComponentInChildren<Animator>(true);
            animator.enabled = false;
            animator.applyRootMotion = false;
            var component = weapon.GetComponent<ViewmodelReload>();
            PlayerPrefabBuilder.Assign(component, "reloadVisual", visual.gameObject);
            PlayerPrefabBuilder.Assign(component, "animator", animator);
            PlayerPrefabBuilder.Assign(component, "clip", PlayerReloadBuilder.ImportedClip(AssetDatabase.GetAssetPath(model)));
            visual.gameObject.SetActive(false);
            normal.gameObject.SetActive(true);
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        }

        public static void BuildAndPreview()
        {
            Build();
            ViewmodelBuilder.RenderPreview();
            RenderReloadPreview();
        }

        // Actual saved camera and sampled skinning; temporary BakeMesh proxies allow EditMode rendering.
        private static void RenderReloadPreview()
        {
            string folder = Path.Combine(Path.GetTempPath(), "blackout-WA1", "reload-preview");
            Directory.CreateDirectory(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath));
            Camera camera = player.GetComponentInChildren<Camera>(true);
            camera.enabled = false;
            camera.aspect = 16f / 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.gray;
            var light = new GameObject("Arm review light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            Transform holder = player.transform.Find(ViewmodelBuilder.HolderPath);
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            try
            {
                foreach (string gun in new[] { "SMG", "Rifle" })
                {
                    foreach (string name in new[] { "SMG", "Rifle", "Knife" }) holder.Find(name).gameObject.SetActive(name == gun);
                    var reload = holder.Find(gun).GetComponent<ViewmodelReload>();
                    var skins = holder.Find(gun + "/WinterReloadVisual").GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var meshes = skins.Select(s => new Mesh()).ToArray();
                    var proxies = skins.Select(s => new GameObject("Baked arm review", typeof(MeshFilter), typeof(MeshRenderer))).ToArray();
                    try
                    {
                        for (int i = 0; i < skins.Length; i++)
                        {
                            proxies[i].transform.SetParent(skins[i].transform, false);
                            proxies[i].GetComponent<MeshFilter>().sharedMesh = meshes[i];
                            proxies[i].GetComponent<MeshRenderer>().sharedMaterials = skins[i].sharedMaterials;
                            skins[i].enabled = false;
                        }
                        foreach (float t in new[] { 0f, .15f, .28f, .45f, .6f, .78f, 1f })
                        {
                            reload.ApplyAt(t);
                            Vector3 scale = holder.localScale;
                            holder.localScale = Vector3.one;
                            for (int i = 0; i < skins.Length; i++) skins[i].BakeMesh(meshes[i]);
                            holder.localScale = scale;
                            camera.Render();
                            RenderTexture.active = target;
                            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                            image.Apply();
                            File.WriteAllBytes(Path.Combine(folder, gun + "-" + Mathf.RoundToInt(t * 100) + ".png"), image.EncodeToPNG());
                        }
                    }
                    finally
                    {
                        foreach (var proxy in proxies) UnityEngine.Object.DestroyImmediate(proxy);
                        foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
                        reload.ResetVisual();
                    }
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            Debug.Log("[WA1] Reload review saved to " + folder);
        }
    }
}
