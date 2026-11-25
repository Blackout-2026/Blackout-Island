// Owner: Agent Controller. Installs the user's rigged Blender Commander and pistol.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Blackout.Agents;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    public static class CommanderHeroBuilder
    {
        private const string ModelPath = "Assets/Models/Custom/SM_Char_CommanderHero.fbx";
        private const string VisualPath = "Assets/Prefabs/Models/Char_CommanderHero_Visual.prefab";
        private const string CommanderPath = "Assets/Prefabs/Agents/Commander.prefab";
        private const string ControllerPath = "Assets/Animations/Commander_Hero.controller";
        private const string MaterialFolder = "Assets/Materials/Models/CommanderHero";
        private const string FacePath = "Assets/Textures/Models/T_CommanderHero_Face.png";
        private const string HairPath = "Assets/Textures/Models/T_CommanderHero_Hair.png";

        [MenuItem("Blackout/Agents/Install Supplied Commander Model")]
        public static void Build()
        {
            ImportRig();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("Commander FBX did not import: " + ModelPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
            AnimationClip Clip(string name) => clips.FirstOrDefault(c =>
                c.name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                c.name.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase));
            foreach (string name in new[] { "Idle", "Walk", "Run", "Aim", "Shoot" })
                if (Clip(name) == null)
                    throw new InvalidOperationException("Commander FBX is missing the " + name + " action. Imported: " +
                        string.Join(", ", clips.Select(c => c.name)));

            var controller = BuildController(Clip("Idle"), Clip("Walk"), Clip("Run"), Clip("Aim"), Clip("Shoot"));
            var visual = BuildVisual(model, controller);
            InstallOnCommander(visual);
            AssetDatabase.SaveAssets();
            Debug.Log("Commander Hero installed: supplied model, five animations, pistol, 180 HP and cover combat.");
        }

        private static void ImportRig()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing Commander ModelImporter.");
            if (importer.animationType != ModelImporterAnimationType.Generic || !importer.importAnimation ||
                importer.optimizeGameObjects)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.optimizeGameObjects = false; // muzzle and pistol bones must remain addressable
                importer.SaveAndReimport();
            }
            AssetDatabase.ImportAsset(FacePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(HairPath, ImportAssetOptions.ForceUpdate);
        }

        private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk,
            AnimationClip run, AnimationClip aim, AnimationClip shoot)
        {
            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing; // preserve later animation tuning
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Aiming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = machine.AddState("Locomotion");
            var tree = new BlendTree { name = "Commander Speed", blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 1.5f);
            tree.AddChild(run, 4f);
            locomotion.motion = tree;
            machine.defaultState = locomotion;
            AnimatorState aiming = machine.AddState("Aim");
            aiming.motion = aim;
            AnimatorState firing = machine.AddState("Shoot");
            firing.motion = shoot;
            AnimatorState dead = machine.AddState("Death");

            AddTransition(locomotion.AddTransition(aiming), "Aiming", AnimatorConditionMode.If);
            AddTransition(aiming.AddTransition(locomotion), "Aiming", AnimatorConditionMode.IfNot);
            var fire = machine.AddAnyStateTransition(firing);
            fire.hasExitTime = false;
            fire.canTransitionToSelf = false;
            fire.duration = 0.04f;
            fire.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
            var backToAim = firing.AddTransition(aiming);
            backToAim.hasExitTime = true;
            backToAim.exitTime = 0.9f;
            backToAim.duration = 0.05f;
            backToAim.AddCondition(AnimatorConditionMode.If, 0f, "Aiming");
            var backToMove = firing.AddTransition(locomotion);
            backToMove.hasExitTime = true;
            backToMove.exitTime = 0.9f;
            backToMove.duration = 0.05f;
            backToMove.AddCondition(AnimatorConditionMode.IfNot, 0f, "Aiming");
            var die = machine.AddAnyStateTransition(dead);
            die.hasExitTime = false;
            die.duration = 0.08f;
            die.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddTransition(AnimatorStateTransition transition, string parameter,
            AnimatorConditionMode condition)
        {
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            transition.AddCondition(condition, 0f, parameter);
        }

        private static GameObject BuildVisual(GameObject source, RuntimeAnimatorController controller)
        {
            var root = new GameObject("Char_CommanderHero_Visual");
            try
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                model.name = "Model";
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localScale = Vector3.one;
                Animator animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
                if (avatar != null) animator.avatar = avatar;
                AnimatorDriver driver = model.GetComponent<AnimatorDriver>() ?? model.AddComponent<AnimatorDriver>();
                var data = new SerializedObject(driver);
                data.FindProperty("visualRoot").objectReferenceValue = root.transform;
                data.ApplyModifiedPropertiesWithoutUndo();
                ApplyMaterials(model);
                var muzzle = Find(root.transform, "muzzle");
                if (muzzle == null) throw new InvalidOperationException("Imported Commander has no muzzle bone.");
                if (Find(root.transform, "Pistol") == null)
                    throw new InvalidOperationException("Imported Commander has no pistol mesh.");
                PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
        }

        private static void ApplyMaterials(GameObject model)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Materials/Models"))
                    AssetDatabase.CreateFolder("Assets/Materials", "Models");
                AssetDatabase.CreateFolder("Assets/Materials/Models", "CommanderHero");
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            var materialCache = new Dictionary<string, Material>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] sourceMaterials = renderer.sharedMaterials;
                var assigned = new Material[sourceMaterials.Length];
                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    string sourceName = sourceMaterials[i] != null ? sourceMaterials[i].name : "Unknown";
                    if (!materialCache.TryGetValue(sourceName, out Material material))
                    {
                        string safe = Regex.Replace(sourceName, "[^a-zA-Z0-9_-]+", "_").Trim('_');
                        string path = MaterialFolder + "/" + safe + ".mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material == null)
                        {
                            material = new Material(shader) { name = safe };
                            AssetDatabase.CreateAsset(material, path);
                        }
                        ConfigureMaterial(material, sourceName);
                        materialCache.Add(sourceName, material);
                    }
                    assigned[i] = material;
                }
                renderer.sharedMaterials = assigned;
            }
        }

        private static void ConfigureMaterial(Material material, string name)
        {
            string lower = name.ToLowerInvariant();
            string color = "#222125";
            float roughness = 0.7f;
            string number = name.Length >= 2 ? name.Substring(0, 2) : "";
            switch (number)
            {
                case "01": color = "#3B393D"; roughness = .83f; break;
                case "02": color = "#333036"; roughness = .87f; break;
                case "03": color = "#603038"; roughness = .77f; break;
                case "04": color = "#522530"; roughness = .71f; break;
                case "05": color = "#4D4947"; roughness = .44f; break;
                case "06": color = "#66605C"; roughness = .43f; break;
                case "07": color = "#996A40"; roughness = .39f; break;
                case "08": color = "#483329"; roughness = .57f; break;
                case "09": color = "#5E4335"; roughness = .56f; break;
                case "10": color = "#22201E"; roughness = .82f; break;
                case "11": color = "#806044"; roughness = .8f; break;
                case "12": color = "#AF7760"; roughness = .62f; break;
                case "13": color = "#9A6B5B"; roughness = .67f; break;
                case "15": color = "#2D2521"; roughness = .77f; break;
                case "17": color = "#3A2C27"; roughness = .63f; break;
                case "21": color = "#454246"; roughness = .39f; break;
                case "22": color = "#303134"; roughness = .5f; break;
                case "23": color = "#292524"; roughness = .66f; break;
                case "24": color = "#101012"; roughness = .48f; break;
            }
            ColorUtility.TryParseHtmlString(color, out Color tint);
            Texture2D texture = null;
            if (lower.Contains("portrait")) texture = AssetDatabase.LoadAssetAtPath<Texture2D>(FacePath);
            else if (lower.Contains("packed albedo") && lower.Contains("hair"))
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(HairPath);
            if (texture != null) tint = Color.white;
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 1f - roughness);
            EditorUtility.SetDirty(material);
        }

        private static void InstallOnCommander(GameObject visualPrefab)
        {
            if (visualPrefab == null) throw new InvalidOperationException("Commander visual prefab is missing.");
            GameObject root = PrefabUtility.LoadPrefabContents(CommanderPath);
            try
            {
                Transform visualParent = root.transform.Find("Visual");
                if (visualParent == null) throw new InvalidOperationException("Commander has no Visual child.");
                for (int i = visualParent.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(visualParent.GetChild(i).gameObject);
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, visualParent);
                // The authored character faces local -Z; AgentRoot and PathFollower face +Z.
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
                visual.transform.localScale = Vector3.one;

                var shooter = root.GetComponent<AgentShooter>();
                var brain = root.GetComponent<Blackout.AI.Commander.CommanderBrain>();
                var health = root.GetComponent<Health>();
                var takedown = root.GetComponent<AgentTakedownTarget>();
                if (shooter == null || brain == null || health == null || takedown == null)
                    throw new InvalidOperationException("Commander is missing shooter, brain, health or takedown component.");
                Transform muzzle = Find(visual.transform, "muzzle");
                if (muzzle == null) throw new InvalidOperationException("Commander pistol muzzle is missing.");
                var shooterData = new SerializedObject(shooter);
                shooterData.FindProperty("muzzle").objectReferenceValue = muzzle;
                shooterData.FindProperty("config.damage").floatValue = 10f;
                shooterData.FindProperty("config.range").floatValue = 22f;
                shooterData.FindProperty("config.burstCount").intValue = 1;
                shooterData.FindProperty("config.burstInterval").floatValue = .3f;
                shooterData.FindProperty("config.burstPause").floatValue = .35f;
                shooterData.FindProperty("config.spread").floatValue = .025f;
                shooterData.FindProperty("config.reactionDelay").floatValue = .45f;
                shooterData.ApplyModifiedPropertiesWithoutUndo();
                var brainData = new SerializedObject(brain);
                brainData.FindProperty("pistolMuzzle").objectReferenceValue = muzzle;
                brainData.ApplyModifiedPropertiesWithoutUndo();
                var healthData = new SerializedObject(health);
                healthData.FindProperty("maxHealth").floatValue = 180f;
                healthData.ApplyModifiedPropertiesWithoutUndo();

                Animator animator = visual.GetComponentInChildren<Animator>(true);
                AnimatorDriver driver = visual.GetComponentInChildren<AnimatorDriver>(true);
                Renderer pistol = visual.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(r => r.name.IndexOf("Pistol", StringComparison.OrdinalIgnoreCase) >= 0);
                if (animator == null || driver == null || pistol == null)
                    throw new InvalidOperationException("Commander visual is missing animation or pistol renderer.");
                var takedownData = new SerializedObject(takedown);
                takedownData.FindProperty("actorAnimator").objectReferenceValue = animator;
                takedownData.FindProperty("driver").objectReferenceValue = driver;
                var weapons = takedownData.FindProperty("carriedWeapon");
                weapons.arraySize = 1;
                weapons.GetArrayElementAtIndex(0).objectReferenceValue = pistol;
                takedownData.FindProperty("disarmWeapon").objectReferenceValue = null;
                takedownData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CommanderPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return root;
            foreach (Transform child in root)
            {
                Transform found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
