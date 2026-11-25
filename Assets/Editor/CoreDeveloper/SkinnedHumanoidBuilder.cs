using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>One downloaded, skinned humanoid that stands in an agent's Visual slot (the Security Patrol's soldier, the Hunter): its files, skeleton and materials.</summary>
    public sealed class SkinnedHumanoidSpec
    {
        public readonly string Name;                                   // the CharacterRig entry it belongs to
        public readonly string FbxPath;
        public readonly string RifleFbxPath;
        public readonly string RifleMaterialName;
        public readonly HumanoidRiflePose Pose;
        public readonly ModelImporterAnimationType Rig;
        public readonly float ModelYawDegrees;                         // turns the model to face +Z (both downloads face -Z once imported)
        public readonly KeyValuePair<string, string>[] SlotMaterials;  // imported material name, then the M_ asset that replaces it

        public SkinnedHumanoidSpec(string name, string fbxPath, string rifleFbxPath, string rifleMaterialName, HumanoidRiflePose pose,
            ModelImporterAnimationType rig, float modelYawDegrees, KeyValuePair<string, string>[] slotMaterials)
        {
            Name = name;
            FbxPath = fbxPath;
            RifleFbxPath = rifleFbxPath;
            RifleMaterialName = rifleMaterialName;
            Pose = pose;
            Rig = rig;
            ModelYawDegrees = modelYawDegrees;
            SlotMaterials = slotMaterials;
        }

        /// <summary>The material asset for an imported slot name, or null for a name this model does not use.</summary>
        public string MaterialFor(string slotName)
        {
            foreach (KeyValuePair<string, string> pair in SlotMaterials)
            {
                if (pair.Key == slotName)
                {
                    return pair.Value;
                }
            }

            return null;
        }

        /// <summary>The distinct material assets the model wears.</summary>
        public HashSet<string> MaterialNames()
        {
            var names = new HashSet<string>();
            foreach (KeyValuePair<string, string> pair in SlotMaterials)
            {
                names.Add(pair.Value);
            }

            return names;
        }
    }

    /// <summary>
    /// Builds the visual prefab of a downloaded skinned humanoid (docs/art-pipeline.md section 18.4): the model with its M_ materials turned to face +Z, a rifle on its right hand, the
    /// arms posed to hold it (<see cref="HumanoidRiflePose"/>), and the rifle's Gun / MuzzlePoint objects, which the Agent Controller's shooter finds by name
    /// (<see cref="CharacterVisualSwap"/>). It is the prefab the agent already nests (Char_Patrol_Visual, Char_Hunter_Visual), so the agent prefab does not change. The prefab is
    /// rebuilt from the FBX every time: posing is relative to the current bones, so rebuilding is what keeps it deterministic.
    /// </summary>
    public static class SkinnedHumanoidBuilder
    {
        public const string ModelName = "Model";
        public const string GunName = "Gun";

        /// <summary>The spec of a CharacterRig entry that is a skinned humanoid, or null.</summary>
        public static SkinnedHumanoidSpec SpecFor(string characterName)
        {
            switch (characterName)
            {
                case "Patrol": return PatrolSoldierBuilder.Spec;
                case "Hunter": return HunterModelBuilder.Spec;
                default: return null;
            }
        }

        /// <summary>
        /// Sets the rig type the model needs. A rig type of None would drop the skin weights and import a static mesh that the bones no longer move; Humanoid (the Mixamo soldier)
        /// gives a valid avatar for the shared soldier clips. AN1 also imports the Valve-biped Hunter as Humanoid; Generic would only keep the skin without retargeting these clips.
        /// The importer adds an Animator without a controller, which leaves the baked pose alone until one is assigned.
        /// </summary>
        public static void EnsureImportSettings(SkinnedHumanoidSpec spec)
        {
            if (AssetImporter.GetAtPath(spec.FbxPath) is ModelImporter importer && importer.animationType != spec.Rig)
            {
                importer.animationType = spec.Rig;
                importer.SaveAndReimport();
            }
        }

        public static void Build(CharacterEntry entry, SkinnedHumanoidSpec spec, Dictionary<string, Material> materials)
        {
            EnsureImportSettings(spec);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
            var rifle = AssetDatabase.LoadAssetAtPath<GameObject>(spec.RifleFbxPath);
            if (model == null || rifle == null)
            {
                Debug.LogWarning($"[SkinnedHumanoid] {spec.FbxPath} or {spec.RifleFbxPath} not found, so {entry.VisualPrefabName} was not built.");
                return;
            }

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(entry.VisualPrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(entry.VisualPrefabPath) : new GameObject(entry.VisualPrefabName);
            try
            {
                Populate(root, spec, model, rifle, materials);
                PrefabUtility.SaveAsPrefabAsset(root, entry.VisualPrefabPath);
            }
            finally
            {
                if (exists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }

            Debug.Log($"[SkinnedHumanoid] {(exists ? "Updated" : "Created")} {entry.VisualPrefabPath}");
        }

        /// <summary>Replaces whatever the root holds with the posed model and its rifle. Returns the model's "Model" object.</summary>
        public static Transform Populate(GameObject root, SkinnedHumanoidSpec spec, GameObject modelFbx, GameObject rifleFbx, Dictionary<string, Material> materials)
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelFbx, root.transform);
            model.name = ModelName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, spec.ModelYawDegrees, 0f);
            AssignMaterials(model, spec, materials);

            // The rifle is placed in the world first and only then handed to the right hand, so posing the hand does not move it.
            var rifle = (GameObject)PrefabUtility.InstantiatePrefab(rifleFbx, root.transform);
            AssignRifleMaterial(rifle, spec, materials);
            spec.Pose.RifleWorldPose(spec.Pose.FrameOf(model.transform), out Vector3 gunPosition, out Quaternion gunRotation);
            rifle.transform.SetPositionAndRotation(gunPosition, gunRotation);
            spec.Pose.Apply(model.transform, gunPosition, gunRotation);
            rifle.transform.SetParent(spec.Pose.Bones.Hand(model.transform, "Right"), true);
            return model.transform;
        }

        /// <summary>The rifle is one mesh with one material, the Patrol's Kit atlas (M_Char_Patrol_Kit), shared by both rifles.</summary>
        public static void AssignRifleMaterial(GameObject rifle, SkinnedHumanoidSpec spec, Dictionary<string, Material> materials)
        {
            if (!materials.TryGetValue(spec.RifleMaterialName, out Material kit))
            {
                Debug.LogWarning($"[SkinnedHumanoid] {spec.RifleMaterialName} is missing, so the rifle keeps its imported material.");
                return;
            }

            foreach (Renderer renderer in rifle.GetComponentsInChildren<Renderer>(true))
            {
                var slots = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    slots[i] = kit;
                }

                renderer.sharedMaterials = slots;
            }
        }

        /// <summary>Gives every slot of the model the asset its imported name stands for.</summary>
        public static void AssignMaterials(GameObject instance, SkinnedHumanoidSpec spec, Dictionary<string, Material> materials)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                Renderer source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                Material[] imported = source != null ? source.sharedMaterials : null;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material named = imported != null && i < imported.Length && imported[i] != null ? imported[i] : slots[i];
                    string assetName = spec.MaterialFor(named != null ? named.name : string.Empty);
                    if (assetName != null && materials.TryGetValue(assetName, out Material replacement))
                    {
                        slots[i] = replacement;
                    }
                    else
                    {
                        Debug.LogWarning($"[SkinnedHumanoid] {renderer.name} slot {i} uses '{(named != null ? named.name : "nothing")}', which matches no {spec.Name} material.", renderer);
                    }
                }

                renderer.sharedMaterials = slots;
            }
        }
    }
}
