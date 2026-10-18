// Owner: Agent Controller.
using System.IO;
using Blackout.AI.Commander;
using Blackout.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Agents.Tests
{
    public sealed class CommanderSetupAssetsTests
    {
        [Test]
        public void CommanderPrefab_HasBrainAndConfigAssignedToAgentRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CommanderSetupBuilder.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var brain = prefab.GetComponent<CommanderBrain>();
            Assert.That(brain, Is.Not.Null);
            Assert.That(prefab.GetComponent<CommanderDirectiveGizmos>(), Is.Not.Null);
            Assert.That(brain.Config, Is.Not.Null);
            Assert.That(new SerializedObject(prefab.GetComponent<AgentRoot>())
                .FindProperty("brainBehaviour").objectReferenceValue, Is.SameAs(brain));
            Assert.That(brain.Config.alertSpeedMultiplier, Is.EqualTo(1.25f));
            Assert.That(brain.Config.alertVisionMultiplier, Is.EqualTo(1.4f));
            Assert.That(brain.Config.alertAimErrorMultiplier, Is.EqualTo(0.9f));
        }

        [Test]
        public void AgentsScene_HasExistingPlayerLifecycleChannelsAssigned()
        {
            Scene scene = SceneManager.GetSceneByPath(CommanderSetupBuilder.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(CommanderSetupBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                BlackboardHolder holder = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.GetComponentInChildren<BlackboardHolder>(true) != null)
                        holder = root.GetComponentInChildren<BlackboardHolder>(true);
                Assert.That(holder, Is.Not.Null);
                var data = new SerializedObject(holder);
                Assert.That(data.FindProperty("playerDied").objectReferenceValue,
                    Is.EqualTo(AssetDatabase.LoadMainAssetAtPath("Assets/ScriptableObjects/Events/PlayerDied.asset")));
                Assert.That(data.FindProperty("playerRespawned").objectReferenceValue,
                    Is.EqualTo(AssetDatabase.LoadMainAssetAtPath("Assets/ScriptableObjects/Events/PlayerRespawned.asset")));
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SetupRerun_PreservesSerializedAssetsExactly()
        {
            string prefab = File.ReadAllText(CommanderSetupBuilder.PrefabPath);
            string scene = File.ReadAllText(CommanderSetupBuilder.ScenePath);
            string config = File.ReadAllText(CommanderSetupBuilder.ConfigPath);

            CommanderSetupBuilder.Build();

            Assert.That(File.ReadAllText(CommanderSetupBuilder.PrefabPath), Is.EqualTo(prefab));
            Assert.That(File.ReadAllText(CommanderSetupBuilder.ScenePath), Is.EqualTo(scene));
            Assert.That(File.ReadAllText(CommanderSetupBuilder.ConfigPath), Is.EqualTo(config));
        }
    }
}
