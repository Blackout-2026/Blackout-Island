// Owner: Systems Engineer.
using Blackout.Weapons;
using UnityEngine;

namespace Blackout.Hud
{
    /// <summary>
    /// Bottom-right weapon slots 1/2/3: highlights the slot of the weapon the WeaponController holds, and shows only the slots of the weapons the player
    /// owns (after the storm that is the knife alone, then the SMG and the sniper rifle appear as they are taken from dead enemies, step L1).
    /// </summary>
    public class WeaponSlots : MonoBehaviour
    {
        [SerializeField] private WeaponController weapons;
        [SerializeField] private WeaponSlot[] slots;

        private void OnEnable()
        {
            if (weapons != null)
            {
                weapons.WeaponChanged += Highlight;
                weapons.LoadoutChanged += Refresh;
                weapons.AmmoChanged += ShowAmmo;
                Highlight(weapons.ActiveWeapon);
                ShowAmmo();
            }
        }

        private void OnDisable()
        {
            if (weapons != null)
            {
                weapons.WeaponChanged -= Highlight;
                weapons.LoadoutChanged -= Refresh;
                weapons.AmmoChanged -= ShowAmmo;
            }
        }

        private void Refresh()
        {
            Highlight(weapons.ActiveWeapon);
        }

        /// <summary>Puts the rounds of every gun on its slot (the loaded rounds and the spare ones, step F2).</summary>
        private void ShowAmmo()
        {
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].SetAmmo(weapons != null ? weapons.AmmoOf(slots[i].Weapon) : null);
                }
            }
        }

        private void Highlight(WeaponData active)
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].gameObject.SetActive(weapons == null || slots[i].Weapon == null || weapons.Owns(slots[i].Weapon));
                    slots[i].SetActive(active != null && slots[i].Weapon == active);
                }
            }
        }
    }
}
