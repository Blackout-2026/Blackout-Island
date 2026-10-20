// Owner: Systems Engineer (Hunter AI).
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>The defaults are the prototype's numbers (agents/hunter_bt.js, agents/perception.js); section 15.</summary>
    public class HunterConfigTests
    {
        private HunterConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<HunterConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Vision_IsThePrototypesHunter()
        {
            Assert.AreEqual(2f, config.fieldOfViewDegrees * Mathf.Deg2Rad, 1e-3f, "2.0 rad");
            Assert.AreEqual(25f, config.sightRange);
            Assert.AreEqual(1.6f, config.eyeHeight);
            Assert.AreEqual(2.2f, config.closeSenseRange);
            Assert.AreEqual(2.2f, config.awarenessRate);
            Assert.AreEqual(1f, config.detectThreshold);
            Assert.AreEqual(6f, config.loseDetectionSeconds);
        }

        [Test]
        public void Tree_IsThePrototypesHunter()
        {
            Assert.AreEqual(32f, config.attackRange);
            Assert.AreEqual(6f, config.closeInDistance);
            Assert.AreEqual(10f, config.searchMemorySeconds);
            Assert.AreEqual(4f, config.ambushSeconds);
            Assert.AreEqual(16f, config.gunfireCloseDistance);
            Assert.AreEqual(2.5f, config.holdCoverSeconds);
            Assert.AreEqual(0.06f, config.gunfireThreshold);
            Assert.AreEqual(0.04f, config.preyNoiseThreshold);
            Assert.AreEqual(25f, config.moveTimeoutSeconds);
        }

        [Test]
        public void Camouflage_HalvesDetectionWhileStill()
        {
            Assert.AreEqual(0.5f, config.camouflageMultiplier);
        }

        [Test]
        public void SightBlockers_AreWallsAndDoors()
        {
            Assert.AreEqual(1 << PhysicsLayers.Level | 1 << PhysicsLayers.Interactable, (int)config.sightBlockers);
        }
    }
}
