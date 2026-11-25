// Owner: Systems Engineer.
using Blackout.Player;
using Blackout.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace Blackout.Interaction
{
    /// <summary>Walk over a fallen enemy's kit to store it, including at full health. Q uses it later.</summary>
    public sealed class MedkitPickup : MonoBehaviour, ICheckpointSpawnedObject
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Light glow;
        private Vector3 visualRest;
        public UnityEvent onPickedUp = new UnityEvent();
        public bool IsTaken { get; private set; }

        private struct Checkpoint { public bool Taken, Active; }

        private void Awake()
        {
            if (visual != null) visualRest = visual.localPosition;
        }

        private void Update()
        {
            if (IsTaken || visual == null) return;
            visual.Rotate(0f, 68.8f * Time.deltaTime, 0f, Space.Self);
            visual.localPosition = visualRest + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.05f);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsTaken && other.CompareTag("Player"))
                TryCollect(other.GetComponentInParent<MedkitInventory>());
        }

        public bool TryCollect(MedkitInventory inventory)
        {
            if (IsTaken || inventory == null || !inventory.AddMedkit()) return false;
            IsTaken = true;
            onPickedUp.Invoke();
            // Keep the instance: a checkpoint may contain this uncollected drop.
            gameObject.SetActive(false);
            return true;
        }

        public object CaptureCheckpoint() => new Checkpoint { Taken = IsTaken, Active = gameObject.activeSelf };

        public void RestoreCheckpoint(object state)
        {
            var saved = (Checkpoint)state;
            IsTaken = saved.Taken;
            if (visual != null) visual.gameObject.SetActive(!IsTaken);
            if (glow != null) glow.enabled = !IsTaken;
            gameObject.SetActive(saved.Active);
        }

        public void DiscardForCheckpoint()
        {
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
