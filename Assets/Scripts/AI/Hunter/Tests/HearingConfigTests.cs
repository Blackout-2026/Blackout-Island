// Owner: Systems Engineer (Hunter AI).
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>The defaults are the prototype's numbers (agents/perception.js); see section 14 of the reference.</summary>
    public class HearingConfigTests
    {
        private HearingConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<HearingConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Defaults_AreThePrototypes()
        {
            Assert.AreEqual(1.7f, config.hearing);
            Assert.AreEqual(0.35f, config.wallFactor);
            Assert.AreEqual(3, config.maxWalls);
            Assert.AreEqual(0.04f, config.threshold);
            Assert.AreEqual(1.2f, config.sourceHeight);
            Assert.AreEqual(1.6f, config.listenerHeight);
            Assert.AreEqual(8f, config.memorySeconds);
        }

        [Test]
        public void Occluders_AreLevelAndInteractableOnly()
        {
            int mask = config.occluders;

            Assert.IsTrue(PhysicsLayers.Contains(mask, PhysicsLayers.Level));
            Assert.IsTrue(PhysicsLayers.Contains(mask, PhysicsLayers.Interactable));
            Assert.AreEqual(1 << PhysicsLayers.Level | 1 << PhysicsLayers.Interactable, mask);
        }

        [Test]
        public void PerceivedWithTheConfig_UsesItsHearingAndWallFactor()
        {
            Assert.AreEqual(0.2975f, HearingMath.Perceived(config, 13f, 6.5f, 1), 1e-5f);
        }
    }
}
