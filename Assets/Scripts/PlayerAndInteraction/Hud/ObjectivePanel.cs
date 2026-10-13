// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>
    /// Top-left objective text from the ObjectiveChanged channel (the MissionDirector raises it in Y6).
    /// Hidden while empty; a new objective flashes in the accent colour and fades to white over 1.2 s.
    /// </summary>
    public class ObjectivePanel : MonoBehaviour
    {
        private const float FlashTime = 1.2f;

        [SerializeField] private StringEventChannel objectiveChanged;
        [SerializeField] private Text text;
        [Tooltip("Hidden while there is no objective.")]
        [SerializeField] private GameObject root;

        private float flashLeft;

        private void Awake()
        {
            SetObjective(string.Empty);
        }

        private void OnEnable()
        {
            if (objectiveChanged != null)
            {
                objectiveChanged.OnRaised += SetObjective;
            }
        }

        private void OnDisable()
        {
            if (objectiveChanged != null)
            {
                objectiveChanged.OnRaised -= SetObjective;
            }
        }

        public void SetObjective(string objective)
        {
            if (text == null)
            {
                return;
            }

            text.text = objective;
            if (root != null)
            {
                root.SetActive(!string.IsNullOrEmpty(objective));
            }

            flashLeft = FlashTime;
        }

        private void Update()
        {
            if (flashLeft <= 0f || text == null)
            {
                return;
            }

            flashLeft = Mathf.Max(0f, flashLeft - Time.deltaTime);
            text.color = Color.Lerp(HudColors.Text, HudColors.Accent, flashLeft / FlashTime);
        }
    }
}
