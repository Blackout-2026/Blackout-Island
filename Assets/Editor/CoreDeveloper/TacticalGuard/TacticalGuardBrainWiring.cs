using Blackout.AI.TacticalGuard;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Puts the Tactical Guard's utility brain (and its config asset) on Assets/Prefabs/Agents/TacticalGuard.prefab.
    /// Without it AgentRoot finds no IAgentBrain, so AgentSpawner gives the guard its placeholder WaypointWalkerBrain: the guard then
    /// only walks between waypoints and never aims or shoots. AgentRoot finds the brain with GetComponent, so a component on the
    /// prefab root is all it needs. Re-runnable: nothing changes when the brain and the config are already there.
    /// Batch mode: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;"
    /// -executeMethod Blackout.Editor.TacticalGuardBrainWiring.AttachToPrefab -logFile -
    /// </summary>
    public static class TacticalGuardBrainWiring
    {
        public const string PrefabPath = "Assets/Prefabs/Agents/TacticalGuard.prefab";

        [MenuItem("Blackout/AI/Attach Tactical Guard Brain To Prefab")]
        public static void AttachToPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                Debug.LogError($"[TacticalGuard] {PrefabPath} does not exist. Run Blackout > Agents > Build Spawn Setup first.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<TacticalGuardConfig>(TacticalGuardConfigCreator.AssetPath);
            if (config == null)
            {
                TacticalGuardConfigCreator.CreateDefault();
                config = AssetDatabase.LoadAssetAtPath<TacticalGuardConfig>(TacticalGuardConfigCreator.AssetPath);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (Wire(root, config))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    Debug.Log($"[TacticalGuard] Attached TacticalGuardBrain with {TacticalGuardConfigCreator.AssetPath} to {PrefabPath}.");
                }
                else
                {
                    Debug.Log($"[TacticalGuard] {PrefabPath} already has the brain and its config, left unchanged.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds a TacticalGuardBrain to the agent if it has none and points it at the config. True when anything changed.</summary>
        public static bool Wire(GameObject agent, TacticalGuardConfig config)
        {
            var brain = agent.GetComponent<TacticalGuardBrain>();
            bool changed = brain == null;
            if (brain == null)
            {
                brain = agent.AddComponent<TacticalGuardBrain>();
            }

            var serialized = new SerializedObject(brain);
            SerializedProperty property = serialized.FindProperty("config");
            if (property.objectReferenceValue != config)
            {
                property.objectReferenceValue = config;
                changed = true;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }
    }
}
