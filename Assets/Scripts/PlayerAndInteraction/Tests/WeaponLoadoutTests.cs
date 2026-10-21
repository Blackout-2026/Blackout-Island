using Blackout.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Player.Tests
{
    /// <summary>
    /// The loot progression of step L1: the storm leaves only the knife, then the first enemy gun taken is the SMG (slot 1) and the second is the
    /// sniper rifle (slot 2). The rules live in WeaponLoadout, so they are tested here without a scene; the controller part is checked before Awake.
    /// </summary>
    public class WeaponLoadoutTests
    {
        private WeaponData smg;
        private WeaponData rifle;
        private WeaponData knife;

        [SetUp]
        public void SetUp()
        {
            smg = Make("MP-9 SMG", 1, WeaponKind.Hitscan);
            rifle = Make("R-700 Rifle", 2, WeaponKind.Hitscan);
            knife = Make("Combat Knife", 3, WeaponKind.Melee);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(smg);
            Object.DestroyImmediate(rifle);
            Object.DestroyImmediate(knife);
        }

        private static WeaponData Make(string name, int slot, WeaponKind kind)
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            data.displayName = name;
            data.slot = slot;
            data.kind = kind;
            return data;
        }

        private WeaponLoadout Loadout(bool ownAll)
        {
            return new WeaponLoadout(new[] { smg, rifle, knife }, ownAll);
        }

        [Test]
        public void NormalStart_OwnsEveryWeaponAndHasNothingLeftToLoot()
        {
            WeaponLoadout loadout = Loadout(true);

            Assert.AreEqual(3, loadout.OwnedCount);
            Assert.IsTrue(loadout.Owns(smg) && loadout.Owns(rifle) && loadout.Owns(knife));
            Assert.AreEqual(-1, loadout.NextLootIndex(), "no gun is missing");
        }

        [Test]
        public void Storm_LeavesOnlyTheKnife()
        {
            WeaponLoadout loadout = Loadout(true);

            loadout.KeepOnlyMelee();

            Assert.AreEqual(1, loadout.OwnedCount);
            Assert.IsTrue(loadout.Owns(knife));
            Assert.IsFalse(loadout.Owns(smg));
            Assert.IsFalse(loadout.Owns(rifle));
            Assert.AreEqual(2, loadout.FirstOwned(), "the knife is the weapon in hand");
        }

        [Test]
        public void StartingAfterTheStorm_IsTheSameAsLosingEverythingButTheKnife()
        {
            WeaponLoadout loadout = Loadout(false);

            Assert.AreEqual(1, loadout.OwnedCount);
            Assert.IsTrue(loadout.Owns(knife));
        }

        [Test]
        public void Loot_IsTheSmgFirstThenTheSniperThenNothing()
        {
            WeaponLoadout loadout = Loadout(false);

            Assert.AreEqual(0, loadout.NextLootIndex(), "the first gun from a dead enemy is the SMG");
            Assert.IsTrue(loadout.Grant(loadout.NextLootIndex()));
            Assert.IsTrue(loadout.Owns(smg));
            Assert.IsFalse(loadout.Owns(rifle));

            Assert.AreEqual(1, loadout.NextLootIndex(), "the second is the sniper rifle");
            Assert.IsTrue(loadout.Grant(loadout.NextLootIndex()));
            Assert.IsTrue(loadout.Owns(rifle));

            Assert.AreEqual(-1, loadout.NextLootIndex(), "both guns are taken");
            Assert.AreEqual(3, loadout.OwnedCount);
        }

        [Test]
        public void Loot_FollowsTheSlotOrderWhateverTheControllersArrayHolds()
        {
            WeaponLoadout loadout = new WeaponLoadout(new[] { knife, smg, rifle }, false);

            Assert.AreEqual(1, loadout.NextLootIndex(), "the SMG, wherever it sits in the array");
            loadout.Grant(1);
            Assert.AreEqual(2, loadout.NextLootIndex());
        }

        [Test]
        public void Grant_IsFalseForAWeaponAlreadyOwnedOrAnInvalidIndex()
        {
            WeaponLoadout loadout = Loadout(false);

            Assert.IsFalse(loadout.Grant(2), "the knife is already owned");
            Assert.IsFalse(loadout.Grant(-1));
            Assert.IsFalse(loadout.Grant(3));
            Assert.IsTrue(loadout.Grant(0));
            Assert.IsFalse(loadout.Grant(0), "a gun is taken once");
        }

        [Test]
        public void Wheel_SkipsWeaponsThePlayerDoesNotHaveAndWrapsAround()
        {
            WeaponLoadout loadout = Loadout(false);

            Assert.AreEqual(-1, loadout.NextOwned(2, 1), "the knife alone: nowhere to switch to");

            loadout.Grant(0); // the SMG
            Assert.AreEqual(0, loadout.NextOwned(2, 1), "forward from the knife wraps to the SMG, past the missing sniper rifle");
            Assert.AreEqual(2, loadout.NextOwned(0, 1), "forward from the SMG skips the sniper rifle and finds the knife");
            Assert.AreEqual(2, loadout.NextOwned(0, -1), "backward wraps to the knife");

            loadout.Grant(1); // the sniper rifle
            Assert.AreEqual(1, loadout.NextOwned(0, 1));
        }

        [Test]
        public void UnknownWeapon_IsNotOwned()
        {
            WeaponLoadout loadout = Loadout(true);
            var stranger = Make("Stranger", 1, WeaponKind.Hitscan);
            try
            {
                Assert.IsFalse(loadout.Owns(stranger));
                Assert.IsFalse(loadout.Owns(null));
                Assert.IsFalse(loadout.IsOwned(99));
            }
            finally
            {
                Object.DestroyImmediate(stranger);
            }
        }

        [Test]
        public void ControllerBeforeAwake_OwnsEverythingUntilTheStormThenOnlyTheKnife()
        {
            var root = new GameObject("Inactive bootstrap player");
            root.SetActive(false);
            try
            {
                var controller = root.AddComponent<WeaponController>();

                Assert.IsTrue(controller.HasLoadout);
                Assert.IsTrue(controller.Owns(smg) && controller.Owns(knife));
                Assert.IsNull(controller.NextLootWeapon(), "nothing to loot before the weapons exist");

                controller.LoseStartingLoadout();

                Assert.IsFalse(controller.HasLoadout);
                Assert.IsFalse(controller.Owns(smg));
                Assert.IsFalse(controller.Owns(rifle));
                Assert.IsTrue(controller.Owns(knife), "the knife survives the storm");
                Assert.IsNull(controller.TakeNextLootWeapon());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
