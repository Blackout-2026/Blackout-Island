using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blackout.Agents;
using Blackout.Takedowns;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>Imports the user-downloaded Mixamo pair; never edits the downloaded source files.</summary>
    public static class TakedownAnimationBuilder
    {
        public const string Folder = "Assets/Animations/Takedowns";
        public const string AttackPath = Folder + "/StealthAssassination_Attacker.fbx";
        public const string VictimPath = Folder + "/StealthAssassination_Victim.fbx";
        public const string KickPath = Folder + "/Takedown_Kick.fbx";
        public const string StabPath = Folder + "/Takedown_DoubleStab.fbx";

        [MenuItem("Blackout/Systems/Import Downloaded Takedown Animations")]
        public static void Import()
        {
            PlayerCinematicBodyBuilder.Build();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Animations", "Takedowns");
            ImportClip("Stealth Assassination (1).fbx", AttackPath, "StealthAssassination_Attacker");
            ImportClip("Stealth Assassination.fbx", VictimPath, "StealthAssassination_Victim");
            ImportClip("Kicking.fbx", KickPath, "Takedown_Kick");
            ImportClip("Stabbing.fbx", StabPath, "Takedown_DoubleStab");
            TakedownBuilder.Build();
            FitActorKnife();
            AssetDatabase.SaveAssets();
            Debug.Log("[TD1] Downloaded Mixamo pair imported as non-looping Humanoid clips.");
        }

        private static void ImportClip(string sourceName, string path, string clipName)
        {
            if (!File.Exists(path))
            {
                string source = Path.Combine(@"D:\model\knife-takedown", sourceName);
                if (!File.Exists(source)) throw new FileNotFoundException("Download the original Mixamo FBX first.", source);
                File.Copy(source, path); // New asset only. Existing Unity assets and GUIDs are retained.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = importer.importLights = importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            ModelImporterClipAnimation clip = importer.defaultClipAnimations.Single();
            clip.name = clipName;
            clip.loopTime = clip.loopPose = false;
            clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            if (path == StabPath) clip.firstFrame = 6f; // Remove the initial already-extended arm pose; the clip contains TWO thrusts.
            float seconds = (clip.lastFrame - clip.firstFrame) / 30f; // Both downloaded sources are 30 FPS.
            // ModelImporter event times are normalized; AnimationClip.events exposes seconds after import.
            clip.events = path == AttackPath ? new[]
            {
                new AnimationEvent { functionName = "TakedownSound", time = 1.8f / seconds },
                new AnimationEvent { functionName = "TakedownImpact", time = 2.1f / seconds }
            } : path == KickPath ? new[] { new AnimationEvent { functionName = "TakedownKick", time = 0.97f / seconds } }
            : path == StabPath ? new[]
            {
                new AnimationEvent { functionName = "TakedownStabSound", time = 1f / seconds, intParameter = 1 },
                new AnimationEvent { functionName = "TakedownStab", time = 1f / seconds, intParameter = 1 },
                new AnimationEvent { functionName = "TakedownStabSound", time = 2.4f / seconds, intParameter = 2 },
                new AnimationEvent { functionName = "TakedownStab", time = 2.4f / seconds, intParameter = 2 }
            } : new AnimationEvent[0];
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
            Animator animator = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("Invalid Humanoid avatar: " + path);
            if (Mathf.Abs(LoadClip(path).length - seconds) > 0.04f)
                throw new InvalidOperationException("The imported clip duration differs from the downloaded motion: " + path);
            Debug.Log($"[TD1] {clipName}: frames {clip.firstFrame}..{clip.lastFrame}, length {LoadClip(path).length:F3}s.");
        }

        public static AnimationClip LoadClip(string path) => AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));

        public static void AssignIfMissing(TakedownProfile profile)
        {
            if (profile.playerAttack != null || profile.victimReaction != null || !File.Exists(AttackPath) || !File.Exists(VictimPath)) return;
            profile.playerAttack = LoadClip(AttackPath);
            profile.victimReaction = LoadClip(VictimPath);
            profile.playerOffset = new Vector3(0f, 0f, -1.85f);
            profile.forwardClearance = 2.1f;
            profile.cameraOffset = new Vector3(2.5f, 1.8f, 1.7f);
            profile.cameraEndOffset = new Vector3(2f, 1.6f, 2.5f);
            profile.focusHeight = 0.95f;
            profile.fieldOfView = 60f;
            EditorUtility.SetDirty(profile);
        }

        public static void AssignFrontalIfMissing(TakedownProfile profile)
        {
            if (profile.playerAttack != null || !File.Exists(KickPath) || !File.Exists(StabPath)) return;
            const string combat = "Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/";
            profile.style = TakedownStyle.Frontal;
            profile.kick = LoadClip(KickPath);
            profile.playerAttack = LoadClip(StabPath);
            profile.victimIdle = LoadClip(combat + "Rifle/HumanM@Rifle_Aim01.fbx");
            profile.victimReaction = LoadClip(combat + "HumanM@Damage01.fbx");
            profile.victimDeath = LoadClip(combat + "HumanM@Death01.fbx");
            profile.playerOffset = new Vector3(0f, 0f, TakedownBuilder.FrontalSpacing);
            profile.stabSpeed = TakedownBuilder.FrontalStabSpeed;
            profile.playerYaw = 180f;
            profile.forwardClearance = 0.8f;
            profile.cameraOffset = new Vector3(2.3f, 1.5f, 0.6f);
            profile.cameraEndOffset = new Vector3(2f, 1.4f, -0.7f);
            profile.focusHeight = 1f;
            profile.fieldOfView = 55f;
            EditorUtility.SetDirty(profile);
        }

        private static void FitActorKnife()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var sample = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TakedownBuilder.ActorPath), scene);
            var graph = PlayableGraph.Create("Knife grip fitting");
            try
            {
                Animator animator = sample.GetComponentInChildren<Animator>();
                AnimationClipPlayable playable = Bind(graph, animator, LoadClip(AttackPath));
                graph.Play(); playable.SetTime(2.1f); graph.Evaluate(0f);
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
                Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
                Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                if (index == null || little == null || middle == null) return;
                Vector3 blade = (little.position - index.position).normalized; // Reverse grip, blade away from the forearm.
                Quaternion rotation = Quaternion.LookRotation(-blade, (middle.position - hand.position).normalized);
                // The existing knife points along local -Z; its handle centre is at local +Z 0.03m.
                Vector3 position = Vector3.Lerp(hand.position, middle.position, 0.8f) - rotation * new Vector3(0f, 0f, 0.03f);
                GameObject root = PrefabUtility.LoadPrefabContents(TakedownBuilder.ActorPath);
                try
                {
                    var data = new SerializedObject(root.GetComponentInChildren<TakedownActor>());
                    data.FindProperty("knifePosition").vector3Value = hand.InverseTransformPoint(position);
                    data.FindProperty("knifeEuler").vector3Value = (Quaternion.Inverse(hand.rotation) * rotation).eulerAngles;
                    graph.Destroy();
                    graph = PlayableGraph.Create("Frontal knife grip fitting");
                    playable = Bind(graph, animator, LoadClip(StabPath));
                    graph.Play(); playable.SetTime(1f); graph.Evaluate(0f);
                    blade = (index.position - little.position).normalized; // Forward grip for the two torso thrusts.
                    rotation = Quaternion.LookRotation(-blade, (middle.position - hand.position).normalized);
                    position = Vector3.Lerp(hand.position, middle.position, 0.8f) - rotation * new Vector3(0f, 0f, 0.03f);
                    data.FindProperty("frontalKnifePosition").vector3Value = hand.InverseTransformPoint(position);
                    data.FindProperty("frontalKnifeEuler").vector3Value = (Quaternion.Inverse(hand.rotation) * rotation).eulerAngles;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, TakedownBuilder.ActorPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            finally { graph.Destroy(); EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void ImportAndPreview() { Import(); Preview(); }

        // Unsaved scene, images/logs outside the repository. Requires a graphics-enabled batch run.
        public static void Preview()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run this preview in a graphics-enabled batch process with the Editor closed; it uses an unsaved review scene.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string output = Path.Combine(Path.GetTempPath(), "blackout-TD1", "paired-preview");
            Directory.CreateDirectory(output);
            var light = new GameObject("Pair review light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.gray;
            var camera = new GameObject("Pair review camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(3f, 1.8f, 3.5f);
            camera.transform.LookAt(new Vector3(0f, 0.9f, 0.2f));
            camera.orthographic = true;
            camera.orthographicSize = 1.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.25f, 0.29f, 0.34f);
            var render = new RenderTexture(1200, 800, 24);
            var pixels = new Texture2D(1200, 800, TextureFormat.RGB24, false);
            camera.targetTexture = render;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Review floor";
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(10f, 0.2f, 10f);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TakedownBuilder.ActorPath));
            var profile = AssetDatabase.LoadAssetAtPath<TakedownProfile>(TakedownBuilder.ProfilePath);
            player.transform.position = profile.playerOffset;
            Animator attacker = player.GetComponentInChildren<TakedownActor>().GetComponent<Animator>();
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Agents/SecurityPatrol.prefab"));
            Animator victim = enemy.GetComponent<AgentTakedownTarget>().Animator;
            var graph = PlayableGraph.Create("Takedown review");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationClipPlayable a = Bind(graph, attacker, LoadClip(AttackPath));
            AnimationClipPlayable b = Bind(graph, victim, LoadClip(VictimPath));
            graph.Play();
            var report = new List<string>();
            try
            {
                foreach (float time in new[] { 0f, 1f, 1.5f, 1.8f, 2f, 2.5f, 3f, 4f, 5f, 6f, 6.9f })
                {
                    a.SetTime(time); b.SetTime(time); graph.Evaluate(0f);
                    FitKnife(player, attacker);
                    Transform gun = enemy.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Gun");
                    if (gun != null) gun.gameObject.SetActive(false);
                    foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                    foreach (SkinnedMeshRenderer skin in enemy.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;
                    // A batch Editor preview renders several poses in one frame. Bake each skin so
                    // GPU skinning cannot reuse the first render's bone matrices for later samples.
                    var baked = new List<(SkinnedMeshRenderer skin, GameObject copy, Mesh mesh)>();
                    foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Concat(enemy.GetComponentsInChildren<SkinnedMeshRenderer>()).Where(s => s.enabled))
                    {
                        var mesh = new Mesh();
                        skin.BakeMesh(mesh, true);
                        var copy = new GameObject("Baked review skin", typeof(MeshFilter), typeof(MeshRenderer));
                        copy.transform.SetParent(skin.transform, false);
                        copy.GetComponent<MeshFilter>().sharedMesh = mesh;
                        copy.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                        skin.enabled = false;
                        baked.Add((skin, copy, mesh));
                    }
                    camera.Render();
                    RenderTexture.active = render;
                    pixels.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, $"Patrol-{time:F2}.png"), pixels.EncodeToPNG());
                    foreach (var item in baked)
                    {
                        item.skin.enabled = true;
                        Object.DestroyImmediate(item.copy); Object.DestroyImmediate(item.mesh);
                    }
                    report.Add($"t={time:F2} attacker hips={Bone(attacker, HumanBodyBones.Hips)} hand={Bone(attacker, HumanBodyBones.RightHand)}; victim hips={Bone(victim, HumanBodyBones.Hips)} head={Bone(victim, HumanBodyBones.Head)}");
                }
                File.WriteAllLines(Path.Combine(output, "bones.txt"), report);
            }
            finally
            {
                graph.Destroy();
                RenderTexture.active = null; camera.targetTexture = null;
                Object.DestroyImmediate(render); Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(player); Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(floor); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(light.gameObject);
            }
            Debug.Log("[TD1] Actual paired animation review: " + output);
        }

        private static Vector3 Bone(Animator animator, HumanBodyBones bone) => animator.GetBoneTransform(bone).position;

        private static AnimationClipPlayable Bind(PlayableGraph graph, Animator animator, AnimationClip clip)
        {
            animator.Rebind();
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var playable = AnimationClipPlayable.Create(graph, clip);
            var output = AnimationPlayableOutput.Create(graph, animator.name, animator);
            output.SetSourcePlayable(playable);
            return playable;
        }

        private static void FitKnife(GameObject player, Animator animator)
        {
            var data = new SerializedObject(player.GetComponentInChildren<TakedownActor>());
            var knife = (Transform)data.FindProperty("knife").objectReferenceValue;
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            knife.SetPositionAndRotation(hand.TransformPoint(data.FindProperty("knifePosition").vector3Value),
                hand.rotation * Quaternion.Euler(data.FindProperty("knifeEuler").vector3Value));
        }
    }
}
