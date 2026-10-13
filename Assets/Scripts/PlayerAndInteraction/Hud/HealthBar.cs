// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>
    /// Bottom-left health bar: the fill shows health, a red ghost bar trails it down after a hit, and the fill
    /// turns red below 35. The number is only rebuilt when it changes, so no string is made per frame.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Health health;
        [Tooltip("Stretched from the left by setting its right anchor to the health fraction.")]
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform ghost;
        [SerializeField] private Text value;

        private Image fillImage;
        private float ghostHealth;
        private int shownValue = -1;

        private void Awake()
        {
            if (health == null || fill == null || ghost == null || value == null)
            {
                Debug.LogError("[Blackout] HealthBar needs its health, fill, ghost and value assigned.", this);
                enabled = false;
                return;
            }

            fillImage = fill.GetComponent<Image>();
            ghostHealth = health.Max;
        }

        private void Update()
        {
            float current = health.Current;
            ghostHealth = HudMath.Ghost(ghostHealth, current, Time.deltaTime);

            SetWidth(fill, current / health.Max);
            SetWidth(ghost, ghostHealth / health.Max);

            int rounded = Mathf.CeilToInt(current);
            if (rounded != shownValue)
            {
                shownValue = rounded;
                value.text = rounded.ToString();
                if (fillImage != null)
                {
                    fillImage.color = current < HudMath.LowHealth ? HudColors.Danger : HudColors.HealthFill;
                }
            }
        }

        private static void SetWidth(RectTransform bar, float fraction)
        {
            Vector2 anchorMax = bar.anchorMax;
            anchorMax.x = Mathf.Clamp01(fraction);
            bar.anchorMax = anchorMax;
        }
    }
}
