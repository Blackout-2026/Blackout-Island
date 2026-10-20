using Blackout.AI.Hunter;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Creates the Hunter's config assets in Assets/Scripts/AI/Hunter/Config (docs/reference/systems-engineer.md
    /// section 14), with the prototype's values from the class defaults. Re-runnable: an asset that already exists is
    /// left untouched, so tuning done in the Inspector and every reference to its GUID are kept. Nothing is deleted.
    /// </summary>
    public static class HunterAssetBuilder
    {
        public const string Folder = "Assets/Scripts/AI/Hunter/Config";
        public const string HearingConfigPath = Folder + "/HearingConfig.asset";

        [MenuItem("Blackout/Systems/Build Hunter Config")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Scripts/AI/Hunter", "Config");
            }

            int created = CreateIfMissing<HearingConfig>(HearingConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Blackout] Build Hunter Config: created {created}, kept {1 - created} in {Folder}.");
        }

        private static int CreateIfMissing<T>(string path) where T : ScriptableObject
        {
            Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null)
            {
                // CreateAsset would replace whatever is there, so never touch a file of another type.
                if (!(existing is T))
                {
                    Debug.LogWarning($"[Blackout] {path} exists but is not a {typeof(T).Name}; left unchanged.");
                }

                return 0;
            }

            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
            return 1;
        }
    }
}
