// Owner: Systems Engineer.
using Blackout.Player;
using Blackout.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>
    /// Event-driven HUD text: the checkpoint label (top right), toasts (checkpoint reached, death, cure taken) and
    /// the SECURITY ALERT banner, which pulses for 7 s when the cure is taken and then stays as a small label.
    /// Strings are only built when an event happens, never per frame.
    /// </summary>
    public class HudMessages : MonoBehaviour
    {
        private const float ToastTime = 3.2f;
        private const float AlertPulseTime = 7f;
        private const float AlertMinAlpha = 0.55f;

        [SerializeField] private CheckpointTracker checkpoints;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private VoidEventChannel cureTaken;

        [Header("Checkpoint label")]
        [SerializeField] private GameObject checkpointRoot;
        [SerializeField] private Text checkpointName;

        [Header("Toast")]
        [SerializeField] private Text toast;

        [Header("Alert banner")]
        [Tooltip("The big pulsing banner with its subtitle.")]
        [SerializeField] private CanvasGroup alertBanner;
        [Tooltip("The small label that stays after the pulse.")]
        [SerializeField] private GameObject alertLabel;

        private float toastLeft;
        private float alertLeft;

        private void Awake()
        {
            SetActive(checkpointRoot, false);
            SetActive(alertLabel, false);
            if (alertBanner != null)
            {
                alertBanner.gameObject.SetActive(false);
            }

            if (toast != null)
            {
                toast.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (checkpoints != null)
            {
                checkpoints.CheckpointReached += OnCheckpointReached;
            }

            if (playerHealth != null)
            {
                playerHealth.Died += OnDied;
            }

            if (cureTaken != null)
            {
                cureTaken.OnRaised += OnCureTaken;
            }
        }

        private void OnDisable()
        {
            if (checkpoints != null)
            {
                checkpoints.CheckpointReached -= OnCheckpointReached;
            }

            if (playerHealth != null)
            {
                playerHealth.Died -= OnDied;
            }

            if (cureTaken != null)
            {
                cureTaken.OnRaised -= OnCureTaken;
            }
        }

        private void OnCheckpointReached(Marker marker)
        {
            string label = PlayerHealthRules.CheckpointName(marker.Id);
            if (checkpointName != null)
            {
                checkpointName.text = label;
            }

            SetActive(checkpointRoot, true);
            ShowToast("CHECKPOINT — " + label, HudColors.Text);
        }

        private void OnDied()
        {
            string where = checkpoints != null && checkpoints.Current != null
                ? PlayerHealthRules.CheckpointName(checkpoints.Current.Id)
                : "THE START";
            ShowToast("YOU DIED — RESPAWNING AT " + where, HudColors.Danger);
        }

        private void OnCureTaken()
        {
            ShowToast("CURE ACQUIRED", HudColors.Good);
            alertLeft = AlertPulseTime;
            SetActive(alertLabel, false);
            if (alertBanner != null)
            {
                alertBanner.gameObject.SetActive(true);
            }
        }

        public void ShowToast(string text, Color colour)
        {
            if (toast == null)
            {
                return;
            }

            toast.text = text;
            toast.color = colour;
            toast.enabled = true;
            toastLeft = ToastTime;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (toastLeft > 0f)
            {
                toastLeft -= dt;
                if (toastLeft <= 0f && toast != null)
                {
                    toast.enabled = false;
                }
            }

            if (alertLeft > 0f)
            {
                alertLeft -= dt;
                if (alertBanner != null)
                {
                    // One pulse per second down to 55 % opacity, as in the prototype's CSS animation.
                    float wave = 0.5f + 0.5f * Mathf.Cos(Time.time * 2f * Mathf.PI);
                    alertBanner.alpha = Mathf.Lerp(AlertMinAlpha, 1f, wave);
                }

                if (alertLeft <= 0f)
                {
                    if (alertBanner != null)
                    {
                        alertBanner.gameObject.SetActive(false);
                    }

                    SetActive(alertLabel, true);
                }
            }
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
