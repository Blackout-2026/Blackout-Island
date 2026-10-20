// Owner: Systems Engineer (Hunter AI).
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>
    /// The assets Blackout/Systems/Build Hunter Config makes, and the Hunter agent prefab it wires (H3, with the Agent
    /// Controller's permission): the brain and the ears on the root, each with its config and the Noise channel.
    /// </summary>
    public class HunterPrefabTests
    {
        private const string HunterPrefabPath = "Assets/Prefabs/Agents/Hunter.prefab";
        private const string HunterConfigPath = "Assets/Scripts/AI/Hunter/Config/HunterConfig.asset";
        private const string HearingConfigPath = "Assets/Scripts/AI/Hunter/Config/HearingConfig.asset";
        private const string NoisePath = "Assets/ScriptableObjects/Events/Noise.asset";

        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HunterPrefabPath);
            Assert.IsNotNull(prefab, HunterPrefabPath);
        }

        [Test]
        public void HunterConfigAsset_HoldsThePrototypeValues()
        {
            var asset = AssetDatabase.LoadAssetAtPath<HunterConfig>(HunterConfigPath);
            var defaults = ScriptableObject.CreateInstance<HunterConfig>();
            try
            {
                Assert.IsNotNull(asset, HunterConfigPath);
                Assert.AreEqual(JsonUtility.ToJson(defaults), JsonUtility.ToJson(asset),
                    "the asset still matches the documented defaults");
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }
        }

        [Test]
        public void HunterPrefab_HasTheHunterBrain_AndNoOtherBrain()
        {
            var brain = prefab.GetComponent<HunterBrain>();

            Assert.IsNotNull(brain);
            Assert.AreSame(brain, prefab.GetComponent<IAgentBrain>(), "AgentRoot finds this brain");
            Assert.AreEqual(1, prefab.GetComponents<IAgentBrain>().Length);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<HunterConfig>(HunterConfigPath), Field(brain, "config"));
            Assert.AreSame(prefab.GetComponent<HearingSystem>(), Field(brain, "hearing"));
        }

        [Test]
        public void HunterPrefab_HearsTheNoiseChannel_WithTheHearingConfig()
        {
            var hearing = prefab.GetComponent<HearingSystem>();

            Assert.IsNotNull(hearing);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<HearingConfig>(HearingConfigPath), Field(hearing, "config"));
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<NoiseEventChannel>(NoisePath), Field(hearing, "noiseChannel"));
        }

        private static Object Field(Component component, string name)
        {
            return new SerializedObject(component).FindProperty(name).objectReferenceValue;
        }
    }
}
