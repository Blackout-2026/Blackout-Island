// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace Blackout.Interaction
{
    /// <summary>
    /// A medkit: walking over it heals 35 HP, but only when hurt, so a kit is never wasted at full health
    /// (prototype). It spins and bobs until taken. Uses OnTriggerStay so a player who gets hurt while standing on
    /// it still picks it up.
    /// </summary>
    public class HealthPickup : MonoBehaviour
    {
        // Prototype idle motion: 1.2 rad/s spin, ±0.05 m bob at 2 rad/s.
        private const float SpinDegreesPerSecond = 68.8f;
        private const float BobHeight = 0.05f;
        private const float BobRate = 2f;
        private const string PlayerTag = "Player";

        [SerializeField, Min(0f)] private float healAmount = 35f;
        [Tooltip("The kit model: spins and bobs, hidden once taken.")]
        [SerializeField] private Transform visual;
        [Tooltip("Optional glow, switched off once taken.")]
        [SerializeField] private Light glow;

        [Header("Audio hooks")]
        public UnityEvent onPickedUp = new UnityEvent();

        private Vector3 visualRest;

        public bool IsTaken { get; private set; }

        private void Awake()
        {
            if (visual != null)
            {
                visualRest = visual.localPosition;
            }
        }

        private void Update()
        {
            if (IsTaken || visual == null)
            {
                return;
            }

            visual.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
            visual.localPosition = visualRest + Vector3.up * (Mathf.Sin(Time.time * BobRate) * BobHeight);
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsTaken || !other.CompareTag(PlayerTag))
            {
                return;
            }

            var health = other.GetComponentInParent<Health>();
            if (health != null)
            {
                TryHeal(health);
            }
        }

        /// <summary>Heals if hurt and returns the amount gained; 0 (and the kit stays) at full health.</summary>
        public float TryHeal(Health health)
        {
            if (IsTaken || !health.IsAlive || health.Current >= health.Max)
            {
                return 0f;
            }

            float gained = health.Heal(healAmount);
            IsTaken = true;
            if (visual != null)
            {
                visual.gameObject.SetActive(false);
            }

            if (glow != null)
            {
                glow.enabled = false;
            }

            onPickedUp.Invoke();
            return gained;
        }
    }
}
