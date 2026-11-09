using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public sealed class CommanderHeroCombatTests
    {
        [Test]
        public void SpawnDifficulty_KeepsCommanderHeroHealthAndPistolStats()
        {
            var difficulty = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(
                "Assets/ScriptableObjects/Agents/DifficultyConfig.asset");
            Assert.That(difficulty, Is.Not.Null);
            AgentStats stats = difficulty.Get(AgentKind.Commander);
            Assert.That(stats.maxHealth, Is.EqualTo(180f));
            Assert.That(stats.damage, Is.EqualTo(10f));
            Assert.That(stats.burstCount, Is.EqualTo(1));
            Assert.That(stats.spread, Is.EqualTo(0.025f));
            Assert.That(stats.reactionDelay, Is.EqualTo(0.45f));
        }

        [Test]
        public void CommanderPrefab_PistolPointsForwardAndDamagesPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Agents/Commander.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject commander = Object.Instantiate(prefab);
            GameObject target = new GameObject("Player target");
            try
            {
                Random.InitState(1);
                commander.transform.position = new Vector3(1000f, 0f, 1000f);
                target.transform.position = commander.transform.position + Vector3.forward * 5f;
                target.layer = PhysicsLayers.Player;
                var collider = target.AddComponent<BoxCollider>();
                collider.center = Vector3.up;
                collider.size = new Vector3(2f, 2f, 2f);
                var targetHealth = target.AddComponent<Health>();
                targetHealth.ResetHealth(0f);
                var commanderHealth = commander.GetComponent<Health>();
                var shooter = commander.GetComponent<AgentShooter>();
                Assert.That(commanderHealth.Max, Is.EqualTo(180f));
                Assert.That(shooter.Config.damage, Is.EqualTo(10f));
                var shooterData = new SerializedObject(shooter);
                var muzzle = shooterData.FindProperty("muzzle").objectReferenceValue as Transform;
                Assert.That(muzzle, Is.Not.Null);
                Assert.That(commander.transform.InverseTransformPoint(muzzle.position).z, Is.GreaterThan(0.4f));

                Physics.SyncTransforms();
                shooter.Apply(new AgentIntent
                {
                    fire = true, aim = true, hasLookTarget = true,
                    lookTarget = target.transform.position + Vector3.up * 1.2f
                });
                shooter.Tick(0.6f);

                Assert.That(shooter.ShotsFired, Is.EqualTo(1));
                Assert.That(targetHealth.Current, Is.EqualTo(targetHealth.Max - 10f));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(commander);
            }
        }
    }
}
