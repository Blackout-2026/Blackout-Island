// Owner: Systems Engineer.
using System;
using Blackout.Hud;
using Blackout.Shared;
using Blackout.Weapons;
using UnityEngine;
using UnityEngine.Events;

namespace Blackout.Interaction
{
    /// <summary>
    /// The gun a dead enemy leaves behind (step L1): look at it and press E to take it. It gives the next gun the player is missing, whoever dropped it: the
    /// first pickup taken is the SMG, the second the R-700 sniper rifle (the order is WeaponLoadout's), and a pickup that finds nothing left to give
    /// hides itself. It shows the model of the gun it would give now, so the prompt and the picture always agree. It bobs and turns slowly until taken.
    /// </summary>
    public class WeaponDropPickup : Interactable, ICheckpointSpawnedObject
    {
        private const float SpinDegreesPerSecond = 40f;
        private const float BobHeight = 0.05f;
        private const float BobRate = 2f;

        /// <summary>One gun this pickup can show: its WeaponData and the model that is switched on while it is the next one.</summary>
        [Serializable]
        public struct Showcase
        {
            public WeaponData weapon;
            public GameObject model;
        }

        [Tooltip("The group that bobs and turns.")]
        [SerializeField] private Transform visual;
        [Tooltip("The guns in the order they can appear, each with its model.")]
        [SerializeField] private Showcase[] showcases;
        [Tooltip("Optional glow, switched off once there is nothing to take.")]
        [SerializeField] private Light glow;

        [Header("Audio hooks")]
        public UnityEvent onTaken = new UnityEvent();

        private IWeaponLoot loot;
        private string[] prompts;
        private int shown = -2;
        private Vector3 visualRest;

        public bool IsTaken { get; private set; }

        private struct Checkpoint
        {
            public bool Taken, Active;
        }

        public object CaptureCheckpoint() => new Checkpoint { Taken = IsTaken, Active = gameObject.activeSelf };

        public void RestoreCheckpoint(object state)
        {
            var saved = (Checkpoint)state;
            IsTaken = saved.Taken;
            gameObject.SetActive(saved.Active);
            shown = -2;
            Refresh();
        }

        public void DiscardForCheckpoint()
        {
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        /// <summary>The gun this pickup would give now, or null.</summary>
        public WeaponData Offer => Loot != null ? Loot.NextLootWeapon() : null;

        /// <summary>The player's weapon system, found once (the player is in another scene), or whatever a test injected.</summary>
        private IWeaponLoot Loot
        {
            get
            {
                if (loot == null || (loot is UnityEngine.Object o && o == null))
                {
                    loot = FindFirstObjectByType<WeaponController>();
                }

                return loot;
            }
        }

        public void SetLoot(IWeaponLoot provider)
        {
            loot = provider;
        }

        private void Awake()
        {
            if (visual != null)
            {
                visualRest = visual.localPosition;
            }

            EnsurePrompts();
        }

        // The prompt strings are built once, so showing the prompt every frame allocates nothing (the Interactable contract).
        private void EnsurePrompts()
        {
            if (prompts != null)
            {
                return;
            }

            prompts = new string[showcases != null ? showcases.Length : 0];
            for (int i = 0; i < prompts.Length; i++)
            {
                string weaponName = showcases[i].weapon != null ? showcases[i].weapon.displayName : "weapon";
                prompts[i] = "Take the " + weaponName;
            }
        }

        private void Update()
        {
            if (IsTaken)
            {
                return;
            }

            Refresh();
            if (visual != null && shown >= 0)
            {
                visual.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
                visual.localPosition = visualRest + Vector3.up * (Mathf.Sin(Time.time * BobRate) * BobHeight);
            }
        }

        /// <summary>Switches on the model of the gun on offer (none when the player has them all, which also puts the glow out).</summary>
        public void Refresh()
        {
            int now = IndexOf(Offer);
            if (now == shown)
            {
                return;
            }

            shown = now;
            for (int i = 0; showcases != null && i < showcases.Length; i++)
            {
                if (showcases[i].model != null)
                {
                    showcases[i].model.SetActive(i == shown);
                }
            }

            if (glow != null)
            {
                glow.enabled = shown >= 0;
            }
        }

        private int IndexOf(WeaponData data)
        {
            for (int i = 0; data != null && showcases != null && i < showcases.Length; i++)
            {
                if (showcases[i].weapon == data)
                {
                    return i;
                }
            }

            return -1;
        }

        public override string Prompt
        {
            get
            {
                EnsurePrompts();
                int index = IndexOf(Offer);
                return index >= 0 && index < prompts.Length ? prompts[index] : "Nothing left to take";
            }
        }

        public override bool CanInteract => !IsTaken && Offer != null;

        public override void Interact(GameObject by)
        {
            if (IsTaken)
            {
                return;
            }

            IWeaponLoot taker = by != null ? by.GetComponentInChildren<IWeaponLoot>(true) : null;
            WeaponData given = (taker ?? Loot)?.TakeNextLootWeapon();
            if (given == null)
            {
                return;
            }

            IsTaken = true;
            var messages = FindFirstObjectByType<HudMessages>();
            if (messages != null)
            {
                messages.ShowToast(given.displayName.ToUpperInvariant() + " ACQUIRED", HudColors.Good);
            }

            onTaken.Invoke();
            // A pickup present at the checkpoint must still exist for restoration after being collected.
            gameObject.SetActive(false);
        }
    }
}
