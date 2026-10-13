// Owner: Systems Engineer.
using Blackout.Player;
using Blackout.Weapons;
using UnityEngine;

namespace Blackout.Hud
{
    /// <summary>
    /// The crosshair changes per weapon, as in the prototype: four lines and a dot for a gun with spread (SMG),
    /// only the dot for an accurate gun (rifle), a ring and a dot for melee (knife). Hidden while dead.
    /// </summary>
    public class Crosshair : MonoBehaviour
    {
        [SerializeField] private WeaponController weapons;
        [SerializeField] private PlayerHealth playerHealth;
        [Tooltip("Parent of everything below, hidden while dead.")]
        [SerializeField] private GameObject root;
        [SerializeField] private GameObject lines;
        [SerializeField] private GameObject ring;

        private void OnEnable()
        {
            if (weapons != null)
            {
                weapons.WeaponChanged += OnWeaponChanged;
                OnWeaponChanged(weapons.ActiveWeapon);
            }

            if (playerHealth != null)
            {
                playerHealth.Died += Hide;
                playerHealth.Respawned += Show;
            }
        }

        private void OnDisable()
        {
            if (weapons != null)
            {
                weapons.WeaponChanged -= OnWeaponChanged;
            }

            if (playerHealth != null)
            {
                playerHealth.Died -= Hide;
                playerHealth.Respawned -= Show;
            }
        }

        private void OnWeaponChanged(WeaponData weapon)
        {
            bool melee = weapon != null && weapon.kind == WeaponKind.Melee;
            bool spread = weapon != null && !melee && weapon.spread > 0f;
            SetActive(lines, spread);
            SetActive(ring, melee);
        }

        private void Hide()
        {
            SetActive(root, false);
        }

        private void Show()
        {
            SetActive(root, true);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
