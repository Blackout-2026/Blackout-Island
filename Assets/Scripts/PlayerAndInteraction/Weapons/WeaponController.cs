// Owner: Systems Engineer.
using System;
using Blackout.Player;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// The player's one weapon system, on the WeaponHolder under the camera. It finds the IWeapon children,
    /// handles switching (1/2/3 and the wheel), the shared cooldown, the fire button and recoil, and animates the
    /// holder (switch dip and viewmodel kick). What a shot or swing actually does is left to the weapon.
    /// Runs after PlayerLook so shots use this frame's aim.
    /// </summary>
    public class WeaponController : MonoBehaviour, IWeaponLoot, ICheckpointState
    {
        // Viewmodel springs (docs/reference/systems-engineer.md section 7): a shot adds kick × 30 to the
        // spring's velocity, and the offset is applied at 60 %.
        private const float KickVelocityScale = 30f;
        private const float KickApplied = 0.6f;
        private const float BackStiffness = 180f;
        private const float BackDamping = 18f;
        private const float RotateStiffness = 160f;
        private const float RotateDamping = 16f;
        // Fully lowered pose while switching: 0.3 m down, muzzle pitched 40° down.
        private const float SwitchDrop = 0.3f;
        private const float SwitchPitch = 40f;

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [Tooltip("The player camera: shots start here and follow its forward.")]
        [SerializeField] private Transform aim;
        [Tooltip("Noise.asset in Assets/ScriptableObjects/Events. Leave empty to make no noise.")]
        [SerializeField] private NoiseEventChannel noiseChannel;
        [SerializeField] private TracerPool tracers;
        [SerializeField] private ImpactEffects impacts;
        [Tooltip("Layers bullets and the knife can hit: PhysicsLayers.PlayerShots (docs/reference/systems-engineer.md " +
                 "section 12). Leave the Player layer out.")]
        [SerializeField] private LayerMask hitMask = PhysicsLayers.PlayerShots;
        [Tooltip("Index of the weapon held at the start, in slot order.")]
        [SerializeField, Min(0)] private int startIndex;

        private IWeapon[] weapons;
        private WeaponSwitcher switcher;
        private readonly WeaponCooldown cooldown = new WeaponCooldown();
        private readonly RecoilSpring kickBack = new RecoilSpring(BackStiffness, BackDamping);
        private readonly RecoilSpring kickRotate = new RecoilSpring(RotateStiffness, RotateDamping);
        private WeaponContext context;
        private Vector3 restPosition;
        private Quaternion restRotation;
        // Gameplay starts after the loss of the guns, even when the cinematic is skipped.
        private bool loadoutLost = true;
        private WeaponLoadout loadout;
        private WeaponAmmo[] ammo;

        private sealed class Checkpoint
        {
            public bool LoadoutLost;
            public bool[] Owned;
            public WeaponSwitcher Switcher;
            public float Cooldown;
            public WeaponAmmo.State[] Ammo;
        }

        public object CaptureCheckpoint() => switcher == null ? null : new Checkpoint
        {
            LoadoutLost = loadoutLost, Owned = loadout.CaptureOwnership(),
            Switcher = switcher.CaptureCheckpoint(), Cooldown = cooldown.Remaining, Ammo = CaptureAmmo()
        };

        public void RestoreCheckpoint(object state)
        {
            if (!(state is Checkpoint saved)) return;
            loadoutLost = saved.LoadoutLost;
            loadout.RestoreOwnership(saved.Owned);
            switcher = saved.Switcher.CaptureCheckpoint(); // never mutate the stored snapshot
            cooldown.Start(saved.Cooldown);
            RestoreAmmo(saved.Ammo);
            kickBack.Reset();
            kickRotate.Reset();
            ShowOnly(loadout.IsOwned(switcher.ActiveIndex) ? switcher.ActiveIndex : -1);
            UpdateHolderPose(0f);
            WeaponChanged?.Invoke(ActiveWeapon);
            LoadoutChanged?.Invoke();
        }

        /// <summary>A round was fired, a reload finished or magazines were picked up (the HUD's ammunition counts, step F2).</summary>
        public event Action AmmoChanged;

        /// <summary>A shot or swing damaged something; true if it killed it (the HUD's hit marker, Y4).</summary>
        public event Action<bool> HitConfirmed;
        /// <summary>The held weapon changed (the HUD's slot highlight, Y4).</summary>
        public event Action<WeaponData> WeaponChanged;
        /// <summary>The set of owned weapons changed: the storm took the guns, or the player took one from a dead enemy (the HUD's slots).</summary>
        public event Action LoadoutChanged;

        /// <summary>Debug: every pellet's ray and what it hit (the Stress Test). Subscribe after Awake.</summary>
        public event Action<PelletTrace> PelletTraced
        {
            add
            {
                if (context != null)
                {
                    context.PelletTraced += value;
                }
            }
            remove
            {
                if (context != null)
                {
                    context.PelletTraced -= value;
                }
            }
        }

        public WeaponData ActiveWeapon => weapons != null && weapons.Length > 0 && switcher != null && loadout != null && loadout.IsOwned(switcher.ActiveIndex)
            ? weapons[switcher.ActiveIndex].Data : null;

        /// <summary>False for the normal knife-only start. Guns are recovered through <see cref="TakeNextLootWeapon"/>.</summary>
        public bool HasLoadout => !loadoutLost;

        /// <summary>
        /// The storm of the introduction takes every gun and leaves the knife (step L1). The loss persists through respawns, and it is safe before Awake on the
        /// inactive bootstrap player: the weapons are hidden now and Awake starts the player with the knife only.
        /// </summary>
        public void LoseStartingLoadout()
        {
            loadoutLost = true;
            if (loadout == null)
            {
                foreach (IWeapon weapon in GetComponentsInChildren<IWeapon>(true)) weapon.SetEquipped(false);
            }
            else
            {
                loadout.KeepOnlyMelee();
                int knife = loadout.FirstOwned();
                switcher = new WeaponSwitcher(weapons.Length, Mathf.Max(0, knife));
                ShowOnly(knife);
            }

            WeaponChanged?.Invoke(ActiveWeapon);
            LoadoutChanged?.Invoke();
        }

        /// <summary>True if the player holds this weapon now (before Awake: only the knife).</summary>
        public bool Owns(WeaponData data)
        {
            if (loadout != null)
            {
                return loadout.Owns(data);
            }

            return data != null && (!loadoutLost || data.kind == WeaponKind.Melee);
        }

        /// <summary>The gun a dead enemy's pickup would give now: the SMG first, then the R-700 sniper rifle; null once the player has both.</summary>
        public WeaponData NextLootWeapon()
        {
            int index = loadout != null ? loadout.NextLootIndex() : -1;
            return index >= 0 ? weapons[index].Data : null;
        }

        /// <summary>Takes the next loot gun, shows it to the player by switching to it, and returns it (null if there is none left).</summary>
        public WeaponData TakeNextLootWeapon()
        {
            int index = loadout != null ? loadout.NextLootIndex() : -1;
            if (index < 0)
            {
                return null;
            }

            loadout.Grant(index);
            switcher.Request(index, weapons[index].Data.switchTime);
            LoadoutChanged?.Invoke();
            return weapons[index].Data;
        }
        public bool IsSwitching => switcher != null && !switcher.IsReady;

        /// <summary>The rounds of a weapon (null for a weapon this controller does not have). The knife's has no limit.</summary>
        public WeaponAmmo AmmoOf(WeaponData data)
        {
            if (ammo == null || data == null)
            {
                return null;
            }

            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i].Data == data)
                {
                    return ammo[i];
                }
            }

            return null;
        }

        /// <summary>
        /// A dead body's ammunition: every gun gets its <c>magazinesPerBody</c> spare magazines (2 for the SMG, 1 for the sniper rifle). Returns how many
        /// magazines were added in all (0 before Awake).
        /// </summary>
        public int AddMagazinesFromBody()
        {
            int added = 0;
            for (int i = 0; ammo != null && i < ammo.Length; i++)
            {
                int count = weapons[i].Data.magazinesPerBody;
                if (count > 0 && ammo[i].IsLimited)
                {
                    ammo[i].AddMagazines(count);
                    added += count;
                }
            }

            if (added > 0)
            {
                AmmoChanged?.Invoke();
            }

            return added;
        }

        private WeaponAmmo.State[] CaptureAmmo()
        {
            var states = new WeaponAmmo.State[ammo.Length];
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = ammo[i].Capture();
            }

            return states;
        }

        private void RestoreAmmo(WeaponAmmo.State[] states)
        {
            for (int i = 0; states != null && i < states.Length && i < ammo.Length; i++)
            {
                ammo[i].Restore(states[i]);
            }

            AmmoChanged?.Invoke();
        }

        private void Awake()
        {
            if (input == null || motor == null || motor.Settings == null || look == null || aim == null)
            {
                Debug.LogError("[Blackout] WeaponController needs its input, motor (with settings), look and aim assigned.", this);
                enabled = false;
                return;
            }

            weapons = GetComponentsInChildren<IWeapon>(true);
            if (weapons.Length == 0)
            {
                Debug.LogError("[Blackout] WeaponController found no weapons under it.", this);
                enabled = false;
                return;
            }

            Array.Sort(weapons, (a, b) => a.Data.slot.CompareTo(b.Data.slot));
            var data = new WeaponData[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                data[i] = weapons[i].Data;
            }

            loadout = new WeaponLoadout(data, !loadoutLost);
            ammo = new WeaponAmmo[weapons.Length];
            for (int i = 0; i < ammo.Length; i++)
            {
                ammo[i] = new WeaponAmmo(data[i].magazineSize, data[i].startMagazines, data[i].reloadTime);
            }

            context = new WeaponContext(aim, motor.gameObject, hitMask, noiseChannel, tracers, impacts, OnHit);
            int start = Mathf.Clamp(startIndex, 0, weapons.Length - 1);
            if (!loadout.IsOwned(start))
            {
                start = Mathf.Max(0, loadout.FirstOwned()); // after the storm: the knife
            }

            switcher = new WeaponSwitcher(weapons.Length, start);
            for (int i = 0; i < weapons.Length; i++)
            {
                weapons[i].Initialize(context);
            }

            ShowOnly(loadout.IsOwned(start) ? start : -1);
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        private void Start()
        {
            WeaponChanged?.Invoke(ActiveWeapon);
        }

        private void Update()
        {
            if (switcher == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return; // paused
            }

            bool hasInput = input.isActiveAndEnabled && Cursor.lockState == CursorLockMode.Locked;
            cooldown.Tick(dt);
            TickAmmo(dt);
            if (hasInput)
            {
                HandleSwitchInput();
            }

            if (switcher.Tick(dt))
            {
                ShowOnly(switcher.ActiveIndex);
                WeaponChanged?.Invoke(ActiveWeapon);
            }

            IWeapon weapon = weapons[switcher.ActiveIndex];
            float walkSpeed = motor.Settings.walkSpeed;
            context.MoveFraction = walkSpeed > 0f ? motor.HorizontalSpeed / walkSpeed : 0f;
            context.Grounded = motor.Grounded;
            weapon.Tick(dt);

            if (hasInput && WantsToFire(weapon.Data) && CanFire(weapon.Data))
            {
                Fire(weapon);
                if (ammo[switcher.ActiveIndex].Consume())
                {
                    AmmoChanged?.Invoke();
                }
            }

            UpdateHolderPose(dt);
        }

        /// <summary>Debug and the Stress Test: holds the weapon in this slot at once, without the switch animation.</summary>
        public bool DebugEquip(int slot)
        {
            int index = weapons != null ? IndexOfSlot(slot) : -1;
            if (index < 0 || !loadout.IsOwned(index))
            {
                return false;
            }

            switcher = new WeaponSwitcher(weapons.Length, index);
            ShowOnly(index);
            WeaponChanged?.Invoke(ActiveWeapon);
            return true;
        }

        /// <summary>Debug and the Stress Test: fires the held weapon now, skipping input, the cooldown and the
        /// sprint rule. Recoil and noise happen as for a real shot.</summary>
        public void DebugFire()
        {
            if (weapons != null && switcher != null && loadout.IsOwned(switcher.ActiveIndex))
            {
                Fire(weapons[switcher.ActiveIndex]);
            }
        }

        private void HandleSwitchInput()
        {
            int slot = input.WeaponSlotPressed;
            if (slot > 0)
            {
                Request(IndexOfSlot(slot));
                return;
            }

            // The wheel only works when not already switching (as in the prototype). Wheel down = next.
            float scroll = input.WeaponScroll;
            if (scroll != 0f && switcher.IsReady)
            {
                Request(loadout.NextOwned(switcher.ActiveIndex, scroll < 0f ? 1 : -1));
            }
        }

        private void Request(int index)
        {
            if (loadout.IsOwned(index))
            {
                switcher.Request(index, weapons[index].Data.switchTime);
            }
        }

        private int IndexOfSlot(int slot)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i].Data.slot == slot)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool WantsToFire(WeaponData data)
        {
            return data.automatic ? input.FireHeld : input.FirePressed;
        }

        /// <summary>Not while cooling down or switching, not with an empty or reloading gun, and guns not while sprinting (the knife can).</summary>
        private bool CanFire(WeaponData data)
        {
            bool sprinting = motor.State == MovementState.Sprint;
            return cooldown.IsReady && switcher.IsReady && ammo[switcher.ActiveIndex].CanFire
                && (data.kind == WeaponKind.Melee || !sprinting);
        }

        // Every gun reloads in the background, so switching away from an emptied gun does not cancel its reload.
        private void TickAmmo(float dt)
        {
            bool finished = false;
            for (int i = 0; i < ammo.Length; i++)
            {
                finished |= ammo[i].Tick(dt);
            }

            if (finished)
            {
                AmmoChanged?.Invoke();
            }
        }

        private void Fire(IWeapon weapon)
        {
            WeaponData data = weapon.Data;
            cooldown.Start(data.cooldown);
            weapon.Fire();

            look.AddRecoil(data.recoilPitch * UnityEngine.Random.Range(0.8f, 1.2f),
                UnityEngine.Random.Range(-1f, 1f) * data.recoilYaw);
            kickBack.Kick(data.kickBack * KickVelocityScale);
            kickRotate.Kick(data.kickRotate * KickVelocityScale);
        }

        private void ShowOnly(int index)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                weapons[i].SetEquipped(i == index);
            }
        }

        /// <summary>Dips the holder while switching and pushes it back and up on each shot.</summary>
        private void UpdateHolderPose(float dt)
        {
            kickBack.Update(dt);
            kickRotate.Update(dt);

            float lowered = WeaponMath.Smoothstep(0f, 1f, switcher.Lowered);
            // Unity's camera looks down +Z, so "back towards the eye" is -Z; a positive X rotation pitches down.
            transform.localPosition = restPosition
                + new Vector3(0f, -SwitchDrop * lowered, -kickBack.Offset * KickApplied);
            transform.localRotation = restRotation
                * Quaternion.Euler(SwitchPitch * lowered - kickRotate.Offset * KickApplied, 0f, 0f);
        }

        private void OnHit(bool killed)
        {
            HitConfirmed?.Invoke(killed);
        }
    }
}
