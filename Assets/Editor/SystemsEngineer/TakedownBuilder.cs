using System;
using System.Linq;
using Blackout.Agents;
using Blackout.Player;
using Blackout.Interaction;
using Blackout.Takedowns;
using Blackout.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Only owned actor/configuration and explicit components on existing gameplay prefabs are managed.</summary>
    public static class TakedownBuilder
    {
        public const string ProfilePath = "Assets/ScriptableObjects/Weapons/KnifeTakedown.asset";
        public const string FrontProfilePath = "Assets/ScriptableObjects/Weapons/KnifeFrontalTakedown.asset";
        public const float RearActivationRange = 3.5f;
        public const float FrontalStabSpeed = 3.5f;
        public const float FrontalSpacing = 1.1f;
        public const string ActorPath = "Assets/Prefabs/Player/TakedownActor.prefab";
        private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
        private const string VisualPath = "Assets/Prefabs/Models/Char_Patrol_Visual.prefab";
        private const string KnifePath = "Assets/Prefabs/Models/Weapon_Knife_Visual.prefab";
        private const string Label = "BlackoutGeneratedTakedownActor";

        [MenuItem("Blackout/Systems/Apply Rear Takedown Range")]
        public static void TuneRearRange()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TakedownProfile>(ProfilePath);
            if (profile == null) throw new InvalidOperationException("Build the rear takedown profile first.");
            profile.range = RearActivationRange;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RT1] Rear takedown range={profile.range:F1}m; animation spacing is preserved.");
        }

        [MenuItem("Blackout/Systems/Apply Quick Frontal Takedown Tuning")]
        public static void TuneFrontal()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TakedownProfile>(FrontProfilePath);
            if (profile == null) throw new InvalidOperationException("Build the frontal takedown profile first.");
            profile.stabSpeed = FrontalStabSpeed;
            profile.playerOffset = new Vector3(0f, 0f, FrontalSpacing);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TD1] Frontal spacing={FrontalSpacing:F2}m; stab speed={FrontalStabSpeed:F1}x; duration={profile.StabDuration:F3}s.");
        }

        [MenuItem("Blackout/Systems/Build Knife Takedown Support")]
        public static void Build()
        {
            TakedownProfile profile = AssetDatabase.LoadAssetAtPath<TakedownProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<TakedownProfile>();
                profile.range = RearActivationRange;
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            TakedownAnimationBuilder.AssignIfMissing(profile);
            TakedownProfile frontal = AssetDatabase.LoadAssetAtPath<TakedownProfile>(FrontProfilePath);
            if (frontal == null)
            {
                frontal = ScriptableObject.CreateInstance<TakedownProfile>();
                AssetDatabase.CreateAsset(frontal, FrontProfilePath);
            }
            TakedownAnimationBuilder.AssignFrontalIfMissing(frontal);
            BuildActor();
            BuildPlayer(profile);
            foreach (string name in new[] { "SecurityPatrol", "Hunter", "TacticalGuard", "Commander" }) BuildTarget(name);
            AssetDatabase.SaveAssets();
            Debug.Log("[TD1] Full-body soldier template, player coordinator and four enemy adapters wired. "
                + "Profile status: " + (profile.MissingRequirement ?? "Ready") + ". "
                + "Ordinary knife attacks remain available. Cinemachine is absent; temporary camera pose blending is used.");
        }

        private static void BuildActor()
        {
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath);
            if (saved != null && !AssetDatabase.GetLabels(saved).Contains(Label))
                throw new InvalidOperationException("Inspect the unlabelled actor before rebuilding: " + ActorPath);
            Scene preview = default;
            GameObject root;
            if (saved != null) root = PrefabUtility.LoadPrefabContents(ActorPath);
            else
            {
                preview = EditorSceneManager.NewPreviewScene();
                root = new GameObject("TakedownPlayer_SoldierTemplate");
                SceneManager.MoveGameObjectToScene(root, preview);
            }
            try
            {
                Transform previous = root.transform.Find("CinematicSoldier");
                Transform visual;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCinematicBodyBuilder.ModelPath) != null)
                {
                    if (previous != null)
                    {
                        previous.gameObject.SetActive(false);
                        foreach (TakedownActor old in previous.GetComponentsInChildren<TakedownActor>(true)) UnityEngine.Object.DestroyImmediate(old);
                    }
                    visual = Nested(root, "CinematicWinterSoldier", PlayerCinematicBodyBuilder.ModelPath);
                    visual.localScale = Vector3.one * 3f; // Original Biped model is about 0.61m tall; cinematic body is about 1.8m.
                    PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        Material body = AssetDatabase.LoadAssetAtPath<Material>(PlayerCinematicBodyBuilder.BodyMaterialPath);
                        Material hidden = AssetDatabase.LoadAssetAtPath<Material>(PlayerCinematicBodyBuilder.HiddenMaterialPath);
                        Material[] materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++) materials[i] = i >= 4 ? hidden : body;
                        renderer.sharedMaterials = materials;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                }
                else visual = Nested(root, "CinematicSoldier", VisualPath);
                Animator animator = visual.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                    throw new InvalidOperationException("The existing Patrol soldier must have its Humanoid avatar imported.");
                animator.transform.localRotation = Quaternion.identity; // AN1 facing correction for Humanoid playback.
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Agent_Humanoid.controller");
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Gun") child.gameObject.SetActive(false);
                Transform knife = Nested(root, "CinematicKnife", KnifePath);
                foreach (Renderer renderer in knife.GetComponentsInChildren<Renderer>(true))
                {
                    if (ViewmodelHands.IsArmPart(renderer.name)) renderer.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                var actor = animator.GetComponent<TakedownActor>();
                if (actor == null) actor = animator.gameObject.AddComponent<TakedownActor>();
                Assign(actor, "knife", knife);
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
                {
                    part.gameObject.layer = 6; // Player, excluded from weapon self hits.
                    GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                GameObject result = PrefabUtility.SaveAsPrefabAsset(root, ActorPath);
                AssetDatabase.SetLabels(result, new[] { Label });
            }
            finally
            {
                if (saved != null) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static Transform Nested(GameObject root, string name, string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing existing visual: " + path);
            Transform child = root.transform.Find(name);
            if (child == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
                child = instance.transform;
                child.SetParent(root.transform, false);
                child.name = name;
            }
            else if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) != prefab)
                throw new InvalidOperationException("Unexpected actor visual source: " + name);
            return child;
        }

        private static void BuildPlayer(TakedownProfile profile)
        {
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var controller = player.GetComponent<TakedownController>();
                if (controller == null) controller = player.AddComponent<TakedownController>();
                WeaponController weapons = player.GetComponentInChildren<WeaponController>(true);
                Assign(controller, "profile", profile);
                Assign(controller, "frontalProfile", AssetDatabase.LoadAssetAtPath<TakedownProfile>(FrontProfilePath));
                Assign(controller, "cinematicActorPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath));
                Assign(controller, "input", player.GetComponent<PlayerInputReader>());
                Assign(controller, "motor", player.GetComponent<PlayerMotor>());
                Assign(controller, "weapons", weapons);
                Assign(controller, "hands", player.GetComponentInChildren<ViewmodelHands>(true));
                Assign(controller, "playerCamera", player.GetComponentInChildren<Camera>(true));
                Assign(weapons, "takedown", controller);
                Behaviour[] pause = { player.GetComponent<PlayerMotor>(), player.GetComponent<PlayerLook>(),
                    player.GetComponentInChildren<PlayerCameraEffects>(true), player.GetComponentInChildren<PlayerInteractor>(true),
                    player.GetComponent<SniperScope>(), weapons };
                var serialized = new SerializedObject(controller);
                SerializedProperty array = serialized.FindProperty("suspendDuringTakedown");
                array.arraySize = pause.Length;
                for (int i = 0; i < pause.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = pause[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        private static void BuildTarget(string name)
        {
            string path = "Assets/Prefabs/Agents/" + name + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var target = root.GetComponent<AgentTakedownTarget>();
                if (target == null) target = root.AddComponent<AgentTakedownTarget>();
                AnimatorDriver driver = root.GetComponentsInChildren<AnimatorDriver>(true).Single(d => d.enabled);
                Assign(target, "actorAnimator", driver.GetComponent<Animator>());
                Assign(target, "agent", root.GetComponent<AgentRoot>());
                Assign(target, "mover", root.GetComponent<PathFollower>());
                Assign(target, "shooter", root.GetComponentInChildren<AgentShooter>(true));
                Assign(target, "driver", driver);
                Transform gun = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Gun");
                Transform supplied = gun != null ? gun.Find("SuppliedEnemyGun") : null;
                Assign(target, "disarmWeapon", gun);
                Transform carried = supplied != null ? supplied : gun;
                Renderer[] parts = carried != null ? carried.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                var serialized = new SerializedObject(target);
                SerializedProperty array = serialized.FindProperty("carriedWeapon");
                array.arraySize = parts.Length;
                for (int i = 0; i < parts.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Assign(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
