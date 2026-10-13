// Owner: Systems Engineer.
using Blackout.Hud;
using Blackout.Player;
using UnityEngine;

namespace Blackout.Interaction
{
    /// <summary>
    /// Finds what the player is looking at (a 3 m ray from the camera), shows its prompt on the HUD, and uses it on
    /// E: a press, or a hold with the ring filling up. Walls block the ray, so nothing is used through a wall.
    /// Runs after PlayerLook so the ray uses this frame's view.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerHealth playerHealth;
        [Tooltip("The player camera: the ray starts here and follows its forward.")]
        [SerializeField] private Transform aim;
        [SerializeField] private InteractionPrompt prompt;
        [Tooltip("Metres (the playbook's 3 m).")]
        [SerializeField, Min(0.5f)] private float reach = 3f;
        [Tooltip("Layers the ray can hit: everything except the Player layer, so walls block it.")]
        [SerializeField] private LayerMask mask = ~0;

        private readonly HoldTimer hold = new HoldTimer();

        /// <summary>The interactable under the crosshair, or null.</summary>
        public Interactable Current { get; private set; }

        private void Awake()
        {
            if (input == null || aim == null || prompt == null)
            {
                Debug.LogError("[Blackout] PlayerInteractor needs its input, aim and prompt assigned.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            bool canAct = input.isActiveAndEnabled && (playerHealth == null || !playerHealth.IsDead);
            Interactable target = canAct ? FindTarget() : null;
            if (target != Current)
            {
                Current = target;
                hold.Reset();
            }

            if (Current == null)
            {
                prompt.Hide();
                return;
            }

            prompt.Show(Current.Key, Current.Prompt);
            bool usable = !string.IsNullOrEmpty(Current.Key);
            float holdTime = Current.HoldTime;
            if (holdTime > 0f)
            {
                bool held = usable && input.InteractHeld;
                bool done = hold.Update(held, holdTime, Time.deltaTime);
                float progress = hold.Fraction(holdTime);
                if (held && !done)
                {
                    Current.WhileHeld(progress, Time.deltaTime);
                }

                prompt.SetProgress(progress);
                if (done)
                {
                    Current.Interact(gameObject);
                }
            }
            else
            {
                prompt.SetProgress(-1f);
                if (usable && input.InteractPressed)
                {
                    Current.Interact(gameObject);
                }
            }
        }

        private Interactable FindTarget()
        {
            // Triggers are included so a trigger-only console can be targeted; the first hit wins.
            if (!Physics.Raycast(aim.position, aim.forward, out RaycastHit hit, reach, mask, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            var interactable = hit.collider.GetComponentInParent<Interactable>();
            return interactable != null && interactable.isActiveAndEnabled && interactable.CanInteract
                ? interactable
                : null;
        }
    }
}
