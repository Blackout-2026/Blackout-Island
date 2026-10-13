// Owner: Systems Engineer.
using Blackout.Player;
using Blackout.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Hud
{
    /// <summary>
    /// Damage feedback: a red screen-edge vignette that flashes on every hit and pulses at low health, and an arc
    /// around the centre pointing at the attacker that fades over 1.1 s. Arcs come from a small fixed pool.
    /// </summary>
    public class DamageIndicator : MonoBehaviour
    {
        private const float ArcTime = 1.1f;

        [SerializeField] private Health health;
        [SerializeField] private PlayerHealth playerHealth;
        [Tooltip("The player's body: arcs are drawn relative to where it faces.")]
        [SerializeField] private Transform player;
        [SerializeField] private RawImage vignette;
        [Tooltip("Full-screen-centred arc images pointing up; each is rotated towards an attacker.")]
        [SerializeField] private RawImage[] arcs;

        private float[] arcTimeLeft;
        private int nextArc;
        private float flash;

        private void Awake()
        {
            if (health == null || player == null || vignette == null)
            {
                Debug.LogError("[Blackout] DamageIndicator needs its health, player and vignette assigned.", this);
                enabled = false;
                return;
            }

            arcs ??= new RawImage[0];
            arcTimeLeft = new float[arcs.Length];
            for (int i = 0; i < arcs.Length; i++)
            {
                SetAlpha(arcs[i], 0f);
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDamaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDamaged -= OnDamaged;
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            flash = HudMath.AddDamageFlash(flash, info.amount);
            if (info.type == DamageType.Fall || arcs.Length == 0)
            {
                return; // falls have no attacker to point at
            }

            Vector3 toAttacker = info.source != null
                ? info.source.transform.position - player.position
                : -info.direction;
            float angle = HudMath.DamageAngle(player.forward, toAttacker);

            RawImage arc = arcs[nextArc];
            arcTimeLeft[nextArc] = ArcTime;
            nextArc = (nextArc + 1) % arcs.Length;
            // UI rotation is anticlockwise, so an attacker to the right (+90) turns the arc by −90.
            arc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            SetAlpha(arc, 1f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            flash = HudMath.DecayDamageFlash(flash, dt);
            bool alive = playerHealth == null || !playerHealth.IsDead;
            float pulse = HudMath.LowHealthPulse(health.Current, alive, Time.time);
            SetAlpha(vignette, HudMath.VignetteAlpha(flash, pulse));

            for (int i = 0; i < arcs.Length; i++)
            {
                if (arcTimeLeft[i] <= 0f)
                {
                    continue;
                }

                arcTimeLeft[i] -= dt;
                SetAlpha(arcs[i], Mathf.Max(0f, arcTimeLeft[i] / ArcTime));
            }
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color colour = graphic.color;
            colour.a = alpha;
            graphic.color = colour;
        }
    }
}
