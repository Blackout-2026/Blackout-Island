using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Replaces the blue capsule that stands in for the three humanoid agents with the real models. For each of the Agent Controller's
    /// prefabs (SecurityPatrol, Hunter, Commander) it deletes the child called PLACEHOLDER_Humanoid under "Visual", nests the matching
    /// Char_*_Visual prefab there and points the agent's AgentShooter at the model's MuzzlePoint (the rifle or pistol muzzle). The agent
    /// prefabs belong to the Agent Controller: the change is the removed capsule, the nested model and one reference, nothing else.
    /// Re-runnable: nothing changes once a prefab has its model. The Animator is not touched: the models have no skin, so the humans
    /// stand upright until the Agent Controller adds a procedural animator like the robot's.
    /// </summary>
    public static class CharacterVisualSwap
    {
        private const string VisualChild = "Visual";
        private const string PlaceholderName = "PLACEHOLDER_Humanoid";

        [MenuItem("Blackout/Art/Use Character Models In Agents")]
        public static void SwapAll()
        {
            CharacterPrefabBuilder.BuildAll();
            foreach (CharacterEntry entry in CharacterRig.Characters)
            {
                var visual = AssetDatabase.LoadAssetAtPath<GameObject>(entry.VisualPrefabPath);
                if (visual == null)
                {
                    Debug.LogWarning($"[CharacterSwap] {entry.VisualPrefabPath} does not exist, so {entry.AgentPrefab} was not changed.");
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(entry.AgentPrefab) == null)
                {
                    Debug.LogWarning($"[CharacterSwap] {entry.AgentPrefab} does not exist. Run Blackout > Agents > Build Spawn Setup first.");
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(entry.AgentPrefab);
                try
                {
                    if (Swap(root, visual))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, entry.AgentPrefab);
                        Debug.Log($"[CharacterSwap] {entry.AgentPrefab} now shows {visual.name}.");
                    }
                    else
                    {
                        Debug.Log($"[CharacterSwap] {entry.AgentPrefab} already shows {visual.name}, left unchanged.");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Removes the capsule, nests the model under Visual and wires the muzzle. True when anything changed.</summary>
        public static bool Swap(GameObject agent, GameObject visualPrefab)
        {
            Transform visualParent = agent.transform.Find(VisualChild);
            if (visualParent == null)
            {
                Debug.LogWarning($"[CharacterSwap] {agent.name} has no '{VisualChild}' child.");
                return false;
            }

            bool changed = false;
            Transform capsule = visualParent.Find(PlaceholderName);
            if (capsule != null)
            {
                Object.DestroyImmediate(capsule.gameObject);
                changed = true;
            }

            Transform model = RobotRig.FindDeep(visualParent, visualPrefab.name);
            if (model == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, visualParent);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                model = instance.transform;
                changed = true;
            }

            Transform muzzle = RobotRig.FindDeep(model, CharacterRig.MuzzlePoint);
            if (muzzle != null && SetMuzzle(agent, muzzle))
            {
                changed = true;
            }

            return changed;
        }

        // The shooter is the component on the agent with a serialized "muzzle" and "eyeHeight": found by its fields, so this
        // Core Developer tool needs no reference to the Agent Controller's assembly.
        private static bool SetMuzzle(GameObject agent, Transform muzzle)
        {
            foreach (MonoBehaviour behaviour in agent.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.FindProperty("muzzle");
                if (property == null || serialized.FindProperty("eyeHeight") == null)
                {
                    continue;
                }

                if (property.objectReferenceValue == muzzle)
                {
                    return false;
                }

                property.objectReferenceValue = muzzle;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }

            return false;
        }
    }
}
