// Owner: Agent Controller. Enemy-only replacement of the supplied gun (EG1).
using System;
using System.IO;
using System.Linq;
using System.Text;
using Blackout.Agents;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    public static class EnemyGunBuilder
    {
        public static readonly string[] AgentNames = { "SecurityPatrol", "Hunter", "Commander" };
        public const string VisualPath = "Assets/Prefabs/Models/EnemyGun_Visual.prefab";
        public const string ModelPath = "Assets/Models/Custom/SM_Weapon_EnemyGun.fbx";
        public const string InstanceName = "SuppliedEnemyGun";

        [MenuItem("Blackout/Agents/Apply Supplied Enemy Gun")]
        public static void Build()
        {
            BuildVisual();
            foreach (string name in AgentNames)
            {
                string path = $"Assets/Prefabs/Agents/{name}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigureAgent(root, name);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[EG1] Supplied rifle connected to Patrol, Hunter and Commander; Tactical Guard is excluded.");
        }

        [MenuItem("Blackout/Agents/Restore Tactical Guard Original Gun")]
        public static void RestoreTacticalGuard()
        {
            const string path = "Assets/Prefabs/Agents/TacticalGuard.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var driver = root.GetComponentsInChildren<AnimatorDriver>(true).Single(d => d.enabled);
                Animator animator = driver.GetComponent<Animator>();
                Transform addedGun = animator.transform.Find("Gun");
                if (addedGun != null)
                {
                    if (addedGun.Find(InstanceName) == null)
                        throw new InvalidOperationException("Tactical Guard has an unmanaged Gun; refusing to remove it.");
                    UnityEngine.Object.DestroyImmediate(addedGun.gameObject);
                }
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var originalGun = new SerializedObject(hand.GetComponent<Renderer>());
                PrefabUtility.RevertPropertyOverride(originalGun.FindProperty("m_Enabled"), InteractionMode.AutomatedAction);
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Agent_Sidearm.overrideController");
                var follow = new SerializedObject(driver);
                follow.FindProperty("weapon").objectReferenceValue = null;
                follow.FindProperty("weaponHand").objectReferenceValue = null;
                follow.FindProperty("weaponGripPosition").vector3Value = Vector3.zero;
                follow.FindProperty("weaponGripRotation").quaternionValue = Quaternion.identity;
                follow.ApplyModifiedPropertiesWithoutUndo();
                Transform muzzle = AgentAvatarBuilder.Find(hand, "MuzzlePoint");
                if (muzzle == null) throw new InvalidOperationException("Original Tactical Guard muzzle is missing.");
                Assign(root.GetComponent<AgentShooter>(), "muzzle", muzzle);
                var target = new SerializedObject(root.GetComponent<AgentTakedownTarget>());
                target.FindProperty("disarmWeapon").objectReferenceValue = null;
                target.FindProperty("carriedWeapon").arraySize = 0;
                target.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[EG2] Tactical Guard original integrated gun, muzzle and sidearm animations restored.");
        }

        private static void BuildVisual()
        {
            foreach (string part in new[] { "Body", "Scope", "Grip" })
            {
                string path = $"Assets/Materials/Models/M_EnemyGun_{part}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                string prefix = $"Assets/Textures/Models/T_EnemyGun_{part}_";
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", Texture(prefix + "BaseColor.png"));
                material.SetTexture("_BumpMap", Texture(prefix + "Normal.png"));
                Texture2D packed = Texture(prefix + "MetalSmooth.png");
                material.SetTexture("_MetallicGlossMap", packed);
                material.SetTexture("_OcclusionMap", packed); // Source AO is packed in G.
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_BumpScale", 1f);
                material.SetFloat("_OcclusionStrength", 1f);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
                EditorUtility.SetDirty(material);
            }
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(VisualPath) : new GameObject("EnemyGun_Visual");
            try
            {
                Transform model = root.transform.Find("Model");
                if (model == null)
                {
                    GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                    if (source == null) throw new InvalidOperationException("Import the supplied gun FBX first.");
                    model = ((GameObject)PrefabUtility.InstantiatePrefab(source, root.scene)).transform;
                    model.SetParent(root.transform, false);
                    model.name = "Model";
                }
                var parts = model.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer renderer in parts)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                        AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Models/{m.name}.mat")
                        ?? throw new InvalidOperationException("Unrecognised supplied gun material: " + m.name)).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                LODGroup group = model.GetComponent<LODGroup>();
                if (group == null) group = model.gameObject.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(new[]
                {
                    new LOD(.35f, parts.Where(p => p.name.EndsWith("LOD0", StringComparison.Ordinal)).ToArray()),
                    new LOD(.10f, parts.Where(p => p.name.EndsWith("LOD1", StringComparison.Ordinal)).ToArray()),
                    new LOD(.005f, parts.Where(p => p.name.EndsWith("LOD2", StringComparison.Ordinal)).ToArray()),
                });
                group.RecalculateBounds();
                Transform muzzle = root.transform.Find("MuzzlePoint");
                if (muzzle == null)
                {
                    muzzle = new GameObject("MuzzlePoint").transform;
                    muzzle.SetParent(root.transform, false);
                }
                muzzle.SetLocalPositionAndRotation(new Vector3(.0002f, .08965f, .577724f), Quaternion.identity);
                PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Texture2D Texture(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path)
            ?? throw new InvalidOperationException("Missing supplied gun texture: " + path);

        // Also called by the existing animation builder, so rebuilding animations preserves this fit.
        public static void ConfigureAgent(GameObject root, string name)
        {
            if (name == "TacticalGuard") return; // Keep its original integrated gun and sidearm pose.
            var driver = root.GetComponentsInChildren<AnimatorDriver>(true).Single(d => d.enabled);
            Animator animator = driver.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Agent_Humanoid.controller");
            Transform gun = AgentAvatarBuilder.Find(animator.transform, "Gun");
            if (gun == null) throw new InvalidOperationException(name + ": missing original Gun.");
            Transform supplied = gun.Find(InstanceName);
            if (supplied == null)
            {
                supplied = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath), root.scene)).transform;
                supplied.SetParent(gun, false);
                supplied.name = InstanceName;
            }
            foreach (Renderer renderer in gun.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = renderer.transform.IsChildOf(supplied);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            Transform[] bones = animator.GetComponentsInChildren<Transform>(true).Where(t => t != gun && !t.IsChildOf(gun)).ToArray();
            var rest = bones.Select(t => (t.localPosition, t.localRotation, t.localScale)).ToArray();
            animator.Rebind();
            animator.SetFloat("Speed", 0f);
            animator.SetBool("Aiming", true);
            animator.SetLayerWeight(animator.GetLayerIndex("WeaponPose"), 1f);
            animator.Play("Aim", 0, 0f);
            animator.Update(0f); animator.Update(.2f);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            gun.SetPositionAndRotation(hand.position + root.transform.forward * .035f,
                Quaternion.LookRotation(root.transform.forward, root.transform.up));
            var follow = new SerializedObject(driver);
            // Patrol/Hunter are already parented to this hand. Following them again in LateUpdate
            // adds redundant world/local conversions and perturbs an exactly restored disarm pose.
            bool needsFollow = !gun.IsChildOf(hand);
            follow.FindProperty("weapon").objectReferenceValue = needsFollow ? gun : null;
            follow.FindProperty("weaponHand").objectReferenceValue = needsFollow ? hand : null;
            follow.FindProperty("weaponGripPosition").vector3Value = hand.InverseTransformPoint(gun.position);
            follow.FindProperty("weaponGripRotation").quaternionValue = Quaternion.Inverse(hand.rotation) * gun.rotation;
            follow.ApplyModifiedPropertiesWithoutUndo();
            // Sampling is only for fitting; keep all saved skeleton transforms as they were.
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].SetLocalPositionAndRotation(rest[i].localPosition, rest[i].localRotation);
                bones[i].localScale = rest[i].localScale;
            }
            driver.SyncWeaponGrip();
            PrefabUtility.RecordPrefabInstancePropertyModifications(gun);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            Assign(root.GetComponent<AgentShooter>(), "muzzle", supplied.Find("MuzzlePoint"));
            var target = new SerializedObject(root.GetComponent<AgentTakedownTarget>());
            target.FindProperty("disarmWeapon").objectReferenceValue = gun;
            Renderer[] carried = supplied.GetComponentsInChildren<Renderer>(true);
            SerializedProperty array = target.FindProperty("carriedWeapon");
            array.arraySize = carried.Length;
            for (int i = 0; i < carried.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = carried[i];
            target.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Inspect()
        {
            var report = new StringBuilder();
            foreach (string name in AgentNames)
            {
                GameObject root = PrefabUtility.LoadPrefabContents($"Assets/Prefabs/Agents/{name}.prefab");
                try
                {
                    var driver = root.GetComponentsInChildren<AnimatorDriver>(true).Single(d => d.enabled);
                    Animator animator = driver.GetComponent<Animator>();
                    report.AppendLine($"{name}: controller={animator.runtimeAnimatorController.name}");
                    foreach (Renderer part in root.GetComponentsInChildren<Renderer>(true))
                        report.AppendLine($"  R {AnimationUtility.CalculateTransformPath(part.transform, root.transform)} enabled={part.enabled} bounds={part.bounds} scale={part.transform.lossyScale}");
                    foreach (string pose in new[] { "Locomotion", "Aim" })
                    {
                        animator.Rebind();
                        animator.SetBool("Aiming", pose == "Aim");
                        animator.SetLayerWeight(animator.GetLayerIndex("WeaponPose"), pose == "Aim" ? 1f : 0f);
                        animator.Play(pose, 0, 0f);
                        animator.Update(0f); animator.Update(0.2f); driver.SyncWeaponGrip();
                        foreach (HumanBodyBones bone in new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand })
                        {
                            Transform hand = animator.GetBoneTransform(bone);
                            report.AppendLine($"  {pose} {bone}: path={AnimationUtility.CalculateTransformPath(hand, root.transform)} position={hand.position.ToString("F4")} rotation={hand.rotation.eulerAngles} scale={hand.lossyScale}");
                            var renderer = hand.GetComponent<MeshRenderer>();
                            if (renderer != null) report.AppendLine($"    hand mesh: {hand.GetComponent<MeshFilter>().sharedMesh.bounds}, world: {renderer.bounds}");
                        }
                        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            var baked = new Mesh();
                            skin.BakeMesh(baked, false);
                            report.AppendLine($"  Skin {skin.name}: local={skin.localBounds}, baked={baked.bounds}, world={skin.bounds}");
                            UnityEngine.Object.DestroyImmediate(baked);
                        }
                        Transform gun = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Gun");
                        Transform muzzle = (Transform)new SerializedObject(root.GetComponent<AgentShooter>()).FindProperty("muzzle").objectReferenceValue;
                        report.AppendLine($"  {pose} Gun={gun?.position} scale={gun?.lossyScale} rotation={gun?.rotation.eulerAngles}; muzzle={muzzle.position}, forward={muzzle.forward}");
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            string folder = Path.Combine(Path.GetTempPath(), "blackout-EG1");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "agent-inspection.txt"), report.ToString());
            Debug.Log("[EG1] Agent inspection: " + folder);
        }
    }
}
