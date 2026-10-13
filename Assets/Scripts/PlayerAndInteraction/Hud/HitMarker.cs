// Owner: Systems Engineer.
using Blackout.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>The X at the centre when a shot or swing hits: white for 0.16 s, red for 0.35 s on a kill.</summary>
    public class HitMarker : MonoBehaviour
    {
        private const float HitTime = 0.16f;
        private const float KillTime = 0.35f;

        [SerializeField] private WeaponController weapons;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Graphic[] lines;

        private float timeLeft;

        private void OnEnable()
        {
            if (weapons != null)
            {
                weapons.HitConfirmed += OnHit;
            }

            SetVisible(false);
        }

        private void OnDisable()
        {
            if (weapons != null)
            {
                weapons.HitConfirmed -= OnHit;
            }
        }

        private void OnHit(bool killed)
        {
            timeLeft = killed ? KillTime : HitTime;
            Color colour = killed ? HudColors.Danger : Color.white;
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].color = colour;
            }

            SetVisible(true);
        }

        private void Update()
        {
            if (timeLeft <= 0f)
            {
                return;
            }

            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                SetVisible(false);
            }
        }

        private void SetVisible(bool visible)
        {
            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
            }
        }
    }
}
