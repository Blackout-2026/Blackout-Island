using NUnit.Framework;
using UnityEngine;

namespace Blackout.Shared.Tests
{
    /// <summary>
    /// Pins the P0 layers and collision matrix (docs/UNITY_BUILD_PLAYBOOK.md P0, docs/reference/systems-engineer.md
    /// section 12) so a change to ProjectSettings fails here before it reaches a teammate's scene.
    /// </summary>
    public class PhysicsLayersTests
    {
        private static readonly int[] CoreLayers =
        {
            PhysicsLayers.Default, PhysicsLayers.Player, PhysicsLayers.Enemy, PhysicsLayers.Level,
            PhysicsLayers.Interactable,
        };

        [TestCase("Default", PhysicsLayers.Default)]
        [TestCase("TransparentFX", PhysicsLayers.TransparentFX)]
        [TestCase("Ignore Raycast", PhysicsLayers.IgnoreRaycast)]
        [TestCase("Water", PhysicsLayers.Water)]
        [TestCase("UI", PhysicsLayers.UI)]
        [TestCase("Player", PhysicsLayers.Player)]
        [TestCase("Enemy", PhysicsLayers.Enemy)]
        [TestCase("Level", PhysicsLayers.Level)]
        [TestCase("Interactable", PhysicsLayers.Interactable)]
        [TestCase("Hit", PhysicsLayers.Hit)]
        [TestCase("Ragdoll", PhysicsLayers.Ragdoll)]
        public void LayerConstant_MatchesProjectSettings(string name, int layer)
        {
            Assert.AreEqual(layer, LayerMask.NameToLayer(name));
        }

        [Test]
        public void Agents_DoNotBodyCollideWithEachOther()
        {
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(PhysicsLayers.Enemy, PhysicsLayers.Enemy));
        }

        [Test]
        public void Ragdoll_CollidesOnlyWithLevel()
        {
            for (int layer = 0; layer < 32; layer++)
            {
                bool collides = !Physics.GetIgnoreLayerCollision(PhysicsLayers.Ragdoll, layer);
                Assert.AreEqual(layer == PhysicsLayers.Level, collides, $"Ragdoll x layer {layer}");
            }
        }

        [Test]
        public void CoreLayers_CollideWithEachOther_ExceptEnemyWithEnemy()
        {
            foreach (int a in CoreLayers)
            {
                foreach (int b in CoreLayers)
                {
                    bool expected = !(a == PhysicsLayers.Enemy && b == PhysicsLayers.Enemy);
                    Assert.AreEqual(expected, !Physics.GetIgnoreLayerCollision(a, b),
                        $"{LayerMask.LayerToName(a)} x {LayerMask.LayerToName(b)}");
                }
            }
        }

        [TestCase(PhysicsLayers.Default)]
        [TestCase(PhysicsLayers.Level)]
        [TestCase(PhysicsLayers.Enemy)]
        [TestCase(PhysicsLayers.Interactable)]
        [TestCase(PhysicsLayers.Hit)]
        public void PlayerShots_HitTheWorldAgentsAndHitboxes(int layer)
        {
            Assert.IsTrue(PhysicsLayers.Contains(PhysicsLayers.PlayerShots, layer));
        }

        [TestCase(PhysicsLayers.Player)]
        [TestCase(PhysicsLayers.Ragdoll)]
        [TestCase(PhysicsLayers.UI)]
        [TestCase(PhysicsLayers.Water)]
        [TestCase(PhysicsLayers.TransparentFX)]
        [TestCase(PhysicsLayers.IgnoreRaycast)]
        [TestCase(12)]
        [TestCase(31)]
        public void PlayerShots_LeaveOutEverythingElse(int layer)
        {
            Assert.IsFalse(PhysicsLayers.Contains(PhysicsLayers.PlayerShots, layer));
        }

        [Test]
        public void AgentShots_HitThePlayerButNotOtherAgents()
        {
            Assert.IsTrue(PhysicsLayers.Contains(PhysicsLayers.AgentShots, PhysicsLayers.Player));
            Assert.IsTrue(PhysicsLayers.Contains(PhysicsLayers.AgentShots, PhysicsLayers.Default));
            Assert.IsTrue(PhysicsLayers.Contains(PhysicsLayers.AgentShots, PhysicsLayers.Level));
            Assert.IsTrue(PhysicsLayers.Contains(PhysicsLayers.AgentShots, PhysicsLayers.Interactable));
            Assert.IsFalse(PhysicsLayers.Contains(PhysicsLayers.AgentShots, PhysicsLayers.Enemy));
            Assert.IsFalse(PhysicsLayers.Contains(PhysicsLayers.AgentShots, PhysicsLayers.Hit));
            Assert.IsFalse(PhysicsLayers.Contains(PhysicsLayers.AgentShots, PhysicsLayers.Ragdoll));
        }
    }
}
