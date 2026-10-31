// Owner: Agent Controller. Connect the imported Kevin Iglesias clips to the existing four agents (AN1).
using System;
using System.Linq;
using Blackout.Agents;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    public static class SoldierAnimationBuilder
    {
        public const string Source = "Assets/Kevin Iglesias/Human Animations/Animations/Male/";
        public const string WeaponLayer = "WeaponPose";

        public static AnimationClip ClipFor(string name)
        {
            string file = name switch
            {
                "Idle" => "Idles/HumanM@MilitaryIdle01.fbx",
                "Walk" => "Movement/Walk/HumanM@Walk01_Forward.fbx",
                "Run" => "Movement/Run/HumanM@Run01_Forward.fbx",
                "Aim_Idle" => "Combat/Rifle/HumanM@Rifle_Aim01.fbx",
                "HitReact" => "Combat/HumanM@Damage01.fbx",
                "Death" => "Combat/HumanM@Death01.fbx",
                "Shoot" => "Combat/Rifle/HumanM@Rifle_Aim01_Shoot01.fbx",
                "PistolAim" => "Combat/Gun/HumanM@Gun_Aim01.fbx",
                "PistolShoot" => "Combat/Gun/HumanM@Gun_Aim01_Shoot01.fbx",
                _ => null
            };
            return file == null ? null : AssetDatabase.LoadAllAssetsAtPath(Source + file).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
        }

        [MenuItem("Blackout/Agents/Apply Soldier Animations")]
        public static void Build()
        {
            const string hunterModel = "Assets/Models/Custom/SM_Char_HunterL4D.fbx";
            var hunterImporter = (ModelImporter)AssetImporter.GetAtPath(hunterModel);
            if (hunterImporter.animationType != ModelImporterAnimationType.Human)
            {
                hunterImporter.animationType = ModelImporterAnimationType.Human;
                hunterImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                hunterImporter.SaveAndReimport();
            }
            AnimatorControllerBuilder.Build();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerBuilder.ControllerPath);
            ConfigureController(controller);
            AnimatorOverrideController pistol = PistolController(controller);
            foreach (string name in new[] { "SecurityPatrol", "Hunter", "TacticalGuard", "Commander" })
                ConfigurePrefab(name, name == "Commander" || name == "TacticalGuard" ? pistol : controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[AN1] Soldier animations configured for SecurityPatrol, Hunter, TacticalGuard and Commander.");
        }

        private static void ConfigureController(AnimatorController controller)
        {
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = machine.states.First(s => s.state.name == "Locomotion").state;
            AnimatorState aim = machine.states.First(s => s.state.name == "Aim").state;
            // Moving while aiming still uses the locomotion tree; the masked layer poses the upper body.
            aim.motion = locomotion.motion;
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
                if (transition.destinationState.name == "HitReact" && !transition.conditions.Any(c => c.parameter == "Dead"))
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            if (!controller.parameters.Any(p => p.name == "Shoot")) controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            if (!controller.layers.Any(l => l.name == WeaponLayer))
            {
                var mask = new AvatarMask { name = "Soldier upper body" };
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                foreach (AvatarMaskBodyPart part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm,
                    AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers }) mask.SetHumanoidBodyPartActive(part, true);
                AssetDatabase.AddObjectToAsset(mask, controller);
                var weaponMachine = new AnimatorStateMachine { name = WeaponLayer };
                AssetDatabase.AddObjectToAsset(weaponMachine, controller);
                AnimatorState hold = weaponMachine.AddState("HoldWeapon");
                hold.motion = ClipFor("Aim_Idle");
                AnimatorState shoot = weaponMachine.AddState("Shoot");
                shoot.motion = ClipFor("Shoot");
                weaponMachine.defaultState = hold;
                var fire = weaponMachine.AddAnyStateTransition(shoot);
                fire.hasExitTime = false;
                fire.duration = 0.04f;
                fire.canTransitionToSelf = true;
                fire.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
                var settle = shoot.AddTransition(hold);
                settle.hasExitTime = true;
                settle.exitTime = 0.9f;
                settle.duration = 0.08f;
                controller.AddLayer(new AnimatorControllerLayer { name = WeaponLayer, stateMachine = weaponMachine,
                    avatarMask = mask, defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override });
            }
            EditorUtility.SetDirty(controller);
        }

        private static AnimatorOverrideController PistolController(AnimatorController controller)
        {
            const string path = "Assets/Animations/Agent_Sidearm.overrideController";
            var result = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (result == null)
            {
                result = new AnimatorOverrideController(controller) { name = "Agent_Sidearm" };
                AssetDatabase.CreateAsset(result, path);
            }
            result.runtimeAnimatorController = controller;
            result[ClipFor("Aim_Idle").name] = ClipFor("PistolAim");
            result[ClipFor("Shoot").name] = ClipFor("PistolShoot");
            EditorUtility.SetDirty(result);
            return result;
        }

        private static void ConfigurePrefab(string name, RuntimeAnimatorController controller)
        {
            string path = $"Assets/Prefabs/Agents/{name}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform visual = root.transform.Find("Visual");
                Transform model = AgentAvatarBuilder.Find(visual, "Model");
                if (model == null) throw new InvalidOperationException(name + ": missing Model.");
                // Humanoid retargeting supplies the downloaded rigs' facing correction. Their static
                // visual prefabs used a 180-degree turn, which would turn the animated body backwards.
                if (name == "SecurityPatrol" || name == "Hunter") model.localRotation = Quaternion.identity;
                foreach (ProceduralRobotAnimator old in root.GetComponentsInChildren<ProceduralRobotAnimator>(true))
                    old.enabled = false;
                Animator animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = null;
                if (name == "Hunter")
                    animator.avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Custom/SM_Char_HunterL4D.fbx").OfType<Avatar>().First();
                else if (name != "SecurityPatrol")
                {
                    animator.avatar = null;
                    animator.avatar = AgentAvatarBuilder.Build(model.gameObject, name);
                }
                if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException(name + ": no valid Humanoid avatar.");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                AnimatorDriver driver = model.GetComponent<AnimatorDriver>();
                if (driver == null) driver = model.gameObject.AddComponent<AnimatorDriver>();
                var serialized = new SerializedObject(driver);
                serialized.FindProperty("visualRoot").objectReferenceValue = visual;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AlignWeapon(root, animator, driver, name);
                if (AgentAvatarBuilder.Find(visual, EnemyGunBuilder.InstanceName) != null)
                    EnemyGunBuilder.ConfigureAgent(root, name);
                PrefabUtility.RecordPrefabInstancePropertyModifications(model);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[AN1] {name}: Humanoid={animator.isHuman}, avatar={animator.avatar.name}, model={model.name}.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AlignWeapon(GameObject root, Animator animator, AnimatorDriver driver, string name)
        {
            animator.Rebind();
            animator.SetFloat("Speed", 0f);
            animator.SetBool("Aiming", true);
            animator.SetLayerWeight(animator.GetLayerIndex(WeaponLayer), 1f);
            animator.Play("Aim", 0, 0f);
            animator.Update(0f);
            animator.Update(0.2f);
            Transform gun = AgentAvatarBuilder.Find(animator.transform, "Gun");
            if (gun == null) return; // robot's pod is already part of its right hand
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Quaternion rotation = Quaternion.LookRotation(root.transform.forward, root.transform.up);
            Vector3 grip = name == "Commander" ? new Vector3(0f, -0.02f, -0.04f) : new Vector3(0f, -0.085f, -0.02f);
            gun.SetPositionAndRotation(hand.position + root.transform.forward * 0.05f - rotation * grip, rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gun);
            if (name == "Commander")
            {
                var serialized = new SerializedObject(driver);
                serialized.FindProperty("weapon").objectReferenceValue = gun;
                serialized.FindProperty("weaponHand").objectReferenceValue = hand;
                serialized.FindProperty("weaponGripPosition").vector3Value = hand.InverseTransformPoint(gun.position);
                serialized.FindProperty("weaponGripRotation").quaternionValue = Quaternion.Inverse(hand.rotation) * gun.rotation;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
