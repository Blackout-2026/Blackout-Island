// Owner: Systems Engineer.
using Blackout.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>One weapon panel (key, name). The active one is opaque, taller and underlined in the accent colour.</summary>
    public class WeaponSlot : MonoBehaviour
    {
        // Prototype: 58 px tall at 55 % opacity, 66 px when active.
        private const float InactiveHeight = 58f;
        private const float ActiveHeight = 66f;
        private const float InactiveAlpha = 0.55f;

        [SerializeField] private WeaponData weapon;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Graphic underline;

        public WeaponData Weapon => weapon;

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
