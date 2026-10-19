using Blackout.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>
    /// Applies the Y7 layer decisions the team agreed on (docs/reference/systems-engineer.md section 12):
    /// - In Environment.unity, every solid collider still on Default moves to Level, so ragdolls land on the world and
    ///   the Tactical Guard's sight check (it treats Level as blocking) is stopped by walls.
    /// - The dock railings (Dock_Rail_*) go to Ignore Raycast instead: in the prototype bullets and eyes pass through
    ///   them, but the player still bumps into them.
    /// - In the Systems Engineer's interactable prefabs, solid colliders still on Default (the nested
    ///   Generator_Visual's body box) move to Level, as an override in Generator.prefab, so the Core Developer's
    ///   prefab is not edited.
    /// - AgentBase.prefab's AgentShooter hit mask moves from "everything" to PhysicsLayers.AgentShots (no friendly
    ///   fire, as in the prototype).
    /// Re-runnable: only objects still on Default and a mask still set to everything are changed, so a layer or mask
    /// set by hand is kept. Triggers and objects with a Rigidbody are left alone. Run it again after a World Builder
    /// tool rebuilds part of the level, because new objects are created on Default.
    /// </summary>
    public static class PhysicsLayerAssigner
    {
        private const string EnvironmentPath = "Assets/Scenes/Environment.unity";
        private const string AgentBasePath = "Assets/Prefabs/Agents/AgentBase.prefab";
        private const string RailPrefix = "Dock_Rail_";
        private static readonly string[] InteractablePrefabs =
        {
            InteractablePrefabBuilder.DoorPath, InteractablePrefabBuilder.GeneratorPath, InteractablePrefabBuilder.CurePath,
            InteractablePrefabBuilder.HealthPath, InteractablePrefabBuilder.CratePath,
        };

        // Found by name so this editor assembly does not have to reference the Agent Controller's assembly.
        private const string AgentShooterType = "AgentShooter";

        [MenuItem("Blackout/Systems/Assign Physics Layers")]
        public static void Assign()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            AssignEnvironmentLayers(out int toLevel, out int toIgnoreRaycast);
            int inPrefabs = 0;
            foreach (string path in InteractablePrefabs)
            {
                inPrefabs += AssignPrefabLayers(path);
            }

            int masks = AssignAgentShotMask();
            Debug.Log($"[Blackout] Assign Physics Layers: {toLevel} colliders moved to Level, {toIgnoreRaycast} dock " +
                      $"railings to Ignore Raycast, {inPrefabs} interactable prefab colliders to Level, {masks} " +
                      "AgentShooter masks set to AgentShots. Anything already off Default was left as it is.");
        }

        private static void AssignEnvironmentLayers(out int toLevel, out int toIgnoreRaycast)
        {
            toLevel = 0;
            toIgnoreRaycast = 0;
            Scene scene = EditorSceneManager.OpenScene(EnvironmentPath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                {
                    GameObject owner = collider.gameObject;
                    if (collider.isTrigger || collider.attachedRigidbody != null
                        || owner.layer != PhysicsLayers.Default)
                    {
                        continue;
                    }

                    if (owner.name.StartsWith(RailPrefix, System.StringComparison.Ordinal))
                    {
                        owner.layer = PhysicsLayers.IgnoreRaycast;
                        toIgnoreRaycast++;
                    }
                    else
                    {
                        owner.layer = PhysicsLayers.Level;
                        toLevel++;
                    }
                }
            }

            // Saved only when something changed, so a re-run leaves the file byte-identical.
            if (toLevel + toIgnoreRaycast > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        /// <summary>Moves solid colliders still on Default inside one of our prefabs to Level. Returns how many.</summary>
        private static int AssignPrefabLayers(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            {
                return 0;
            }

            int changed = 0;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                {
                    if (!collider.isTrigger && collider.attachedRigidbody == null
                        && collider.gameObject.layer == PhysicsLayers.Default)
                    {
                        collider.gameObject.layer = PhysicsLayers.Level;
                        changed++;
                    }
                }

                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }

        /// <summary>Moves AgentShooter masks still set to everything (the old default) to AgentShots.</summary>
        private static int AssignAgentShotMask()
        {
            if (AssetDatabase.LoadMainAssetAtPath(AgentBasePath) == null)
            {
                Debug.LogWarning($"[Blackout] Assign Physics Layers: {AgentBasePath} not found; agent mask skipped.");
                return 0;
            }

            int changed = 0;
            GameObject root = PrefabUtility.LoadPrefabContents(AgentBasePath);
            try
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || behaviour.GetType().Name != AgentShooterType)
                    {
                        continue;
                    }

                    var serialized = new SerializedObject(behaviour);
                    SerializedProperty mask = serialized.FindProperty("hitMask");
                    if (mask != null && mask.intValue == ~0)
                    {
                        mask.intValue = PhysicsLayers.AgentShots;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        changed++;
                    }
                }

                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, AgentBasePath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }
    }
}
