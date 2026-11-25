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
        public static void Build() => Install(null);

        [MenuItem("Blackout/Systems/Apply SMG Grip And Sights")]
        public static void BuildSmg() => Install("SMG");

        private static void Install(string selected)
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
                    if (selected != null && pair.Item1 != selected) continue;
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
                    if (pair.Item1 == "SMG") InstallSmgPose(root, weapon);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[WA1] Winter Soldier meshes/material installed. Camera, weapons, clips and cinematic actor retained.");
        }

        [Serializable] private class Grip
        {
            public Vector3 wrist, back, fingers, elbowDirection, contact, curl, indexCurl, thumbCurl;
            public Vector3 middleCurl, ringCurl, littleCurl;
            public float thumbOpposition, handScale, sleeveExtension;
        }
        [Serializable] private class SmgGrip
        {
            public Grip right, left;
            public Vector3 aimReference, hipPosition, hipEuler;
            public float eyeDistance, blendRate;
        }

        private static void InstallSmgPose(GameObject player, Transform smg)
        {
            var config = JsonUtility.FromJson<SmgGrip>(File.ReadAllText("Art/Blender/SmgGrip.json"));
            smg.SetLocalPositionAndRotation(config.hipPosition, Quaternion.Euler(config.hipEuler));
            foreach (var pair in new[] { ("RightHandGrip", config.right), ("LeftHandGrip", config.left) })
            {
                Transform target = Marker(smg, pair.Item1);
                target.SetLocalPositionAndRotation(pair.Item2.contact, Quaternion.LookRotation(pair.Item2.fingers, pair.Item2.back));
            }
            Transform sight = Marker(smg, "AimReference");
            sight.SetLocalPositionAndRotation(config.aimReference, Quaternion.identity);
            var pose = smg.GetComponent<SmgAimPose>();
            if (pose == null) pose = smg.gameObject.AddComponent<SmgAimPose>();
            PlayerPrefabBuilder.Assign(pose, "input", player.GetComponent<Blackout.Player.PlayerInputReader>());
            PlayerPrefabBuilder.Assign(pose, "motor", player.GetComponent<Blackout.Player.PlayerMotor>());
            PlayerPrefabBuilder.Assign(pose, "look", player.GetComponent<Blackout.Player.PlayerLook>());
            PlayerPrefabBuilder.Assign(pose, "health", player.GetComponent<Blackout.Shared.Health>());
            PlayerPrefabBuilder.Assign(pose, "weapons", smg.parent.GetComponent<WeaponController>());
            PlayerPrefabBuilder.Assign(pose, "data", smg.GetComponent<HitscanWeapon>().Data);
            PlayerPrefabBuilder.Assign(pose, "hands", smg.parent.GetComponent<ViewmodelHands>());
            PlayerPrefabBuilder.Assign(pose, "aimReference", sight);
            var fields = new SerializedObject(pose);
            fields.FindProperty("eyeDistance").floatValue = config.eyeDistance;
            fields.FindProperty("blendRate").floatValue = config.blendRate;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Marker(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;
            var made = new GameObject(name);
            made.layer = parent.gameObject.layer;
            made.transform.SetParent(parent, false);
            return made.transform;
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

        public static void BuildSmgAndPreview()
        {
            BuildSmg();
            ViewmodelBuilder.RenderPreview();
            RenderReloadPreview("SMG", Path.Combine(Path.GetTempPath(), "blackout-SG1", "reload-preview"));
            RenderAimPreview();
        }

        // Diagnostic cameras are temporary. These views inspect actual saved meshes, not grip-marker positions alone.
        public static void RenderGripViews()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath));
            Transform holder = player.transform.Find(ViewmodelBuilder.HolderPath), smg = holder.Find("SMG");
            foreach (string name in new[] { "SMG", "Rifle", "Knife" }) holder.Find(name).gameObject.SetActive(name == "SMG");
            foreach (ViewmodelReload reload in player.GetComponentsInChildren<ViewmodelReload>(true)) reload.ResetVisual();
            Camera camera = player.GetComponentInChildren<Camera>(true);
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.22f,.24f,.27f);
            camera.aspect = 16f/9f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.gray;
            var light = new GameObject("Grip inspection light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35,-40,0);
            string folder = Path.Combine(Path.GetTempPath(),"blackout-SG1-review");
            Directory.CreateDirectory(folder);
            var target = new RenderTexture(1280,720,24);
            var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            camera.targetTexture = target;
            try
            {
                var gun = Find(smg.Find("Visual"), "Smg").GetComponent<MeshFilter>();
                var collider = gun.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = gun.sharedMesh;
                Physics.SyncTransforms();
                string measurements = "Actual gun-space surface ray hits (metres):\n";
                foreach (float z in new[] { .14f,.15f,.26f,.27f,.28f })
                {
                    var ray = new Ray(smg.TransformPoint(new Vector3(-.3f,-.075f,z)), smg.right);
                    if (collider.Raycast(ray,out RaycastHit hit,1)) measurements += $"side at z={z:F3}: {smg.InverseTransformPoint(hit.point).ToString("F5")}\n";
                }
                var frontRay = new Ray(smg.TransformPoint(new Vector3(0,-.075f,.6f)),-smg.forward);
                if(collider.Raycast(frontRay,out RaycastHit front,1)) measurements += "front below barrel: " + smg.InverseTransformPoint(front.point).ToString("F5") + "\n";
                File.WriteAllText(Path.Combine(folder,"geometry-surfaces.txt"),measurements);
                var reload = smg.GetComponent<ViewmodelReload>();
                reload.ApplyAt(0);
                string bindReport = "Saved holding mesh versus sampled reload start, gun-local metres:\n";
                foreach (string side in new[] { "L", "R" })
                {
                    var normal = Find(smg.Find("Visual"), "Glove" + side).GetComponent<MeshFilter>();
                    var skin = Find(smg.Find("WinterReloadVisual"), "WinterSmg" + side).GetComponent<SkinnedMeshRenderer>();
                    var baked = new Mesh();
                    skin.BakeMesh(baked, true); // Compensate holder scale; TransformPoint applies it once below.
                    Vector3[] held = normal.sharedMesh.vertices, sampled = baked.vertices;
                    float error = 0;
                    for(int i=0;i<held.Length;i++) error = Mathf.Max(error, Vector3.Distance(
                        smg.InverseTransformPoint(normal.transform.TransformPoint(held[i])),
                        smg.InverseTransformPoint(skin.transform.TransformPoint(sampled[i]))));
                    bindReport += side + " maximum static/skinned vertex difference: " + error.ToString("F6") + "\n";
                    bindReport += "normal transform " + normal.transform.localToWorldMatrix + "skin transform " + skin.transform.localToWorldMatrix;
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                File.WriteAllText(Path.Combine(folder,"mesh-bind-space.txt"),bindReport);
                reload.ResetVisual();
                camera.transform.SetParent(null,true); // Moving the review camera must not move its child WeaponHolder.
                holder.SetParent(null,true);
                Renderer[] arms = smg.Find("Visual").GetComponentsInChildren<Renderer>(true).Where(r=>ViewmodelHands.IsArmPart(r.name)).ToArray();
                bool[] armVisibility = arms.Select(r=>r.enabled).ToArray();
                foreach (var view in new[] {
                    ("front", new Vector3(0,-.075f,.9f)),
                    ("left", new Vector3(-.7f,-.075f,.2f)),
                    ("right", new Vector3(.7f,-.075f,.2f)),
                    ("top", new Vector3(0,.65f,.2f)) })
                {
                    camera.transform.position = smg.TransformPoint(view.Item2);
                    camera.transform.rotation = Quaternion.LookRotation(smg.TransformPoint(new Vector3(0,-.075f,.2f))-camera.transform.position,
                        view.Item1 == "top" ? smg.forward : smg.up);
                    camera.orthographic = true;
                    camera.orthographicSize = .24f;
                    camera.nearClipPlane = .005f;
                    foreach(bool showArms in new[] {true,false})
                    {
                        for(int i=0;i<arms.Length;i++) arms[i].enabled = showArms && armVisibility[i];
                        camera.Render();
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0,0,1280,720),0,0);
                        image.Apply();
                        File.WriteAllBytes(Path.Combine(folder,view.Item1+(showArms ? "" : "-weapon")+".png"),image.EncodeToPNG());
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
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

        private static void RenderAimPreview()
        {
            RenderAimPreview(1280,720,"");
            RenderAimPreview(960,720,"-4x3");
            RenderAimPreview(1920,720,"-wide");
        }

        public static void RenderForestGripPreview()
        {
            RenderAimPreview(1280,720,"-16x9",true);
            RenderAimPreview(960,720,"-4x3",true);
            RenderAimPreview(1800,720,"-wide",true);
        }

        private static void RenderAimPreview(int width, int height, string suffix, bool forest = false)
        {
            if(forest) EditorSceneManager.OpenScene("Assets/Scenes/Environment.unity", OpenSceneMode.Single);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath));
            if(forest)
            {
                var position = new Vector3(1,6,-40);
                var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
                if(terrain != null) position.y = terrain.SampleHeight(position)+terrain.transform.position.y+.05f;
                player.transform.position = position;
            }
            Camera camera = player.GetComponentInChildren<Camera>(true);
            camera.enabled = false;
            camera.aspect = width/(float)height;
            camera.clearFlags = forest ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            if(!forest)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.gray;
                var light = new GameObject("SG1 review light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45,-30,0);
            }
            Transform holder = player.transform.Find(ViewmodelBuilder.HolderPath);
            foreach (string name in new[] { "SMG", "Rifle", "Knife" }) holder.Find(name).gameObject.SetActive(name == "SMG");
            var pose = holder.Find("SMG").GetComponent<SmgAimPose>();
            var target = new RenderTexture(width,height,24);
            var image = new Texture2D(width,height,TextureFormat.RGB24,false);
            camera.targetTexture = target;
            string folder = forest ? Path.Combine(Path.GetTempPath(), "blackout-SG1-finger-fit", "forest")
                : Path.Combine(Path.GetTempPath(), "blackout-SG1", "aim-preview");
            Directory.CreateDirectory(folder);
            try
            {
                foreach(float t in new[] { 0f, .5f, 1f })
                {
                    pose.Apply(t);
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0,0,width,height),0,0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(folder, "smg" + suffix + "-" + t + ".png"),image.EncodeToPNG());
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        // Actual saved camera and sampled skinning; temporary BakeMesh proxies allow EditMode rendering.
        private static void RenderReloadPreview(string selected = null, string output = null)
        {
            string folder = output ?? Path.Combine(Path.GetTempPath(), "blackout-WA1", "reload-preview");
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
                    if (selected != null && selected != gun) continue;
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
                            for (int i = 0; i < skins.Length; i++) skins[i].BakeMesh(meshes[i], true);
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
