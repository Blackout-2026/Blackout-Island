using System.Collections.Generic;
using System.Reflection;
using Blackout.Shared;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Interaction.Tests
{
    /// <summary>
    /// The guns that dead enemies leave behind (step L1): the pickup gives the SMG first and the sniper rifle second, an enemy leaves one pickup when it dies,
    /// and the built prefabs are wired. Awake and OnEnable do not run in EditMode for added components, so the tests call OnEnable themselves.
    /// </summary>
    public class WeaponDropTests
    {
        private const string DropPath = "Assets/Prefabs/Interactables/WeaponDrop.prefab";
        private const string SmgPath = "Assets/ScriptableObjects/Weapons/SMG.asset";
        private const string RiflePath = "Assets/ScriptableObjects/Weapons/Rifle.asset";

        private static readonly string[] ArmedEnemies =
        {
            "Assets/Prefabs/Agents/SecurityPatrol.prefab",
            "Assets/Prefabs/Agents/Hunter.prefab",
            "Assets/Prefabs/Agents/Commander.prefab",
        };

        private sealed class FakeLoot : IWeaponLoot
        {
            private readonly Queue<WeaponData> guns;
            public FakeLoot(params WeaponData[] order) { guns = new Queue<WeaponData>(order); }
            public WeaponData NextLootWeapon() { return guns.Count > 0 ? guns.Peek() : null; }
            public WeaponData TakeNextLootWeapon() { return guns.Count > 0 ? guns.Dequeue() : null; }
        }

        private readonly List<Object> created = new List<Object>();
        private WeaponData smg;
        private WeaponData rifle;

        [SetUp]
        public void SetUp()
        {
            smg = Track(ScriptableObject.CreateInstance<WeaponData>());
            smg.displayName = "MP-9 SMG";
            rifle = Track(ScriptableObject.CreateInstance<WeaponData>());
            rifle.displayName = "R-700 Rifle";
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            created.Clear();
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }

        private static void Call(Component target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private WeaponDropPickup MakePickup(IWeaponLoot loot)
        {
            var root = Track(new GameObject("Pickup"));
            var smgModel = new GameObject("Smg");
            smgModel.transform.SetParent(root.transform);
            var sniperModel = new GameObject("Sniper");
            sniperModel.transform.SetParent(root.transform);

            var pickup = root.AddComponent<WeaponDropPickup>();
            var serialized = new SerializedObject(pickup);
            SerializedProperty list = serialized.FindProperty("showcases");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).FindPropertyRelative("weapon").objectReferenceValue = smg;
            list.GetArrayElementAtIndex(0).FindPropertyRelative("model").objectReferenceValue = smgModel;
            list.GetArrayElementAtIndex(1).FindPropertyRelative("weapon").objectReferenceValue = rifle;
            list.GetArrayElementAtIndex(1).FindPropertyRelative("model").objectReferenceValue = sniperModel;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            pickup.SetLoot(loot);
            return pickup;
        }

        // ---- the pickup ----------------------------------------------------------------------------------------

        [Test]
        public void Pickup_OffersTheNextGunAndNamesItInThePrompt()
        {
            WeaponDropPickup pickup = MakePickup(new FakeLoot(smg, rifle));

            Assert.AreSame(smg, pickup.Offer);
            Assert.AreEqual("Take the MP-9 SMG", pickup.Prompt);
            Assert.IsTrue(pickup.CanInteract);
        }

        [Test]
        public void Pickup_ShowsOnlyTheModelOfTheGunOnOffer()
        {
            var loot = new FakeLoot(smg, rifle);
            WeaponDropPickup pickup = MakePickup(loot);
            Transform smgModel = pickup.transform.Find("Smg");
            Transform sniperModel = pickup.transform.Find("Sniper");

            pickup.Refresh();
            Assert.IsTrue(smgModel.gameObject.activeSelf);
            Assert.IsFalse(sniperModel.gameObject.activeSelf);

            loot.TakeNextLootWeapon(); // the SMG was taken somewhere else
            pickup.Refresh();
            Assert.IsFalse(smgModel.gameObject.activeSelf);
            Assert.IsTrue(sniperModel.gameObject.activeSelf);
            Assert.AreEqual("Take the R-700 Rifle", pickup.Prompt);
        }

        [Test]
        public void Pickup_FirstTakenIsTheSmgAndTheSecondIsTheSniper()
        {
            var loot = new FakeLoot(smg, rifle);
            WeaponDropPickup first = MakePickup(loot);
            WeaponDropPickup second = MakePickup(loot);

            first.Interact(null);
            Assert.IsTrue(first.IsTaken);
            Assert.AreSame(rifle, second.Offer, "the SMG is gone, so the next pickup offers the sniper rifle");

            second.Interact(null);
            Assert.IsTrue(second.IsTaken);
            Assert.IsNull(MakePickup(loot).Offer, "both guns are taken");
        }

        [Test]
        public void Pickup_WithNothingLeftCannotBeUsed()
        {
            WeaponDropPickup pickup = MakePickup(new FakeLoot());

            Assert.IsFalse(pickup.CanInteract);
            Assert.AreEqual("Nothing left to take", pickup.Prompt);

            pickup.Interact(null);
            Assert.IsFalse(pickup.IsTaken);
        }

        [Test]
        public void Pickup_IsTakenOnce()
        {
            var loot = new FakeLoot(smg, rifle);
            WeaponDropPickup pickup = MakePickup(loot);

            pickup.Interact(null);
            pickup.Interact(null);

            Assert.AreSame(rifle, loot.NextLootWeapon(), "the second press did not take the sniper rifle too");
        }

        // ---- the enemy -------------------------------------------------------------------------------------------

        private (GameObject enemy, EnemyWeaponDrop drop, Health health) MakeEnemy(GameObject dropPrefab)
        {
            var enemy = Track(new GameObject("Enemy"));
            enemy.transform.position = new Vector3(10f, 0f, 5f);
            Health health = enemy.AddComponent<Health>();
            health.ResetHealth(0f);
            var drop = enemy.AddComponent<EnemyWeaponDrop>();
            var serialized = new SerializedObject(drop);
            serialized.FindProperty("dropPrefab").objectReferenceValue = dropPrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Call(drop, "OnEnable");
            return (enemy, drop, health);
        }

        private static DamageInfo Lethal()
        {
            return new DamageInfo(1000f, Vector3.zero, Vector3.forward, null, DamageType.Bullet);
        }

        [Test]
        public void Enemy_LeavesOnePickupBesideTheBodyWhenItDies()
        {
            var template = Track(new GameObject("Drop template"));
            var (enemy, drop, health) = MakeEnemy(template);

            Assert.IsNull(drop.LastDrop, "nothing is left while the enemy lives");

            health.TakeDamage(Lethal());
            health.TakeDamage(Lethal());

            Assert.IsNotNull(drop.LastDrop);
            Track(drop.LastDrop);
            Vector3 expected = enemy.transform.position + enemy.transform.right * 0.6f;
            Assert.AreEqual(expected.x, drop.LastDrop.transform.position.x, 0.01f);
            Assert.AreEqual(expected.z, drop.LastDrop.transform.position.z, 0.01f);
        }

        [Test]
        public void Enemy_WithoutAPrefabLeavesNothing()
        {
            var (_, drop, health) = MakeEnemy(null);

            health.TakeDamage(Lethal());

            Assert.IsNull(drop.LastDrop);
        }

        [Test]
        public void Enemy_StopsListeningWhenDisabled()
        {
            var template = Track(new GameObject("Drop template"));
            var (_, drop, health) = MakeEnemy(template);

            Call(drop, "OnDisable");
            health.TakeDamage(Lethal());

            Assert.IsNull(drop.LastDrop);
        }

        // ---- the built assets --------------------------------------------------------------------------------------

        [Test]
        public void Prefab_IsAnInteractableTriggerThatShowsTheSmgAndTheSniper()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DropPath);
            Assert.IsNotNull(prefab, "run Blackout > Systems > Build Weapon Drops");

            Assert.AreEqual(PhysicsLayers.Interactable, prefab.layer);
            Assert.IsTrue(prefab.GetComponent<Collider>().isTrigger);
            var pickup = prefab.GetComponent<WeaponDropPickup>();
            Assert.IsNotNull(pickup);

            var showcases = new SerializedObject(pickup).FindProperty("showcases");
            Assert.AreEqual(2, showcases.arraySize);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<WeaponData>(SmgPath), showcases.GetArrayElementAtIndex(0).FindPropertyRelative("weapon").objectReferenceValue);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<WeaponData>(RiflePath), showcases.GetArrayElementAtIndex(1).FindPropertyRelative("weapon").objectReferenceValue);
            Assert.IsNotNull(showcases.GetArrayElementAtIndex(0).FindPropertyRelative("model").objectReferenceValue);
            Assert.IsNotNull(showcases.GetArrayElementAtIndex(1).FindPropertyRelative("model").objectReferenceValue);
        }

        [Test]
        public void Prefab_ShowsNoHandsOrMuzzleFlashAndHasNoSolidColliders()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DropPath);

            foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
            {
                string n = child.name;
                bool handPart = n.StartsWith("Glove") || n.StartsWith("Sleeve") || n.StartsWith("Wrist") || n.StartsWith("Fingers") || n.StartsWith("MuzzleFlash");
                Assert.IsFalse(handPart && child.gameObject.activeSelf, n + " should be hidden");
            }

            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                Assert.IsTrue(collider.isTrigger, "only the trigger zone");
            }
        }

        [TestCaseSource(nameof(ArmedEnemies))]
        public void ArmedEnemyPrefab_DropsTheWeaponPrefab(string path)
        {
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var drop = enemy.GetComponent<EnemyWeaponDrop>();

            Assert.IsNotNull(drop, path + " carries a gun, so it drops one");
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<GameObject>(DropPath), new SerializedObject(drop).FindProperty("dropPrefab").objectReferenceValue);
        }

        [Test]
        public void TheRobotGuardDropsNothing()
        {
            var robot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Agents/TacticalGuard.prefab");

            Assert.IsNull(robot.GetComponent<EnemyWeaponDrop>());
        }
    }
}
