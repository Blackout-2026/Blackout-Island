using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Agents.Tests
{
    /// <summary>Checks the assets Blackout > Agents > Build Spawn Setup produced.</summary>
    public class AgentSetupAssetsTests
    {
        private const string DifficultyPath = "Assets/ScriptableObjects/Agents/DifficultyConfig.asset";
        private const string ScenePath = "Assets/Scenes/Agents.unity";

        private static GameObject Prefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Agents/{name}.prefab");
            Assert.IsNotNull(prefab, $"{name}.prefab is missing. Run Blackout > Agents > Build Spawn Setup.");
            return prefab;
        }

        [Test]
        public void DifficultyConfigAsset_HoldsThePrototypeNumbers()
        {
            var config = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(DifficultyPath);

            Assert.IsNotNull(config);
            Assert.AreEqual(90f, config.Get(AgentKind.Hunter).maxHealth);
            Assert.AreEqual(1f, config.healthMultiplier);
        }

        [TestCase("Hunter")]
        [TestCase("SecurityPatrol")]
        [TestCase("TacticalGuard")]
        [TestCase("Commander")]
        public void EveryTypePrefab_IsAVariantOfAgentBase_WithTheSharedComponents(string name)
        {
            GameObject prefab = Prefab(name);

            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(prefab));
            Assert.IsNotNull(prefab.GetComponent<AgentRoot>());
            Assert.IsNotNull(prefab.GetComponent<PathFollower>());
            Assert.IsNotNull(prefab.GetComponent<AgentShooter>());
            Assert.IsNotNull(prefab.GetComponent<Health>());
            Assert.AreEqual(LayerMask.NameToLayer("Enemy"), prefab.layer);
        }

        // Until its model exists a humanoid stands in with PLACEHOLDER_Humanoid; after Blackout > Art > Use Character Models In Agents the
        // Core Developer's Char_<Name>_Visual is nested in the same Visual slot instead (the way the Tactical Guard nests its robot).
        [TestCase("Hunter", "Char_Hunter_Visual")]
        [TestCase("SecurityPatrol", "Char_Patrol_Visual")]
        [TestCase("Commander", "Char_Commander_Visual")]
        public void HumanoidPrefabs_HaveABody_WithoutACollider(string name, string modelName)
        {
            Transform visual = Prefab(name).transform.Find("Visual");
            Assert.IsNotNull(visual, "the empty Visual child the body nests in");

            Transform body = visual.Find("PLACEHOLDER_Humanoid");
            if (body == null)
            {
                body = visual.Find(modelName);
            }

            Assert.IsNotNull(body, $"a PLACEHOLDER_Humanoid or the {modelName} model stands in the Visual slot");
            Assert.AreEqual(0, body.GetComponentsInChildren<Collider>(true).Length, "the agent's own capsule is the collider");
        }

        [Test]
        public void TacticalGuard_NestsTheRobotVisual_WithTheProceduralAnimator()
        {
            GameObject guard = Prefab("TacticalGuard");

            Assert.IsNotNull(guard.GetComponentInChildren<ProceduralRobotAnimator>(true));
            Assert.IsNotNull(guard.transform.Find("Visual/SecurityRobot_Visual"));
            Assert.IsNull(guard.transform.Find("Visual/PLACEHOLDER_Humanoid"));
        }

        [Test]
        public void TacticalGuard_FiresFromTheRobotsMuzzle()
        {
            var shooter = Prefab("TacticalGuard").GetComponent<AgentShooter>();

            var serialized = new SerializedObject(shooter);
            var muzzle = serialized.FindProperty("muzzle").objectReferenceValue as Transform;
            Assert.IsNotNull(muzzle);
            Assert.AreEqual("MuzzlePoint", muzzle.name);
        }

        [Test]
        public void EyeHeights_FollowTheReference()
        {
            Assert.AreEqual(1.95f, new SerializedObject(Prefab("TacticalGuard").GetComponent<AgentShooter>()).FindProperty("eyeHeight").floatValue, 0.0001f);
            Assert.AreEqual(1.6f, new SerializedObject(Prefab("Hunter").GetComponent<AgentShooter>()).FindProperty("eyeHeight").floatValue, 0.0001f);
        }

        [Test]
        public void AgentsScene_HoldsTheRegistryHolderAndASpawnerWithFourEntries()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                AgentSpawner spawner = FindInScene<AgentSpawner>(scene);
                Assert.IsNotNull(spawner);
                Assert.IsNotNull(FindInScene<AgentRegistry>(scene));
                BlackboardHolder holder = FindInScene<BlackboardHolder>(scene);
                Assert.IsNotNull(holder);

                var serialized = new SerializedObject(spawner);
                Assert.IsNotNull(serialized.FindProperty("difficulty").objectReferenceValue);
                SerializedProperty entries = serialized.FindProperty("entries");
                Assert.AreEqual(4, entries.arraySize);
                for (int i = 0; i < entries.arraySize; i++)
                {
                    Assert.IsNotNull(entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue, $"entry {i} has a prefab");
                }

                var holderSerialized = new SerializedObject(holder);
                Assert.IsNotNull(holderSerialized.FindProperty("generatorDisabled").objectReferenceValue);
                Assert.IsNotNull(holderSerialized.FindProperty("cureTaken").objectReferenceValue);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
