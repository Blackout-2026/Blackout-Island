// Installs Nasser's Army Officer as the Commander's visual while retaining the existing combat brain.
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Blackout.Agents;
using Blackout.AI.Commander;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    public static class ArmyOfficerCommanderBuilder
    {
        private const string ModelPath = "Assets/Models/Custom/SM_Char_ArmyOfficer.fbx";
        private const string TextureFolder = "Assets/Textures/Models/ArmyOfficer/";
        private const string MaterialFolder = "Assets/Materials/Models/ArmyOfficer";
        private const string VisualPath = "Assets/Prefabs/Models/Char_ArmyOfficer_Visual.prefab";
        private const string CommanderPath = "Assets/Prefabs/Agents/Commander.prefab";
        private const string GunPath = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Soldier Animations/Prefabs/Weapons/Human_Gun.prefab";
        private const string ControllerPath = "Assets/Animations/Agent_Sidearm.overrideController";

        [MenuItem("Blackout/Agents/Install Army Officer Commander")]
        public static void Build()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>()
                .FirstOrDefault(a => a.isValid && a.isHuman);
            if (model == null || avatar == null)
                throw new InvalidOperationException("Army Officer FBX needs a valid Humanoid avatar.");
            var gun = AssetDatabase.LoadAssetAtPath<GameObject>(GunPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (gun == null || controller == null)
                throw new InvalidOperationException("The soldier animation package or sidearm controller is missing.");

            Material[] materials = BuildMaterials();
            Material gunMaterial = BuildGunMaterial(gun);
            GameObject visual = BuildVisual(model, avatar, gun, controller, materials, gunMaterial);
            InstallOnCommander(visual);
            AssetDatabase.SaveAssets();
            Debug.Log("Army Officer Commander installed with Humanoid sidearm animations, pistol and shooting muzzle.");
        }

        private static Material[] BuildMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Materials/Models", "ArmyOfficer");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            var materials = new Material[4];
            for (int i = 0; i < materials.Length; i++)
            {
                string prefix = TextureFolder + "Army_officer1_Packed" + i;
                string basePath = prefix + "_BaseColor.png";
                string normalPath = prefix + "_Normal.png";
                AssetDatabase.ImportAsset(basePath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
                var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
                {
                    normalImporter.textureType = TextureImporterType.NormalMap;
                    normalImporter.SaveAndReimport();
                }
                var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
                var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (baseMap == null || normalMap == null)
                    throw new InvalidOperationException("Missing Army Officer textures for atlas " + i);
                string path = MaterialFolder + "/M_ArmyOfficer_Packed" + i + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = "M_ArmyOfficer_Packed" + i };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", baseMap);
                material.SetTexture("_BumpMap", normalMap);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
                // The supplied atlas keeps cutout opacity in separate PNGs. It is packed into
                // the imported BaseColor alpha so eyelashes, facial hair and cloth edges stay clean.
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", .5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                EditorUtility.SetDirty(material);
                materials[i] = material;
            }
            return materials;
        }

        private static Material BuildGunMaterial(GameObject source)
        {
            const string path = MaterialFolder + "/M_ArmyOfficer_Pistol.mat";
            var original = source.GetComponentInChildren<Renderer>(true)?.sharedMaterial;
            if (original == null) throw new InvalidOperationException("Soldier animation pistol has no material.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { name = "M_ArmyOfficer_Pistol" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white);
            material.SetTexture("_BaseMap", original.HasProperty("_MainTex") ? original.GetTexture("_MainTex") : null);
            material.SetFloat("_Smoothness", .5f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildVisual(GameObject source, Avatar avatar, GameObject gunSource,
            RuntimeAnimatorController controller, Material[] materials, Material gunMaterial)
        {
            var root = new GameObject("Char_ArmyOfficer_Visual");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                model.name = "Model";
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localScale = Vector3.one;
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var assigned = renderer.sharedMaterials.Select(m =>
                    {
                        Match match = Regex.Match(m != null ? m.name : "", @"PackedMaterial([0-3])mat",
                            RegexOptions.IgnoreCase);
                        if (!match.Success) throw new InvalidOperationException("Unknown Army Officer material: " + m?.name);
                        return materials[int.Parse(match.Groups[1].Value)];
                    }).ToArray();
                    renderer.sharedMaterials = assigned;
                }

                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var driver = model.GetComponent<AnimatorDriver>() ?? model.AddComponent<AnimatorDriver>();
                var gun = (GameObject)PrefabUtility.InstantiatePrefab(gunSource, root.transform);
                gun.name = "Gun";
                foreach (Renderer renderer in gun.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => gunMaterial).ToArray();
                var muzzle = new GameObject("MuzzlePoint").transform;
                muzzle.SetParent(gun.transform, false);
                muzzle.localPosition = new Vector3(0f, 0f, .22f);

                // Fit the gun to the right hand in the actual sidearm aim animation.
                Transform[] bones = model.GetComponentsInChildren<Transform>(true);
                var rest = bones.Select(t => (t.localPosition, t.localRotation, t.localScale)).ToArray();
                animator.Rebind();
                animator.SetFloat("Speed", 0f);
                animator.SetBool("Aiming", true);
                int weaponLayer = animator.GetLayerIndex(SoldierAnimationBuilder.WeaponLayer);
                if (weaponLayer < 0) throw new InvalidOperationException("Sidearm controller has no weapon pose layer.");
                animator.SetLayerWeight(weaponLayer, 1f);
                animator.Play("Aim", 0, 0f);
                animator.Update(0f);
                animator.Update(.2f);
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand == null) throw new InvalidOperationException("Army Officer has no right hand bone.");
                gun.transform.SetPositionAndRotation(hand.position + model.transform.forward * .11f,
                    Quaternion.LookRotation(model.transform.forward, model.transform.up));
                var follow = new SerializedObject(driver);
                follow.FindProperty("visualRoot").objectReferenceValue = root.transform;
                follow.FindProperty("weapon").objectReferenceValue = gun.transform;
                follow.FindProperty("weaponHand").objectReferenceValue = hand;
                follow.FindProperty("weaponGripPosition").vector3Value = hand.InverseTransformPoint(gun.transform.position);
                follow.FindProperty("weaponGripRotation").quaternionValue =
                    Quaternion.Inverse(hand.rotation) * gun.transform.rotation;
                follow.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i].SetLocalPositionAndRotation(rest[i].localPosition, rest[i].localRotation);
                    bones[i].localScale = rest[i].localScale;
                }
                // Keep the saved prefab's muzzle in front of the body before its first Animator update.
                gun.transform.SetLocalPositionAndRotation(new Vector3(.24f, 1.35f, .55f), Quaternion.identity);
                PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
        }

        private static void InstallOnCommander(GameObject visualPrefab)
        {
            if (visualPrefab == null) throw new InvalidOperationException("Army Officer visual prefab was not saved.");
            var root = PrefabUtility.LoadPrefabContents(CommanderPath);
            try
            {
                Transform visualParent = root.transform.Find("Visual");
                if (visualParent == null) throw new InvalidOperationException("Commander has no Visual child.");
                for (int i = visualParent.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(visualParent.GetChild(i).gameObject);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, visualParent);
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;
                Transform muzzle = Find(visual.transform, "MuzzlePoint");
                Transform gun = Find(visual.transform, "Gun");
                var animator = visual.GetComponentInChildren<Animator>(true);
                var driver = visual.GetComponentInChildren<AnimatorDriver>(true);
                if (muzzle == null || gun == null || animator == null || driver == null)
                    throw new InvalidOperationException("Army Officer is missing its gun, muzzle or animation driver.");
                // Scale the entire Visual slot on death, including the separate pistol.
                SetReference(driver, "visualRoot", visualParent);
                SetReference(root.GetComponent<AgentShooter>(), "muzzle", muzzle);
                SetReference(root.GetComponent<CommanderBrain>(), "pistolMuzzle", muzzle);
                var takedown = new SerializedObject(root.GetComponent<AgentTakedownTarget>());
                takedown.FindProperty("actorAnimator").objectReferenceValue = animator;
                takedown.FindProperty("driver").objectReferenceValue = driver;
                takedown.FindProperty("disarmWeapon").objectReferenceValue = gun;
                Renderer[] renderers = gun.GetComponentsInChildren<Renderer>(true);
                var carried = takedown.FindProperty("carriedWeapon");
                carried.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++)
                    carried.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                takedown.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CommanderPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void SetReference(Object component, string name, Object value)
        {
            if (component == null) throw new InvalidOperationException("Commander is missing " + name + " owner.");
            var serialized = new SerializedObject(component);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Find(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform result = Find(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
