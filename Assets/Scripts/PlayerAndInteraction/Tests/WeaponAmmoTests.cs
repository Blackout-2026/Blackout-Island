using System.Linq;
using Blackout.Hud;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Interaction.Tests
{
    /// <summary>Step F2: a 45 round SMG magazine, a 10 round sniper magazine, a 1 second reload when a gun empties, and the ammunition on dead bodies.</summary>
    public class WeaponAmmoTests
    {
        private const string SmgPath = "Assets/ScriptableObjects/Weapons/SMG.asset";
        private const string RiflePath = "Assets/ScriptableObjects/Weapons/Rifle.asset";
        private const string KnifePath = "Assets/ScriptableObjects/Weapons/Knife.asset";

        // ---- the rules ------------------------------------------------------------------------------------------------

        [Test]
        public void AFullMagazine_FiresItsRoundsThenReloadsForOneSecondFromASpareMagazine()
        {
            var ammo = new WeaponAmmo(45, 2, 1f);
            Assert.That(ammo.Loaded, Is.EqualTo(45));
            Assert.That(ammo.ReserveRounds, Is.EqualTo(90));

            for (int i = 0; i < 45; i++)
            {
                Assert.That(ammo.CanFire, Is.True, "round " + i);
                Assert.That(ammo.Consume(), Is.True);
            }

            Assert.That(ammo.Loaded, Is.Zero);
            Assert.That(ammo.Reloading, Is.True, "the emptied gun starts to reload");
            Assert.That(ammo.CanFire, Is.False);
            Assert.That(ammo.Consume(), Is.False, "no round while reloading");

            Assert.That(ammo.Tick(0.9f), Is.False);
            Assert.That(ammo.CanFire, Is.False, "still reloading after 0.9 s");
            Assert.That(ammo.Tick(0.11f), Is.True, "the reload ends after 1 s");
            Assert.That(ammo.Loaded, Is.EqualTo(45));
            Assert.That(ammo.SpareMagazines, Is.EqualTo(1));
            Assert.That(ammo.CanFire, Is.True);
        }

        [Test]
        public void WithNoSpareMagazine_TheEmptyGunStaysEmptyUntilBodyAmmunitionIsTaken()
        {
            var ammo = new WeaponAmmo(10, 0, 1f);
            for (int i = 0; i < 10; i++)
            {
                ammo.Consume();
            }

            Assert.That(ammo.Loaded, Is.Zero);
            Assert.That(ammo.Reloading, Is.False, "nothing to reload from");
            Assert.That(ammo.CanFire, Is.False);

            ammo.AddMagazines(1);
            Assert.That(ammo.Reloading, Is.True, "a magazine from a body starts the reload at once");
            ammo.Tick(1.01f);
            Assert.That(ammo.Loaded, Is.EqualTo(10));
            Assert.That(ammo.SpareMagazines, Is.Zero);
        }

        [Test]
        public void TheKnifeHasNoAmmunition_AndACheckpointKeepsTheRounds()
        {
            var knife = new WeaponAmmo(0, 0, 1f);
            Assert.That(knife.IsLimited, Is.False);
            Assert.That(knife.CanFire, Is.True);
            Assert.That(knife.Consume(), Is.True);

            var ammo = new WeaponAmmo(45, 2, 1f);
            for (int i = 0; i < 5; i++)
            {
                ammo.Consume();
            }

            WeaponAmmo.State saved = ammo.Capture();
            for (int i = 0; i < 40; i++)
            {
                ammo.Consume();
            }

            ammo.Restore(saved);
            Assert.That(ammo.Loaded, Is.EqualTo(40));
            Assert.That(ammo.SpareMagazines, Is.EqualTo(2));
            Assert.That(ammo.Reloading, Is.False);
        }

        // ---- the numbers of the guns -----------------------------------------------------------------------------------

        [Test]
        public void TheGunsHaveTheRequestedMagazinesAndBodyAmmunition()
        {
            var smg = AssetDatabase.LoadAssetAtPath<WeaponData>(SmgPath);
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponData>(RiflePath);
            var knife = AssetDatabase.LoadAssetAtPath<WeaponData>(KnifePath);

            Assert.That(smg.magazineSize, Is.EqualTo(45));
            Assert.That(rifle.magazineSize, Is.EqualTo(10));
            Assert.That(smg.reloadTime, Is.EqualTo(1f));
            Assert.That(rifle.reloadTime, Is.EqualTo(1f));
            Assert.That(smg.magazinesPerBody, Is.EqualTo(2), "a dead body gives 2 SMG magazines");
            Assert.That(rifle.magazinesPerBody, Is.EqualTo(1), "and 1 sniper magazine");
            Assert.That(knife.magazineSize, Is.Zero);
            Assert.That(knife.magazinesPerBody, Is.Zero);
        }

        // ---- the loot and the HUD in the prefabs -----------------------------------------------------------------------

        [Test]
        public void TheAmmoDropIsATriggerWithAPickup_AndEveryEnemyDropsIt()
        {
            var drop = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/AmmoDrop.prefab");
            Assert.That(drop, Is.Not.Null, "run Blackout > Systems > Add Ammunition Loot And HUD");
            Assert.That(drop.GetComponent<AmmoPickup>(), Is.Not.Null);
            Assert.That(drop.GetComponent<Collider>().isTrigger, Is.True);

            foreach (string enemy in new[] { "SecurityPatrol", "Hunter", "TacticalGuard", "Commander" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Agents/" + enemy + ".prefab");
                var component = prefab.GetComponent<EnemyWeaponDrop>();
                Assert.That(component, Is.Not.Null, enemy);
                Assert.That(new SerializedObject(component).FindProperty("ammoPrefab").objectReferenceValue, Is.SameAs(drop), enemy);
            }
        }

        [Test]
        public void ThePlayerHud_ShowsRoundsOnEverySlot_AndOnlyASymbolAndANumberForTheMedkits()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            Transform hud = player.transform.Find("HUD");
            WeaponSlot[] slots = hud.Find("Weapons").GetComponentsInChildren<WeaponSlot>(true);
            Assert.That(slots.Length, Is.EqualTo(3));
            foreach (WeaponSlot slot in slots)
            {
                var serialized = new SerializedObject(slot);
                Assert.That(serialized.FindProperty("ammoLoaded").objectReferenceValue, Is.Not.Null, slot.name);
                Assert.That(serialized.FindProperty("ammoReserve").objectReferenceValue, Is.Not.Null, slot.name);
            }

            Transform medkits = hud.Find("Medkits");
            Assert.That(medkits.Find("Symbol"), Is.Not.Null, "a medkit symbol");
            Assert.That(medkits.Find("Count"), Is.Not.Null, "and the number");
            Assert.That(medkits.Find("Label"), Is.Null, "no MEDKITS caption");
            Assert.That(medkits.Find("Hint"), Is.Null, "and no Q hint");
            Assert.That(medkits.GetComponentsInChildren<Text>(true).Select(t => t.gameObject.name), Is.EquivalentTo(new[] { "Count" }));
        }

        [Test]
        public void TheSlotShowsLoadedAndReserveRounds_ReloadAndNoAmmo()
        {
            var root = new GameObject("Slot");
            try
            {
                var slot = root.AddComponent<WeaponSlot>();
                var loaded = new GameObject("Loaded").AddComponent<Text>();
                var reserve = new GameObject("Reserve").AddComponent<Text>();
                loaded.transform.SetParent(root.transform);
                reserve.transform.SetParent(root.transform);
                var serialized = new SerializedObject(slot);
                serialized.FindProperty("ammoLoaded").objectReferenceValue = loaded;
                serialized.FindProperty("ammoReserve").objectReferenceValue = reserve;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var ammo = new WeaponAmmo(45, 2, 1f);
                slot.SetAmmo(ammo);
                Assert.That(loaded.text, Is.EqualTo("45"));
                Assert.That(reserve.text, Is.EqualTo("/ 90"));

                for (int i = 0; i < 45; i++)
                {
                    ammo.Consume();
                }

                slot.SetAmmo(ammo);
                Assert.That(reserve.text, Is.EqualTo("RELOAD"));

                ammo.Tick(1.1f);
                for (int i = 0; i < 45; i++)
                {
                    ammo.Consume();
                }

                ammo.Tick(1.1f);
                for (int i = 0; i < 45; i++)
                {
                    ammo.Consume();
                }

                slot.SetAmmo(ammo);
                Assert.That(reserve.text, Is.EqualTo("NO AMMO"));

                slot.SetAmmo(new WeaponAmmo(0, 0, 1f));
                Assert.That(loaded.gameObject.activeSelf, Is.False, "the knife shows no rounds");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
