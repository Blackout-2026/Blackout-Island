// Owner: Agent Controller.
using System;
using Blackout.AI.Commander;
using Blackout.Agents;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>G2 setup. Preserves existing tuning and saves only this role's Commander prefab and Agents scene.</summary>
    public static class CommanderSetupBuilder
    {
        public const string ConfigPath = "Assets/Scripts/AI/Commander/Config/CommanderConfig.asset";
        public const string PrefabPath = "Assets/Prefabs/Agents/Commander.prefab";
        public const string ScenePath = "Assets/Scenes/Agents.unity";

        [MenuItem("Blackout/Agents/Set Up Commander (G2)")]
        public static void Build()
        {
            var config = AssetDatabase.LoadAssetAtPath<CommanderConfig>(ConfigPath);
            if (config == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Scripts/AI/Commander/Config"))
                    AssetDatabase.CreateFolder("Assets/Scripts/AI/Commander", "Config");
                config = ScriptableObject.CreateInstance<CommanderConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            ConfigurePrefab(config);
            ConfigureScene();
            AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log("CommanderSetupBuilder: Commander prefab, config and player lifecycle channels are configured.");
        }

        private static void ConfigurePrefab(CommanderConfig config)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                bool changed = false;
                var brain = contents.GetComponent<CommanderBrain>();
                if (brain == null)
                {
                    brain = contents.AddComponent<CommanderBrain>();
                    changed = true;
                }
                var brainData = new SerializedObject(brain);
                SerializedProperty configField = brainData.FindProperty("config");
                // Preserve a deliberately assigned alternative Commander config.
                if (configField.objectReferenceValue == null)
                {
                    configField.objectReferenceValue = config;
                    changed |= brainData.ApplyModifiedPropertiesWithoutUndo();
                }
                var root = contents.GetComponent<AgentRoot>();
                if (root == null) throw new InvalidOperationException("Commander prefab needs AgentRoot.");
                var rootData = new SerializedObject(root);
                SerializedProperty brainField = rootData.FindProperty("brainBehaviour");
                if (brainField.objectReferenceValue != null && brainField.objectReferenceValue != brain)
                    throw new InvalidOperationException("Commander already has another brain assigned. Review it before running G2 setup.");
                if (brainField.objectReferenceValue != brain)
                {
                    brainField.objectReferenceValue = brain;
                    changed |= rootData.ApplyModifiedPropertiesWithoutUndo();
                }
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                BlackboardHolder holder = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (BlackboardHolder candidate in root.GetComponentsInChildren<BlackboardHolder>(true))
                    {
                        if (holder != null) throw new InvalidOperationException("Agents scene has more than one BlackboardHolder.");
                        holder = candidate;
                    }
                if (holder == null) throw new InvalidOperationException("Agents scene needs its BlackboardHolder.");
                var data = new SerializedObject(holder);
                SetChannel(data, "playerDied", "PlayerDied");
                SetChannel(data, "playerRespawned", "PlayerRespawned");
                if (data.ApplyModifiedPropertiesWithoutUndo())
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void SetChannel(SerializedObject holder, string field, string asset)
        {
            var channel = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(
                "Assets/ScriptableObjects/Events/" + asset + ".asset");
            if (channel == null) throw new InvalidOperationException("Missing existing event channel: " + asset);
            holder.FindProperty(field).objectReferenceValue = channel;
        }
    }
}
