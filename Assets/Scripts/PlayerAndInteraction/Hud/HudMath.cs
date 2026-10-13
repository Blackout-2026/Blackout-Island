// Owner: Systems Engineer.
using Blackout.Player;
using UnityEngine;

namespace Blackout.Hud
{
    /// <summary>
    /// The HUD's numbers as pure functions (docs/reference/systems-engineer.md section 9): the trailing health
    /// ghost, the damage vignette and the damage-direction angle. The HUD components only apply the results.
    /// </summary>
    public static class HudMath
    {
        /// <summary>The health bar turns red below this.</summary>
        public const float LowHealth = 35f;
        /// <summary>Rate at which the red ghost bar catches up with the health bar.</summary>
        public const float GhostRate = 2.5f;
        /// <summary>A hit of this much damage gives a full-strength flash.</summary>
        public const float DamageForFullFlash = 25f;
        /// <summary>Flash strength lost per second.</summary>
        public const float FlashDecay = 1.4f;
        /// <summary>The low-health pulse starts below this.</summary>
        public const float PulseStartHealth = 40f;
        /// <summary>The vignette is never more opaque than this.</summary>
        public const float MaxVignette = 0.9f;

        /// <summary>The ghost bar eases down to the health, but never sits below it (healing jumps it up).</summary>
        public static float Ghost(float ghost, float health, float dt)
        {
            return Mathf.Max(health, PlayerMovementLogic.Damp(ghost, health, GhostRate, dt));
        }

        public static float AddDamageFlash(float flash, float damage)
        {
            return Mathf.Min(1f, flash + damage / DamageForFullFlash);
        }

        public static float DecayDamageFlash(float flash, float dt)
        {
            return Mathf.Max(0f, flash - FlashDecay * dt);
        }

        /// <summary>0 above 40 HP, rising to a pulse (0.75–1, 6 rad/s) at 0 HP. Always 1 while dead.</summary>
        public static float LowHealthPulse(float health, bool alive, float time)
        {
            if (!alive)
            {
                return 1f;
            }

            float low = Mathf.Clamp01((PulseStartHealth - health) / PulseStartHealth);
            return low * (0.75f + 0.25f * Mathf.Sin(time * 6f));
        }

        public static float VignetteAlpha(float flash, float pulse)
        {
            return Mathf.Min(MaxVignette, flash + pulse * 0.5f);
        }

        /// <summary>
        /// Where the attacker is, seen from above: 0 = straight ahead, +90 = right, −90 = left, ±180 = behind.
        /// Heights are ignored.
        /// </summary>
        public static float DamageAngle(Vector3 forward, Vector3 toAttacker)
        {
            forward.y = 0f;
            toAttacker.y = 0f;
            if (forward.sqrMagnitude < 1e-6f || toAttacker.sqrMagnitude < 1e-6f)
            {
                return 0f;
            }

            return Vector3.SignedAngle(forward, toAttacker, Vector3.up);
        }
    }
}
