// Owner: Systems Engineer.
using Blackout.Hud;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using UnityEngine;
using UnityEngine.Events;

namespace Blackout.Interaction
{
    /// <summary>
    /// The ammunition on a dead body (step F2): walk over it and the player takes the body's magazines, 2 for the SMG and 1 for the sniper rifle (the
    /// numbers are <c>WeaponData.magazinesPerBody</c>). EnemyWeaponDrop leaves one beside every body, so each body can be taken once. It works whether
    /// or not the player has the gun yet; the magazines wait in his pack.
    /// </summary>
    public sealed class AmmoPickup : MonoBehaviour, ICheckpointSpawnedObject
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Light glow;
        private Vector3 visualRest;
        public UnityEvent onPickedUp = new UnityEvent();

        public bool IsTaken { get; private set; }

        private struct Checkpoint
        {
            public bool Taken, Active;
        }

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

            visual.Rotate(0f, 60f * Time.deltaTime, 0f, Space.Self);
            visual.localPosition = visualRest + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.04f);
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsTaken || !other.CompareTag("Player"))
            {
                return;
            }

            PlayerMotor motor = other.GetComponentInParent<PlayerMotor>();
            TryCollect(motor != null ? motor.GetComponentInChildren<WeaponController>(true) : null);
        }

        /// <summary>Gives the player the body's magazines. False if it was already taken or there is nothing to give.</summary>
        public bool TryCollect(WeaponController weapons)
        {
            if (IsTaken || weapons == null || weapons.AddMagazinesFromBody() <= 0)
            {
                return false;
            }

            IsTaken = true;
            onPickedUp.Invoke();
            var messages = FindFirstObjectByType<HudMessages>();
            if (messages != null)
            {
                messages.ShowToast("AMMUNITION COLLECTED", HudColors.Good);
            }

            // Keep the instance: a checkpoint may contain this uncollected drop.
            gameObject.SetActive(false);
            return true;
        }

        public object CaptureCheckpoint() => new Checkpoint { Taken = IsTaken, Active = gameObject.activeSelf };

        public void RestoreCheckpoint(object state)
        {
            var saved = (Checkpoint)state;
            IsTaken = saved.Taken;
            if (visual != null)
            {
                visual.gameObject.SetActive(!IsTaken);
            }

            if (glow != null)
            {
                glow.enabled = !IsTaken;
            }

            gameObject.SetActive(saved.Active);
        }

        public void DiscardForCheckpoint()
        {
            gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
