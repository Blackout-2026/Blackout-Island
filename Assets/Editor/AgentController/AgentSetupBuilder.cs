// Owner: Agent Controller.
using System.IO;
using Blackout.Agents;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds everything the spawner needs, once: the DifficultyConfig asset, one prefab per agent type (a variant
    /// of AgentBase with its visual nested under Visual), and Assets/Scenes/Agents.unity holding the registry,
    /// the Blackboard holder and the spawner. Re-runnable: anything that already exists is left alone, so hand
    /// edits are never overwritten. Touches only this role's paths and never saves any other scene.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.AgentSetupBuilder.BuildAll
    /// </summary>
    public static class AgentSetupBuilder
    {
        public const string DifficultyPath = "Assets/ScriptableObjects/Agents/DifficultyConfig.asset";
        public const string ScenePath = "Assets/Scenes/Agents.unity";
        public const string PrefabFolder = "Assets/Prefabs/Agents";
        public const string RobotVisualPath = "Assets/Prefabs/Models/SecurityRobot_Visual.prefab";
        public const string PlaceholderName = "PLACEHOLDER_Humanoid";
        private const string GeneratorDisabledPath = "Assets/ScriptableObjects/Events/GeneratorDisabled.asset";
        private const string CureTakenPath = "Assets/ScriptableObjects/Events/CureTaken.asset";

        private struct TypeSetup
        {
            public AgentKind kind;
            public string prefabName;
            public string ownerPrefix; // matches the AgentSpawn markers' Owner (Hunter-1, Patrol-1, TG-01, Commander)
            public float eyeHeight;    // docs/reference/agent-controller.md section 5
            public Color placeholderColor;
        }

        private static readonly TypeSetup[] Types =
        {
            new TypeSetup { kind = AgentKind.Hunter, prefabName = "Hunter", ownerPrefix = "Hunter-", eyeHeight = 1.6f, placeholderColor = new Color(0.25f, 0.45f, 0.2f) },
            new TypeSetup { kind = AgentKind.SecurityPatrol, prefabName = "SecurityPatrol", ownerPrefix = "Patrol-", eyeHeight = 1.62f, placeholderColor = new Color(0.2f, 0.3f, 0.6f) },
            new TypeSetup { kind = AgentKind.TacticalGuard, prefabName = "TacticalGuard", ownerPrefix = "TG-", eyeHeight = 1.95f, placeholderColor = new Color(0.6f, 0.6f, 0.65f) },
            new TypeSetup { kind = AgentKind.Commander, prefabName = "Commander", ownerPrefix = "Commander", eyeHeight = 1.62f, placeholderColor = new Color(0.9f, 0.9f, 0.85f) },
        };

        public static string PrefabPathFor(AgentKind kind)
        {
            foreach (TypeSetup type in Types)
            {
                if (type.kind == kind)
                {
                    return $"{PrefabFolder}/{type.prefabName}.prefab";
                }
            }
            return null;
        }

        [MenuItem("Blackout/Agents/Build Spawn Setup")]
        public static void BuildAll()
        {
            AgentBasePrefabBuilder.Build(); // makes or tops up AgentBase.prefab, which the type prefabs are variants of

            EnsureDifficultyConfig();
            foreach (TypeSetup type in Types)
            {
                EnsureTypePrefab(type);
            }
            EnsureScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureDifficultyConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<DifficultyConfig>(DifficultyPath) != null)
            {
                return;
            }

            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DifficultyConfig>(), DifficultyPath); // starts with the prototype numbers
            Debug.Log($"AgentSetupBuilder: created {DifficultyPath}.");
        }

        private static void EnsureTypePrefab(TypeSetup type)
        {
            string path = PrefabPathFor(type.kind);
            if (File.Exists(path))
            {
                return;
            }

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AgentBasePrefabBuilder.PrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError($"AgentSetupBuilder: {AgentBasePrefabBuilder.PrefabPath} is missing, so {type.prefabName} was not built.");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                instance.name = type.prefabName;
                instance.GetComponent<AgentRoot>().SetKind(type.kind);
                Transform visualParent = instance.transform.Find(AgentBasePrefabBuilder.VisualChildName);
                Transform muzzle = null;

                var robot = type.kind == AgentKind.TacticalGuard ? AssetDatabase.LoadAssetAtPath<GameObject>(RobotVisualPath) : null;
                if (robot != null)
                {
                    // The Core Developer's robot nests as it is; the animator is added on this prefab's instance of it, never on theirs.
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(robot, visualParent);
                    visual.AddComponent<ProceduralRobotAnimator>();
                    muzzle = FindDeep(visual.transform, "MuzzlePoint");
                }
                else
                {
                    AddPlaceholderHumanoid(visualParent, type);
                }

                ConfigureShooter(instance, type.eyeHeight, muzzle);
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                Debug.Log($"AgentSetupBuilder: created {path}.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // Stands in for a model that does not exist yet (no humanoid is in the project). Replace it by nesting a real
        // humanoid prefab under Visual, deleting this child, and adding AnimatorDriver with Agent_Humanoid.controller.
        private static void AddPlaceholderHumanoid(Transform visualParent, TypeSetup type)
        {
            GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = PlaceholderName;
            Object.DestroyImmediate(capsule.GetComponent<Collider>()); // the agent's own capsule collider does the work
            capsule.layer = visualParent.gameObject.layer;
            capsule.transform.SetParent(visualParent, false);
            capsule.transform.localScale = new Vector3(0.76f, 0.9f, 0.76f); // 1.8 m tall, 0.38 m radius, like the collider
            capsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            capsule.GetComponent<Renderer>().sharedMaterial = EnsurePlaceholderMaterial(type);
        }

        private static Material EnsurePlaceholderMaterial(TypeSetup type)
        {
            string folder = PrefabFolder + "/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(PrefabFolder, "Materials");
            }

            string path = $"{folder}/PH_{type.prefabName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = type.placeholderColor };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ConfigureShooter(GameObject instance, float eyeHeight, Transform muzzle)
        {
            AgentShooter shooter = instance.GetComponentInChildren<AgentShooter>(true);
            if (shooter == null)
            {
                return;
            }

            var serialized = new SerializedObject(shooter);
            serialized.FindProperty("eyeHeight").floatValue = eyeHeight;
            if (muzzle != null)
            {
                serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureScene()
        {
            if (File.Exists(ScenePath))
            {
                return;
            }

            // Additive, so whatever scene the Editor has open is neither replaced nor saved. The one exception is an
            // empty untitled scene (what batch mode starts with): Unity refuses to add to it, and there is nothing to lose.
            Scene active = SceneManager.GetActiveScene();
            bool onlyAnEmptyUntitledScene = SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path) && active.rootCount == 0;
            NewSceneMode mode = onlyAnEmptyUntitledScene ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            try
            {
                var root = new GameObject("Agents");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.AddComponent<AgentRegistry>(); // adds the BlackboardHolder too (RequireComponent)
                AssignChannels(root.GetComponent<BlackboardHolder>());

                var spawnerObject = new GameObject("AgentSpawner");
                spawnerObject.transform.SetParent(root.transform, false);
                ConfigureSpawner(spawnerObject.AddComponent<AgentSpawner>());

                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"AgentSetupBuilder: created {ScenePath}.");
            }
            finally
            {
                if (mode == NewSceneMode.Additive)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void AssignChannels(BlackboardHolder holder)
        {
            var serialized = new SerializedObject(holder);
            serialized.FindProperty("generatorDisabled").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(GeneratorDisabledPath);
            serialized.FindProperty("cureTaken").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(CureTakenPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureSpawner(AgentSpawner spawner)
        {
            var serialized = new SerializedObject(spawner);
            // Loaded here, after the scene exists: creating a scene unloads unused assets, which would have destroyed a
            // reference held in a variable since before it.
            serialized.FindProperty("difficulty").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(DifficultyPath);
            serialized.FindProperty("spawnOnStart").boolValue = true;
            // Until the real brains are in the prefabs, agents without one walk their waypoints so the scene is alive.
            serialized.FindProperty("placeholderBrainWhenMissing").boolValue = true;

            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = Types.Length;
            for (int i = 0; i < Types.Length; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("kind").enumValueIndex = (int)Types[i].kind;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPathFor(Types[i].kind));
                entry.FindPropertyRelative("ownerPrefix").stringValue = Types[i].ownerPrefix;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
