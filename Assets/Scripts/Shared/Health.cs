// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using System;
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>
    /// Hit points for the player and the agents, ported from the prototype's player.js:
    /// damage is clamped at 0, ignored while invulnerable or dead, and the invulnerability timer counts down
    /// by delta time (respawn uses 1.5 s). Death is raised once through <see cref="OnDied"/>.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f; // prototype player: 100 HP

        private float current;
        private float invulnerableTime;

        /// <summary>Raised after damage is applied (not when it is blocked), before <see cref="OnDied"/>.</summary>
        public event Action<DamageInfo> OnDamaged;
        /// <summary>Raised once, when health first reaches 0.</summary>
        public event Action OnDied;

        public float Max => maxHealth;
        public float Current => current;
        public bool IsAlive => current > 0f;
        public bool IsInvulnerable => invulnerableTime > 0f;

        private void Awake()
        {
            ResetHealth(0f);
        }

        private void Update()
        {
            if (invulnerableTime > 0f)
            {
                Tick(Time.deltaTime);
            }
        }

        /// <summary>Counts the invulnerability timer down. Called by Update; public so tests can drive time.</summary>
        public void Tick(float deltaTime)
        {
            invulnerableTime = Mathf.Max(0f, invulnerableTime - deltaTime);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || IsInvulnerable || info.amount <= 0f)
            {
                return;
            }

            current = Mathf.Max(0f, current - info.amount);
            OnDamaged?.Invoke(info);

            if (current <= 0f)
            {
                OnDied?.Invoke();
            }
        }

        /// <summary>Adds health up to the maximum and returns how much was actually gained. The dead cannot heal.</summary>
        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return 0f;
            }

            float before = current;
            current = Mathf.Min(maxHealth, current + amount);
            return current - before;
        }

        /// <summary>Full health (respawn or spawn), optionally followed by a period of invulnerability.</summary>
        public void ResetHealth(float invulnerableSeconds)
        {
            current = maxHealth;
            invulnerableTime = Mathf.Max(0f, invulnerableSeconds);
        }

        /// <summary>Makes this invulnerable for at least the given time (never shortens a longer timer).</summary>
        public void SetInvulnerable(float seconds)
        {
            invulnerableTime = Mathf.Max(invulnerableTime, seconds);
        }
    }
}
