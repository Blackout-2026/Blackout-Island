// Owner: Systems Engineer.
using Blackout.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>
    /// The "[E] Open door" prompt below the crosshair. The PlayerInteractor (Y5) calls <see cref="Show"/> while an
    /// interactable is targeted and <see cref="Hide"/> when not, and <see cref="SetProgress"/> for hold-E actions,
    /// which fills the ring next to the text. It stays hidden while the player is dead.
    /// </summary>
    public class InteractionPrompt : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private GameObject root;
        [SerializeField] private Text key;
        [SerializeField] private Text label;
        [Tooltip("Radial-filled Image shown while an action is held (the generator's 2 s hold).")]
        [SerializeField] private Image holdRing;

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
            SetProgress(-1f);
            Refresh();
        }

        /// <summary>Fills the hold ring (0..1). A negative value hides it, for press-only actions.</summary>
        public void SetProgress(float fraction)
        {
            if (holdRing == null)
            {
                return;
            }

            bool show = fraction >= 0f;
            if (holdRing.enabled != show)
            {
                holdRing.enabled = show;
            }

            if (show)
            {
                holdRing.fillAmount = Mathf.Clamp01(fraction);
            }
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
