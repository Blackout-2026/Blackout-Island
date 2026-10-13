// Owner: Systems Engineer.
using Blackout.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>
    /// The "[E] Open door" prompt below the crosshair. Interactables (Y5) call <see cref="Show"/> while targeted
    /// and <see cref="Hide"/> when not. It stays hidden while the player is dead.
    /// </summary>
    public class InteractionPrompt : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private GameObject root;
        [SerializeField] private Text key;
        [SerializeField] private Text label;

        private bool wanted;

        private void Awake()
        {
            Hide();
        }

        /// <summary>Shows the prompt. Assign the same strings every frame freely: Text ignores unchanged values.</summary>
        public void Show(string keyText, string labelText)
        {
            wanted = true;
            if (key != null)
            {
                key.text = keyText;
            }

            if (label != null)
            {
                label.text = labelText;
            }

            Refresh();
        }

        public void Hide()
        {
            wanted = false;
            Refresh();
        }

        private void Update()
        {
            Refresh(); // hides it while dead without the interactable having to know
        }

        private void Refresh()
        {
            bool visible = wanted && (playerHealth == null || !playerHealth.IsDead);
            if (root != null && root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }
    }
}
