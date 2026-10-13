// Owner: Systems Engineer.
using Blackout.Weapons;
using UnityEngine;

namespace Blackout.Hud
{
    /// <summary>Bottom-right weapon slots 1/2/3: highlights the slot of the weapon the WeaponController holds.</summary>
    public class WeaponSlots : MonoBehaviour
    {
        [SerializeField] private WeaponController weapons;
        [SerializeField] private WeaponSlot[] slots;

        private void OnEnable()
        {
            if (weapons != null)
            {
                weapons.WeaponChanged += Highlight;
                Highlight(weapons.ActiveWeapon);
            }
        }

        private void OnDisable()
        {
            if (weapons != null)
            {
                weapons.WeaponChanged -= Highlight;
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
                    slots[i].SetActive(active != null && slots[i].Weapon == active);
                }
            }
        }
    }
}
