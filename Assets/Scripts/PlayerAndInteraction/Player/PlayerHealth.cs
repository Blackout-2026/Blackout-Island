// Owner: Systems Engineer.
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// The player's life cycle on top of the shared <see cref="Health"/> (100 HP, no regeneration): fall damage,
    /// the kill height, death, and the respawn at the last checkpoint after 2.2 s with 1.5 s of invulnerability.
    /// Publishes PlayerDied and PlayerRespawned. Nothing else is reset, so generator and cure state are kept.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private CheckpointTracker checkpoints;
        [Tooltip("Hidden while dead so the viewmodel does not float in the death camera.")]
        [SerializeField] private GameObject weaponHolder;
        [SerializeField] private VoidEventChannel playerDied;
        [SerializeField] private VoidEventChannel playerRespawned;

        [Header("Prototype numbers (docs/reference/systems-engineer.md section 8)")]
        [SerializeField, Min(0f)] private float respawnDelay = 2.2f;
        [SerializeField, Min(0f)] private float invulnerableAfterRespawn = 1.5f;
        [Tooltip("Landings slower than this (m/s) do no damage.")]
        [SerializeField, Min(0f)] private float safeFallSpeed = 13f;
        [SerializeField, Min(0f)] private float fallDamagePerSpeed = 8f;
        [Tooltip("Falling below this height kills the player.")]
        [SerializeField] private float killHeight = -20f;

        private Health health;
        private DeathSequence death;

        public event Action Died;
        public event Action Respawned;

        public Health Health => health;
        public bool IsDead => death != null && death.IsDead;
        /// <summary>0 while alive, 0 → 1 while the death camera falls over.</summary>
        public float DeathProgress => death != null ? death.FallProgress : 0f;

        private void Awake()
        {
            health = GetComponent<Health>();
            death = new DeathSequence(respawnDelay);
            if (input == null || motor == null || look == null)
            {
                Debug.LogError("[Blackout] PlayerHealth needs its input, motor and look assigned.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            health.OnDied += OnDied;
            if (motor != null)
            {
                motor.Landed += OnLanded;
            }
        }

        private void OnDisable()
        {
            health.OnDied -= OnDied;
            if (motor != null)
            {
                motor.Landed -= OnLanded;
            }
        }

        private void Update()
        {
            if (death.IsDead)
            {
                if (death.Tick(Time.deltaTime))
                {
                    Respawn();
                }

                return;
            }

            if (transform.position.y < killHeight && health.IsAlive)
            {
                // Fell out of the world. Blocked while invulnerable, which only lasts 1.5 s after a respawn.
                health.TakeDamage(new DamageInfo(health.Current, transform.position, Vector3.down, null, DamageType.Fall));
            }
        }

        private void OnLanded(float landingSpeed)
        {
            float damage = PlayerHealthRules.FallDamage(landingSpeed, safeFallSpeed, fallDamagePerSpeed);
            if (damage > 0f)
            {
                health.TakeDamage(new DamageInfo(damage, transform.position, Vector3.down, null, DamageType.Fall));
            }
        }

        private void OnDied()
        {
            death.Begin();
            SetControlsActive(false);
            if (playerDied != null)
            {
                playerDied.Raise();
            }

            Died?.Invoke();
        }

        private void Respawn()
        {
            Vector3 position = transform.position;
            float yaw = transform.eulerAngles.y;
            if (checkpoints != null)
            {
                checkpoints.GetRespawnPose(out position, out yaw);
            }

            motor.Teleport(position, yaw);
            look.ResetPitch();
            health.ResetHealth(invulnerableAfterRespawn);
            SetControlsActive(true);
            if (playerRespawned != null)
            {
                playerRespawned.Raise();
            }

            Respawned?.Invoke();
        }

        /// <summary>
        /// Turning the input reader off stops movement, look and weapons at once: they all treat a disabled reader
        /// as "no input". The cursor stays locked.
        /// </summary>
        private void SetControlsActive(bool active)
        {
            input.enabled = active;
            if (weaponHolder != null)
            {
                weaponHolder.SetActive(active);
            }
        }
    }
}
