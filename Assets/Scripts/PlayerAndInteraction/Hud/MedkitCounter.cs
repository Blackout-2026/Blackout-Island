// Owner: Systems Engineer.
using Blackout.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>The side HUD reads the inventory; it never spends or grants kits.</summary>
    public sealed class MedkitCounter : MonoBehaviour
    {
        [SerializeField] private MedkitInventory inventory;
        [SerializeField] private Text value;

        private void OnEnable()
        {
            if (inventory == null) return;
            inventory.CountChanged += Refresh;
            Refresh(inventory.Count);
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.CountChanged -= Refresh;
        }

        private void Refresh(int count)
        {
            if (value != null) value.text = count.ToString();
        }
    }
}
