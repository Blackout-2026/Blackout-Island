// Owner: Systems Engineer. MC1 rules and saved prefab integration.
using System.Collections.Generic;
using System.Reflection;
using Blackout.Hud;
using Blackout.Interaction;
using Blackout.Mission;
using Blackout.Player;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Player.Tests
{
    public class MedkitCompassTests
    {
        private readonly List<Object> created = new List<Object>();
        private Health health;
        private MedkitInventory inventory;

        [SetUp]
        public void SetUp()
        {
            var player = Track(new GameObject("Medkit test player"));
            health = player.AddComponent<Health>();
            health.ResetHealth(0f);
            inventory = player.AddComponent<MedkitInventory>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        private T Track<T>(T value) where T : Object { created.Add(value); return value; }
        private void Damage(float amount) => health.TakeDamage(new DamageInfo(amount, Vector3.zero, Vector3.forward, null, DamageType.Generic));

        [Test]
        public void FullHealth_CanStoreButCannotSpendAKit()
        {
            Assert.That(inventory.Count, Is.Zero);
            Assert.That(inventory.TryUse(), Is.Zero);
            Assert.That(inventory.AddMedkit(), Is.True);
            Assert.That(inventory.TryUse(), Is.Zero);
            Assert.That(inventory.Count, Is.EqualTo(1));
        }

        [TestCase(50f, 25f, 75f)]
        [TestCase(10f, 10f, 100f)]
        public void Use_HealsUpTo25AndSpendsExactlyOne(float damage, float healing, float remaining)
        {
            Damage(damage);
            inventory.AddMedkit();
            Assert.That(inventory.TryUse(), Is.EqualTo(healing));
            Assert.That(health.Current, Is.EqualTo(remaining));
            Assert.That(inventory.Count, Is.Zero);
            Assert.That(inventory.TryUse(), Is.Zero);
        }

        [Test]
        public void DeadPlayer_CannotCollectOrUseStoredKits()
        {
            inventory.AddMedkit();
            Damage(1000f);
            Assert.That(inventory.AddMedkit(), Is.False);
            Assert.That(inventory.TryUse(), Is.Zero);
            Assert.That(inventory.Count, Is.EqualTo(1));
        }

        [Test]
        public void Snapshot_RepeatedlyRestoresCountAndNotifiesHud()
        {
            inventory.AddMedkit();
            object saved = inventory.CaptureCheckpoint();
            int shown = -1;
            inventory.CountChanged += count => shown = count;
            for (int i = 0; i < 2; i++)
            {
                Damage(40f);
                inventory.TryUse();
                inventory.RestoreCheckpoint(saved);
                Assert.That(inventory.Count, Is.EqualTo(1));
                Assert.That(shown, Is.EqualTo(1));
            }
        }

        [Test]
        public void Pickup_IsCollectedOnceAtFullHealthAndCanBeRestored()
        {
            var pickup = Track(new GameObject("Medkit pickup")).AddComponent<MedkitPickup>();
            object saved = pickup.CaptureCheckpoint();
            Assert.That(pickup.TryCollect(inventory), Is.True);
            Assert.That(pickup.TryCollect(inventory), Is.False);
            Assert.That(inventory.Count, Is.EqualTo(1));
            Assert.That(pickup.gameObject.activeSelf, Is.False);
            pickup.RestoreCheckpoint(saved);
            Assert.That(pickup.gameObject.activeSelf, Is.True);
            Assert.That(pickup.IsTaken, Is.False);
            Assert.That(pickup.TryCollect(null), Is.False);
        }

        [Test]
        public void EnemyDrops_UseOneSubscriptionAndPutKitOppositeGun()
        {
            var gunTemplate = Track(new GameObject("Gun template"));
            var kitTemplate = Track(new GameObject("Kit template"));
            var enemy = Track(new GameObject("Enemy"));
            var enemyHealth = enemy.AddComponent<Health>();
            enemyHealth.ResetHealth(0f);
            var drop = enemy.AddComponent<EnemyWeaponDrop>();
            var fields = new SerializedObject(drop);
            fields.FindProperty("dropPrefab").objectReferenceValue = gunTemplate;
            fields.FindProperty("medkitPrefab").objectReferenceValue = kitTemplate;
            fields.ApplyModifiedPropertiesWithoutUndo();
            var enable = typeof(EnemyWeaponDrop).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);
            enable.Invoke(drop, null);
            enable.Invoke(drop, null);
            enemyHealth.TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null, DamageType.Generic));
            Track(drop.LastDrop);
            Track(drop.LastMedkitDrop);
            Assert.That(drop.LastDrop.transform.position.x, Is.EqualTo(0.6f).Within(0.01f));
            Assert.That(drop.LastMedkitDrop.transform.position.x, Is.EqualTo(-0.6f).Within(0.01f));
            Assert.That(GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None),
                Has.Exactly(1).Matches<Transform>(t => t.name == "Kit template(Clone)"));
            var last = drop.LastMedkitDrop;
            enemy.SetActive(false);
            enemy.SetActive(true);
            enemyHealth.TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null, DamageType.Generic));
            Assert.That(drop.LastMedkitDrop, Is.SameAs(last));
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, 90f)]
        [TestCase(0f, -1f, 180f)]
        [TestCase(-1f, 0f, 270f)]
        public void Compass_UsesUnityNorthAndClockwiseDegrees(float x, float z, float expected)
        {
            Assert.That(CompassMath.Bearing(new Vector3(x, 100f, z)), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void Compass_WrapsThroughNorthAndClampsDistantPointer()
        {
            Vector3 eastOfNorth = Quaternion.Euler(0f, 1f, 0f) * Vector3.forward;
            Assert.That(CompassMath.MarkerOffset(359f, eastOfNorth), Is.EqualTo(4f).Within(0.001f));
            Assert.That(CompassMath.MarkerOffset(0f, Vector3.back), Is.EqualTo(224f));
        }

        [TestCase(MissionPhase.Beach, "DoorSocket_D-Bunker")]
        [TestCase(MissionPhase.Forest, "DoorSocket_D-Bunker")]
        [TestCase(MissionPhase.Corridor, "DoorSocket_D-Generator")]
        [TestCase(MissionPhase.GeneratorRoom, "GeneratorSocket")]
        [TestCase(MissionPhase.Lab, "CureSocket")]
        [TestCase(MissionPhase.Complete, null)]
        public void Navigation_FollowsTheMission(MissionPhase phase, string expected)
        {
            Assert.That(MissionNavigation.TargetMarker(phase, false, false, Vector3.zero, out _), Is.EqualTo(expected));
        }

        [Test]
        public void Navigation_UsesRampAfterDoorAndRoutesEscapeBackOutside()
        {
            Assert.That(MissionNavigation.TargetMarker(MissionPhase.BunkerEntrance, false, true, Vector3.zero, out _),
                Is.EqualTo("DoorSocket_D-Security"));
            Assert.That(MissionNavigation.TargetMarker(MissionPhase.Escape, true, true, new Vector3(0f, 2.5f, 135f), out _),
                Is.EqualTo("DoorSocket_D-Security"));
            Assert.That(MissionNavigation.TargetMarker(MissionPhase.Escape, true, true, new Vector3(0f, 6f, 60f), out _),
                Is.EqualTo("DoorSocket_D-Bunker"));
            Assert.That(MissionNavigation.TargetMarker(MissionPhase.Escape, true, true, new Vector3(0f, 10f, 40f), out string label),
                Is.EqualTo("BoatExit"));
            Assert.That(label, Is.EqualTo("BOAT"));
        }

        [TestCase("SecurityPatrol")]
        [TestCase("Hunter")]
        [TestCase("TacticalGuard")]
        [TestCase("Commander")]
        public void EnemyPrefabs_ReferenceTheStoredKit(string name)
        {
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Agents/" + name + ".prefab");
            var drop = enemy.GetComponent<EnemyWeaponDrop>();
            Assert.That(drop, Is.Not.Null);
            Assert.That(enemy.GetComponents<EnemyWeaponDrop>().Length, Is.EqualTo(1));
            Assert.That(new SerializedObject(drop).FindProperty("medkitPrefab").objectReferenceValue,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/MedkitDrop.prefab")));
        }

        [Test]
        public void PlayerPrefab_HasInventoryAndConnectedHudReferences()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            Assert.That(player.GetComponent<MedkitInventory>(), Is.Not.Null);
            Assert.That(new SerializedObject(player.GetComponent<MedkitInventory>()).FindProperty("input").objectReferenceValue,
                Is.SameAs(player.GetComponent<PlayerInputReader>()));
            var hud = player.transform.Find("HUD");
            AssertReferences(hud.GetComponent<MedkitCounter>(), "inventory", "value");
            AssertReferences(hud.GetComponent<CompassHud>(), "player", "playerHealth", "root", "strip", "objectiveMarker", "heading", "destination");
            Assert.That(hud.Find("Medkits"), Is.Not.Null);
            Assert.That(hud.Find("Compass/Viewport/Strip").childCount, Is.EqualTo(73));
        }

        private static void AssertReferences(Component component, params string[] names)
        {
            Assert.That(component, Is.Not.Null);
            var serialized = new SerializedObject(component);
            foreach (string name in names) Assert.That(serialized.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
        }
    }
}
