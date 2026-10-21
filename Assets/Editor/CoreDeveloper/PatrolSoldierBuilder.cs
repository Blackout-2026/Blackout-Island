using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds Assets/Prefabs/Models/Char_Patrol_Visual.prefab from the downloaded soldier (SM_Char_PatrolSoldier.fbx, step PS4): the skinned model with
    /// the five M_Char_PatrolSoldier_* materials, the SCAR-style rifle (SM_Char_PatrolRifle.fbx) on its right hand, the arms posed to hold it
    /// (<see cref="PatrolSoldierPose"/>), and the rifle's Gun / MuzzlePoint objects, which the Agent Controller's shooter finds by name
    /// (<see cref="CharacterVisualSwap"/>). It is the same prefab the Security Patrol already nests, so the agent prefab does not change.
    /// The prefab is rebuilt from the FBX every time: posing is relative to the current bones, so rebuilding is what keeps it deterministic.
    /// </summary>
    public static class PatrolSoldierBuilder
    {
        public const string SoldierFbxPath = "Assets/Models/Custom/SM_Char_PatrolSoldier.fbx";
        public const string RifleFbxPath = "Assets/Models/Custom/SM_Char_PatrolRifle.fbx";

        /// <summary>The FBX names its materials lambert1 to lambert7 (Mixamo's export); the five assets share them by what they cover.</summary>
        public static readonly KeyValuePair<string, string>[] SlotMaterials =
        {
            new KeyValuePair<string, string>("lambert1", "M_Char_PatrolSoldier_Body"),    // jacket, vest and pouches
            new KeyValuePair<string, string>("lambert2", "M_Char_PatrolSoldier_Face"),    // the head
            new KeyValuePair<string, string>("lambert3", "M_Char_PatrolSoldier_Face"),    // the eyes are on the same face texture
            new KeyValuePair<string, string>("lambert4", "M_Char_PatrolSoldier_Legs"),    // the cap is on the trousers' atlas
            new KeyValuePair<string, string>("lambert5", "M_Char_PatrolSoldier_Legs"),    // trousers and boots
            new KeyValuePair<string, string>("lambert6", "M_Char_PatrolSoldier_Hands"),
            new KeyValuePair<string, string>("lambert7", "M_Char_PatrolSoldier_Hair"),
        };

        public const string RifleMaterialName = "M_Char_Patrol_Kit";
        public const string ModelName = "Model";
        public const string GunName = "Gun";

        /// <summary>The material asset for an imported slot name, or null for a name this soldier does not use.</summary>
        public static string MaterialFor(string slotName)
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

        /// <summary>
        /// The soldier imports as a Humanoid. A rig type of None would drop the skin weights and import a static mesh that the bones no longer move, and a Humanoid
        /// avatar is what lets the Agent Controller's Animator drive it with Mixamo clips later (the importer adds an Animator with that avatar and no controller, which
        /// leaves the baked pose alone until a controller is assigned).
        /// </summary>
        public static void EnsureImportSettings()
        {
            if (AssetImporter.GetAtPath(SoldierFbxPath) is ModelImporter importer && importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.SaveAndReimport();
            }
        }

        public static void Build(CharacterEntry entry, Dictionary<string, Material> materials)
        {
            EnsureImportSettings();
            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierFbxPath);
            var rifle = AssetDatabase.LoadAssetAtPath<GameObject>(RifleFbxPath);
            if (soldier == null || rifle == null)
            {
                Debug.LogWarning($"[PatrolSoldier] {SoldierFbxPath} or {RifleFbxPath} not found, so {entry.VisualPrefabName} was not built.");
                return;
            }

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(entry.VisualPrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(entry.VisualPrefabPath) : new GameObject(entry.VisualPrefabName);
            try
            {
                Populate(root, soldier, rifle, materials);
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

            Debug.Log($"[PatrolSoldier] {(exists ? "Updated" : "Created")} {entry.VisualPrefabPath}");
        }

        /// <summary>Replaces whatever the root holds with the posed soldier and his rifle. Returns the soldier's "Model" object.</summary>
        public static Transform Populate(GameObject root, GameObject soldierFbx, GameObject rifleFbx, Dictionary<string, Material> materials)
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(soldierFbx, root.transform);
            model.name = ModelName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // the Mixamo export faces -Z once imported; the agents face +Z
            AssignMaterials(model, materials);

            // The rifle is placed in the world first and only then handed to the right hand, so posing the hand does not move it.
            var rifle = (GameObject)PrefabUtility.InstantiatePrefab(rifleFbx, root.transform);
            AssignRifleMaterial(rifle, materials);
            PatrolSoldierPose.RifleWorldPose(PatrolSoldierPose.FrameOf(model.transform), out Vector3 gunPosition, out Quaternion gunRotation);
            rifle.transform.SetPositionAndRotation(gunPosition, gunRotation);
            PatrolSoldierPose.Apply(model.transform, gunPosition, gunRotation);
            rifle.transform.SetParent(PatrolSoldierPose.Bone(model.transform, "RightHand"), true);
            return model.transform;
        }

        /// <summary>The rifle is one mesh with one material, the Patrol's Kit atlas (the PS3 operator's M_Char_Patrol_Kit).</summary>
        public static void AssignRifleMaterial(GameObject rifle, Dictionary<string, Material> materials)
        {
            if (!materials.TryGetValue(RifleMaterialName, out Material kit))
            {
                Debug.LogWarning($"[PatrolSoldier] {RifleMaterialName} is missing, so the rifle keeps its imported material.");
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

        /// <summary>Gives every slot of the soldier the asset its imported name stands for (lambert1 is the body, and so on).</summary>
        public static void AssignMaterials(GameObject instance, Dictionary<string, Material> materials)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                Renderer source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                Material[] imported = source != null ? source.sharedMaterials : null;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material named = imported != null && i < imported.Length && imported[i] != null ? imported[i] : slots[i];
                    string assetName = MaterialFor(named != null ? named.name : string.Empty);
                    if (assetName != null && materials.TryGetValue(assetName, out Material replacement))
                    {
                        slots[i] = replacement;
                    }
                    else
                    {
                        Debug.LogWarning($"[PatrolSoldier] {renderer.name} slot {i} uses '{(named != null ? named.name : "nothing")}', which matches no M_Char_PatrolSoldier_* material.", renderer);
                    }
                }

                renderer.sharedMaterials = slots;
            }
        }
    }
}
