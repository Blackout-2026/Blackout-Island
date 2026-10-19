using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Weapons.Tests
{
    /// <summary>
    /// The player's weapons hit only PhysicsLayers.PlayerShots (docs/reference/systems-engineer.md section 12).
    /// The shot tests raycast in EditMode against primitives, after Physics.SyncTransforms, because Auto Sync
    /// Transforms is off.
    /// </summary>
    public class WeaponMaskTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        private readonly List<Object> created = new List<Object>();
        private GameObject shooter;
        private HitscanWeapon gun;
        private readonly List<PelletTrace> traces = new List<PelletTrace>();

        [SetUp]
        public void SetUp()
        {
            shooter = Track(new GameObject("Shooter"));
            shooter.layer = PhysicsLayers.Player;

            var data = Track(ScriptableObject.CreateInstance<WeaponData>());
            data.spread = 0f;
            data.moveSpread = 0f;
            data.bloomPerShot = 0f;
            data.tracerEvery = 0;

            gun = shooter.AddComponent<HitscanWeapon>();
            var serialized = new SerializedObject(gun);
            serialized.FindProperty("data").objectReferenceValue = data;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var context = new WeaponContext(shooter.transform, shooter, PhysicsLayers.PlayerShots, null, null, null, null);
            context.PelletTraced = traces.Add;
            gun.Initialize(context);
            traces.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        [Test]
        public void NewWeaponController_DefaultsToPlayerShots()
        {
            var controller = shooter.AddComponent<WeaponController>();
            Assert.AreEqual(PhysicsLayers.PlayerShots, new SerializedObject(controller).FindProperty("hitMask").intValue);
        }

        [Test]
        public void PlayerPrefab_WeaponsUsePlayerShots()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, PlayerPrefabPath);
            var controller = prefab.GetComponentInChildren<WeaponController>(true);
            Assert.IsNotNull(controller, "WeaponController on the Player prefab");
            Assert.AreEqual(PhysicsLayers.PlayerShots, new SerializedObject(controller).FindProperty("hitMask").intValue);
        }

        [Test]
        public void Shot_HitsAnAgent_AndDamagesIt()
        {
            GameObject agent = Box("Agent", PhysicsLayers.Enemy, 5f);
            var health = agent.AddComponent<Health>();
            health.ResetHealth(0f);

            Fire();

            Assert.AreEqual(1, traces.Count);
            Assert.AreSame(agent.GetComponent<Collider>(), traces[0].Hit);
            Assert.Less(health.Current, health.Max);
        }

        [TestCase(PhysicsLayers.UI)]
        [TestCase(PhysicsLayers.Water)]
        [TestCase(PhysicsLayers.TransparentFX)]
        [TestCase(PhysicsLayers.IgnoreRaycast)]
        [TestCase(PhysicsLayers.Ragdoll)]
        [TestCase(PhysicsLayers.Player)]
        public void Shot_PassesThroughLayersOutsideTheMask(int layer)
        {
            Box("In the way", layer, 3f);
            GameObject agent = Box("Agent", PhysicsLayers.Enemy, 5f);

            Fire();

            Assert.AreSame(agent.GetComponent<Collider>(), traces[0].Hit);
        }

        [TestCase(PhysicsLayers.Default)]
        [TestCase(PhysicsLayers.Level)]
        [TestCase(PhysicsLayers.Interactable)]
        public void Shot_IsStoppedByTheWorldAndDoors(int layer)
        {
            GameObject wall = Box("Wall", layer, 3f);
            Box("Agent", PhysicsLayers.Enemy, 5f);

            Fire();

            Assert.AreSame(wall.GetComponent<Collider>(), traces[0].Hit);
        }

        [Test]
        public void Shot_HitsAHitbox_AndDamagesItsOwner()
        {
            GameObject agent = Track(new GameObject("Agent"));
            var health = agent.AddComponent<Health>();
            health.ResetHealth(0f);
            GameObject head = Box("Head", PhysicsLayers.Hit, 5f);
            head.transform.SetParent(agent.transform, true);

            Fire();

            Assert.AreSame(head.GetComponent<Collider>(), traces[0].Hit);
            Assert.Less(health.Current, health.Max);
        }

        [Test]
        public void Shot_TriggersDoNotStopIt()
        {
            Box("Pickup", PhysicsLayers.Interactable, 3f).GetComponent<Collider>().isTrigger = true;
            GameObject agent = Box("Agent", PhysicsLayers.Enemy, 5f);

            Fire();

            Assert.AreSame(agent.GetComponent<Collider>(), traces[0].Hit);
        }

        [Test]
        public void Shot_ThatHitsNothing_ReportsTheRange()
        {
            Fire();

            Assert.IsNull(traces[0].Hit);
            Assert.AreEqual(traces[0].Range, traces[0].Distance);
        }

        private void Fire()
        {
            Physics.SyncTransforms();
            gun.Fire();
        }

        /// <summary>A 1 m cube on a layer, straight ahead of the shooter (who looks down +Z from the origin).</summary>
        private GameObject Box(string name, int layer, float distance)
        {
            GameObject box = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            box.name = name;
            box.layer = layer;
            box.transform.position = new Vector3(0f, 0f, distance);
            return box;
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
