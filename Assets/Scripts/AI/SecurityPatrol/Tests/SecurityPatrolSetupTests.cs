// Owner: World Builder (Security Patrol AI).
using Blackout.Agents;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.AI.SecurityPatrol.Tests
{
    /// <summary>Read-only checks of the actual prefab/config used by the Agents scene's spawner.</summary>
    public sealed class SecurityPatrolSetupTests
    {
        private const string PrefabPath = "Assets/Prefabs/Agents/SecurityPatrol.prefab";
        private const string ConfigPath = "Assets/Scripts/AI/SecurityPatrol/Config/SecurityPatrolConfig.asset";
        private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        private static SecurityPatrolConfig Config => AssetDatabase.LoadAssetAtPath<SecurityPatrolConfig>(ConfigPath);

        [Test]
        public void SavedVariant_HasOneFsmBrainSelectedByItsAgentRoot()
        {
            Assert.That(Prefab, Is.Not.Null);
            Assert.That(PrefabUtility.GetPrefabAssetType(Prefab), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(Prefab.GetComponents<IAgentBrain>().Length, Is.EqualTo(1));
            var brain = Prefab.GetComponent<SecurityPatrolBrain>();
            var root = Prefab.GetComponent<AgentRoot>();
            Assert.That(brain, Is.Not.Null);
            Assert.That(brain.enabled, Is.True);
            Assert.That(root.Kind, Is.EqualTo(AgentKind.SecurityPatrol));
            Assert.That(new SerializedObject(root).FindProperty("brainBehaviour").objectReferenceValue, Is.EqualTo(brain));
            Assert.That(brain.Config, Is.EqualTo(Config));
            Assert.That(Config, Is.Not.Null);
        }

        [Test]
        public void SavedChannels_UseTheExistingSharedNoiseAndRespawnAssets()
        {
            var fields = new SerializedObject(Prefab.GetComponent<SecurityPatrolBrain>());
            var noise = AssetDatabase.LoadAssetAtPath<NoiseEventChannel>("Assets/ScriptableObjects/Events/Noise.asset");
            var respawn = AssetDatabase.LoadAssetAtPath<VoidEventChannel>("Assets/ScriptableObjects/Events/PlayerRespawned.asset");
            Assert.That(fields.FindProperty("noiseChannel").objectReferenceValue, Is.EqualTo(noise));
            Assert.That(fields.FindProperty("playerRespawned").objectReferenceValue, Is.EqualTo(respawn));
            int mask = fields.FindProperty("geometryMask").intValue;
            Assert.That(PhysicsLayers.Contains(mask, PhysicsLayers.Level), Is.True);
            Assert.That(PhysicsLayers.Contains(mask, PhysicsLayers.Interactable), Is.True);
            Assert.That(PhysicsLayers.Contains(mask, PhysicsLayers.Enemy), Is.False);
            Assert.That(PhysicsLayers.Contains(mask, PhysicsLayers.Player), Is.False);
            Assert.That(new SerializedObject(Prefab.GetComponent<AgentShooter>()).FindProperty("gunfireChannel").objectReferenceValue,
                Is.EqualTo(noise));
        }

        [Test]
        public void SavedExecutors_MatchTheConfigAndReferenceWeaponNumbers()
        {
            MovementSettings movement = Prefab.GetComponent<PathFollower>().Settings;
            AgentWeaponConfig weapon = Prefab.GetComponent<AgentShooter>().Config;
            Assert.That(movement.walkSpeed, Is.EqualTo(Config.walkSpeed));
            Assert.That(movement.runSpeed, Is.EqualTo(Config.runSpeed));
            Assert.That(weapon.reactionDelay, Is.EqualTo(Config.reactionDelay));
            Assert.That(weapon.spread, Is.EqualTo(Config.aimError));
            Assert.That(weapon.range, Is.EqualTo(34f));
            Assert.That(weapon.burstCount, Is.EqualTo(3));
            Assert.That(weapon.burstInterval, Is.EqualTo(0.12f));
            Assert.That(weapon.burstPause, Is.EqualTo(1.05f));
        }

        [Test]
        public void SpawnerDifficultyRow_MatchesTheConfigWithDifficultyMultipliersApplied()
        {
            var difficulty = AssetDatabase.LoadAssetAtPath<DifficultyConfig>("Assets/ScriptableObjects/Agents/DifficultyConfig.asset");
            AgentStats stats = difficulty.Get(AgentKind.SecurityPatrol);
            Assert.That(stats.walkSpeed, Is.EqualTo(Config.walkSpeed));
            Assert.That(stats.runSpeed, Is.EqualTo(Config.runSpeed));
            Assert.That(stats.reactionDelay, Is.EqualTo(Config.reactionDelay * difficulty.reactionDelayMultiplier));
            Assert.That(stats.spread, Is.EqualTo(Config.aimError * difficulty.aimErrorMultiplier));
        }

        [Test]
        public void AgentsScene_SpawnsThisConfiguredPrefabForPatrolMarkers()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Agents.unity", OpenSceneMode.Additive);
            try
            {
                AgentSpawner spawner = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    AgentSpawner found = root.GetComponentInChildren<AgentSpawner>(true);
                    if (found != null) spawner = found;
                }
                Assert.That(spawner, Is.Not.Null);
                var entries = new SerializedObject(spawner).FindProperty("entries");
                int matches = 0;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("kind").enumValueIndex != (int)AgentKind.SecurityPatrol) continue;
                    matches++;
                    Assert.That(entry.FindPropertyRelative("prefab").objectReferenceValue, Is.EqualTo(Prefab));
                    Assert.That(entry.FindPropertyRelative("ownerPrefix").stringValue, Is.EqualTo("Patrol-"));
                }
                Assert.That(matches, Is.EqualTo(1));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
