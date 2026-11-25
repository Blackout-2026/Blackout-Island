// Owner: Systems Engineer.
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>Stored enemy medkits. Q spends one only if the living player actually gains health.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class MedkitInventory : MonoBehaviour, ICheckpointState
    {
        public const float HealAmount = 25f;

        [SerializeField] private PlayerInputReader input;
        private Health health;

        public int Count { get; private set; }
        public event Action<int> CountChanged;

        private Health PlayerHealth => health != null ? health : health = GetComponent<Health>();

        private void Update()
        {
            if (Time.timeScale > 0f && input != null && input.isActiveAndEnabled && input.UseMedkitPressed)
                TryUse();
        }

        public bool AddMedkit()
        {
            if (!PlayerHealth.IsAlive) return false;
            Count++;
            CountChanged?.Invoke(Count);
            return true;
        }

        /// <summary>Returns actual healing (up to 25). No kit is spent at full health or while dead.</summary>
        public float TryUse()
        {
            if (Count <= 0) return 0f;
            float healed = PlayerHealth.Heal(HealAmount);
            if (healed <= 0f) return 0f;
            Count--;
            CountChanged?.Invoke(Count);
            return healed;
        }

        public object CaptureCheckpoint() => Count;

        public void RestoreCheckpoint(object state)
        {
            Count = (int)state;
            CountChanged?.Invoke(Count);
        }
    }
}
