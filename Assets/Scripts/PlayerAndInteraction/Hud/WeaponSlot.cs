// Owner: Systems Engineer.
using Blackout.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>One weapon panel (key, name, rounds). The active one is opaque, taller and underlined in the accent colour.</summary>
    public class WeaponSlot : MonoBehaviour
    {
        // Prototype: 58 px tall at 55 % opacity, 66 px when active.
        private const float InactiveHeight = 58f;
        private const float ActiveHeight = 66f;
        private const float InactiveAlpha = 0.55f;

        [SerializeField] private WeaponData weapon;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Graphic underline;
        [Tooltip("Rounds in the gun. Empty for the knife.")]
        [SerializeField] private Text ammoLoaded;
        [Tooltip("Rounds in the spare magazines, or RELOAD while the gun reloads.")]
        [SerializeField] private Text ammoReserve;

        private static readonly string[] Numbers = new string[1000];
        private static readonly string[] Reserves = new string[1000];

        public WeaponData Weapon => weapon;

        /// <summary>The rounds shown: the loaded ones, then the spare ones (RELOAD while reloading, NO AMMO when there is nothing left).</summary>
        public void SetAmmo(WeaponAmmo ammo)
        {
            bool limited = ammo != null && ammo.IsLimited;
            if (ammoLoaded != null)
            {
                ammoLoaded.gameObject.SetActive(limited);
                if (limited)
                {
                    ammoLoaded.text = Number(ammo.Loaded);
                }
            }

            if (ammoReserve != null)
            {
                ammoReserve.gameObject.SetActive(limited);
                if (limited)
                {
                    ammoReserve.text = ammo.Reloading ? "RELOAD" : ammo.Loaded == 0 && ammo.SpareMagazines == 0 ? "NO AMMO" : Reserve(ammo.ReserveRounds);
                }
            }
        }

        private static string Reserve(int value)
        {
            if (value < 0 || value >= Reserves.Length)
            {
                return "/ " + value;
            }

            return Reserves[value] ?? (Reserves[value] = "/ " + value);
        }

        // Cached, so showing the rounds after every shot makes no new string for the same number.
        private static string Number(int value)
        {
            if (value < 0 || value >= Numbers.Length)
            {
                return value.ToString();
            }

            return Numbers[value] ?? (Numbers[value] = value.ToString());
        }

        public void SetActive(bool active)
        {
            if (group != null)
            {
                group.alpha = active ? 1f : InactiveAlpha;
            }

            if (underline != null)
            {
                underline.enabled = active;
            }

            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, active ? ActiveHeight : InactiveHeight);
        }
    }
}
