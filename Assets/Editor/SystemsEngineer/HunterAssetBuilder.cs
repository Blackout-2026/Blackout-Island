using Blackout.AI.Hunter;
using Blackout.Shared;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Creates the Hunter's config assets in Assets/Scripts/AI/Hunter/Config (docs/reference/systems-engineer.md
    /// sections 14 and 15), with the prototype's values from the class defaults, and wires the Hunter agent prefab:
    /// a HearingSystem (HearingConfig, Noise channel) and a HunterBrain (HunterConfig, that HearingSystem) on the
    /// root of Assets/Prefabs/Agents/Hunter.prefab, a variant of the Agent Controller's AgentBase (H3, done with the
    /// Agent Controller's permission). AgentRoot then finds the brain, and the spawner no longer gives Hunters its
    /// placeholder walker.
    /// Re-runnable: existing assets are left untouched (tuning and GUIDs are kept), only missing components are
    /// added and only empty fields are filled, and the prefab is saved only when something changed. Nothing is
    /// removed.
    /// </summary>
    public static class HunterAssetBuilder
    {
        public const string Folder = "Assets/Scripts/AI/Hunter/Config";
        public const string HearingConfigPath = Folder + "/HearingConfig.asset";
        public const string HunterConfigPath = Folder + "/HunterConfig.asset";
        public const string HunterPrefabPath = "Assets/Prefabs/Agents/Hunter.prefab";
        private const string NoiseChannelPath = EventChannelAssetBuilder.Folder + "/Noise.asset";

        [MenuItem("Blackout/Systems/Build Hunter Config")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Scripts/AI/Hunter", "Config");
            }

            int created = CreateIfMissing<HearingConfig>(HearingConfigPath) + CreateIfMissing<HunterConfig>(HunterConfigPath);
            AssetDatabase.SaveAssets();

            bool wired = WireHunterPrefab();
            Debug.Log($"[Blackout] Build Hunter Config: created {created}, kept {2 - created} config assets in " +
                      $"{Folder}; {(wired ? "wired" : "left unchanged")} {HunterPrefabPath}.");
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

        /// <summary>Adds and fills the hearing and the brain on the Hunter prefab's root. Returns true if it saved.</summary>
        private static bool WireHunterPrefab()
        {
            if (AssetDatabase.LoadMainAssetAtPath(HunterPrefabPath) == null)
            {
                Debug.LogWarning($"[Blackout] Build Hunter Config: {HunterPrefabPath} not found; prefab not wired.");
                return false;
            }

            var hearingConfig = AssetDatabase.LoadAssetAtPath<HearingConfig>(HearingConfigPath);
            var hunterConfig = AssetDatabase.LoadAssetAtPath<HunterConfig>(HunterConfigPath);
            var noise = AssetDatabase.LoadAssetAtPath<NoiseEventChannel>(NoiseChannelPath);
            if (noise == null)
            {
                Debug.LogWarning($"[Blackout] Build Hunter Config: {NoiseChannelPath} not found; the Hunter will not " +
                                 "hear anything until it is assigned (run Blackout/Systems/Create Event Channels).");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(HunterPrefabPath);
            try
            {
                bool changed = false;
                var hearing = root.GetComponent<HearingSystem>();
                if (hearing == null)
                {
                    hearing = root.AddComponent<HearingSystem>();
                    changed = true;
                }

                changed |= SetIfEmpty(hearing, "config", hearingConfig);
                changed |= SetIfEmpty(hearing, "noiseChannel", noise);

                var brain = root.GetComponent<HunterBrain>();
                if (brain == null)
                {
                    if (root.GetComponent<IAgentBrain>() != null)
                    {
                        // Two brains would make AgentRoot's choice depend on component order; never remove someone
                        // else's component, so stop and say so.
                        Debug.LogWarning($"[Blackout] Build Hunter Config: {HunterPrefabPath} already has another " +
                                         "brain; HunterBrain not added. Remove the other brain, then run this again.");
                    }
                    else
                    {
                        brain = root.AddComponent<HunterBrain>();
                        changed = true;
                    }
                }

                if (brain != null)
                {
                    changed |= SetIfEmpty(brain, "config", hunterConfig);
                    changed |= SetIfEmpty(brain, "hearing", hearing);
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, HunterPrefabPath);
                }

                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Assigns an object field only if it is empty, so a reference set by hand is kept.</summary>
        private static bool SetIfEmpty(Component component, string field, Object value)
        {
            if (value == null)
            {
                return false;
            }

            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || property.objectReferenceValue != null)
            {
                return false;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
