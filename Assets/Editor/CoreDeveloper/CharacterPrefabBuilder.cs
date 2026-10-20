using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds Assets/Prefabs/Models/Char_Patrol_Visual.prefab, Char_Hunter_Visual.prefab and Char_Commander_Visual.prefab
    /// (docs/art-pipeline.md section 18): the model only, with its materials, no collider, Health or script. The Agent Controller's
    /// agent prefabs nest these in place of the capsule (see <see cref="CharacterVisualSwap"/>). Re-runnable: only the "Model"
    /// child is replaced, and not even that while it already is an instance of the same FBX.
    /// </summary>
    public static class CharacterPrefabBuilder
    {
        public const string ModelName = "Model";

        [MenuItem("Blackout/Art/Build Character Visual Prefabs")]
        public static void BuildAll()
        {
            Dictionary<string, Material> materials = CharacterMaterialBuilder.BuildAll();
            ModelMaterials.EnsureFolder("Assets/Prefabs/Models");
            foreach (CharacterEntry entry in CharacterRig.Characters)
            {
                Build(entry, materials);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Rebuilds only the Commander's materials and visual prefab (after his model changed); the other two characters are not touched.</summary>
        [MenuItem("Blackout/Art/Rebuild Commander Visual Prefab")]
        public static void RebuildCommander()
        {
            CharacterEntry entry = CharacterRig.Find("Commander");
            ModelMaterials.EnsureFolder("Assets/Prefabs/Models");
            Build(entry, CharacterMaterialBuilder.BuildFor("Commander"));
            AssetDatabase.SaveAssets();
        }

        public static void Build(CharacterEntry entry, Dictionary<string, Material> materials)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(entry.FbxPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[CharacterPrefab] {entry.FbxPath} not found, so {entry.VisualPrefabName} was not built.");
                return;
            }

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(entry.VisualPrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(entry.VisualPrefabPath) : new GameObject(entry.VisualPrefabName);
            try
            {
                Populate(root, fbx, materials);
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

            Debug.Log($"[CharacterPrefab] {(exists ? "Updated" : "Created")} {entry.VisualPrefabPath}");
        }

        /// <summary>Puts the FBX under the root as "Model" and gives it the M_Char_* assets. Safe to call repeatedly.</summary>
        public static Transform Populate(GameObject root, GameObject fbx, Dictionary<string, Material> materials)
        {
            Transform old = root.transform.Find(ModelName);
            if (old != null && PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject) == fbx)
            {
                AssignMaterials(old.gameObject, materials);
                return old;
            }

            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            instance.name = ModelName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            AssignMaterials(instance, materials);
            return instance.transform;
        }

        /// <summary>
        /// Replaces every imported slot with the asset of the same name. The name is read from the FBX's own slot when the renderer
        /// belongs to a model instance, so a prefab built from an older FBX is corrected instead of keeping its old materials.
        /// </summary>
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
                    string slotName = named != null ? named.name : string.Empty;
                    if (materials.TryGetValue(slotName, out Material replacement))
                    {
                        slots[i] = replacement;
                    }
                    else
                    {
                        Debug.LogWarning($"[CharacterPrefab] {renderer.name} slot {i} uses '{slotName}', which matches no M_Char_* material.", renderer);
                    }
                }

                renderer.sharedMaterials = slots;
            }
        }
    }
}
