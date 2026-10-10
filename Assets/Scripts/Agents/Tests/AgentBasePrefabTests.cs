using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class AgentBasePrefabTests
    {
        private const string PrefabPath = "Assets/Prefabs/Agents/AgentBase.prefab";

        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"{PrefabPath} is missing. Run Blackout > Agents > Create Agent Base Prefab.");
        }

        [Test]
        public void IsOnTheEnemyLayer()
        {
            Assert.AreEqual(LayerMask.NameToLayer("Enemy"), prefab.layer);
        }

        [Test]
        public void HasAPlayerSizedCapsuleCollider()
        {
            var capsule = prefab.GetComponent<CapsuleCollider>();

            Assert.IsNotNull(capsule);
            Assert.AreEqual(1.8f, capsule.height, 0.001f);
            Assert.AreEqual(0.38f, capsule.radius, 0.001f);
            Assert.AreEqual(0.9f, capsule.center.y, 0.001f, "the capsule stands on the pivot at the agent's feet");
        }

        [Test]
        public void HasHealthAndAgentRoot()
        {
            Assert.IsNotNull(prefab.GetComponent<Health>());
            Assert.IsNotNull(prefab.GetComponent<AgentRoot>());
        }

        [Test]
        public void HasAPathFollowerWithTheReferenceSpeeds()
        {
            var follower = prefab.GetComponent<PathFollower>();

            Assert.IsNotNull(follower);
            Assert.IsInstanceOf<Blackout.Shared.IAgentMover>(follower);
            Assert.AreEqual(1.5f, follower.Settings.walkSpeed, 0.0001f);
            Assert.AreEqual(4f, follower.Settings.runSpeed, 0.0001f);
        }

        [Test]
        public void HasAnEmptyVisualChildForTheModelToNestIn()
        {
            Transform visual = prefab.transform.Find("Visual");

            Assert.IsNotNull(visual);
            Assert.AreEqual(0, visual.childCount);
        }
    }
}
