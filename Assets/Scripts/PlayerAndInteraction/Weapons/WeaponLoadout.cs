// Owner: Systems Engineer.
namespace Blackout.Weapons
{
    /// <summary>
    /// Which of the player's weapons are owned right now (the loot progression of step L1). The controller keeps all weapons in slot order; this
    /// remembers which of them the player holds: everything at the start of a run, only the knife once the storm has taken the guns, and then
    /// the SMG (slot 1), the R-700 sniper rifle (slot 2) as the player takes them from dead enemies, in that order. Pure C#, so the rules are
    /// tested without a scene; WeaponController shows and hides the models.
    /// </summary>
    public sealed class WeaponLoadout
    {
        private readonly WeaponData[] weapons;
        private readonly bool[] owned;

        /// <param name="weaponsInSlotOrder">The weapons in the controller's order (ascending slot).</param>
        /// <param name="ownAll">True for a normal start; false for the storm's aftermath (the knife only).</param>
        public WeaponLoadout(WeaponData[] weaponsInSlotOrder, bool ownAll)
        {
            weapons = weaponsInSlotOrder;
            owned = new bool[weapons.Length];
            for (int i = 0; i < owned.Length; i++)
            {
                owned[i] = ownAll || IsMelee(i);
            }
        }

        public int Count => weapons.Length;

        public bool[] CaptureOwnership() => (bool[])owned.Clone();

        public void RestoreOwnership(bool[] saved)
        {
            System.Array.Copy(saved, owned, owned.Length);
        }

        public int OwnedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < owned.Length; i++)
                {
                    n += owned[i] ? 1 : 0;
                }

                return n;
            }
        }

        public bool IsOwned(int index)
        {
            return index >= 0 && index < owned.Length && owned[index];
        }

        public bool Owns(WeaponData data)
        {
            return IsOwned(IndexOf(data));
        }

        public int IndexOf(WeaponData data)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == data)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Takes a weapon. False if it is not a valid index or was already owned.</summary>
        public bool Grant(int index)
        {
            if (index < 0 || index >= owned.Length || owned[index])
            {
                return false;
            }

            owned[index] = true;
            return true;
        }

        /// <summary>The storm: every gun is lost and the knife stays (a knife was never part of the loss).</summary>
        public void KeepOnlyMelee()
        {
            for (int i = 0; i < owned.Length; i++)
            {
                owned[i] = IsMelee(i);
            }
        }

        /// <summary>The first owned weapon in slot order, or -1 when nothing is owned.</summary>
        public int FirstOwned()
        {
            for (int i = 0; i < owned.Length; i++)
            {
                if (owned[i])
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// The owned weapon <paramref name="direction"/> steps away from <paramref name="from"/>, wrapping around and skipping what is not owned
        /// (the mouse wheel). -1 when there is no other owned weapon.
        /// </summary>
        public int NextOwned(int from, int direction)
        {
            int count = owned.Length;
            for (int step = 1; step < count; step++)
            {
                int index = (((from + direction * step) % count) + count) % count;
                if (owned[index])
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// The weapon the next enemy gun gives: the first gun (not the knife) the player does not have yet, in slot order, so the SMG first and then
        /// the sniper rifle. -1 once every gun is owned.
        /// </summary>
        public int NextLootIndex()
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (!owned[i] && weapons[i] != null && weapons[i].kind == WeaponKind.Hitscan)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsMelee(int index)
        {
            return weapons[index] != null && weapons[index].kind == WeaponKind.Melee;
        }
    }

    /// <summary>What a weapon pickup needs from the player's weapon system (implemented by WeaponController, faked in tests).</summary>
    public interface IWeaponLoot
    {
        /// <summary>The gun the next pickup would give, or null when the player has them all.</summary>
        WeaponData NextLootWeapon();

        /// <summary>Gives that gun to the player and returns it (or null if there is none left).</summary>
        WeaponData TakeNextLootWeapon();
    }
}
