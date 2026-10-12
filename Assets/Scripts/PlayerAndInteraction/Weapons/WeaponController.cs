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
    public class WeaponController : MonoBehaviour
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
        [Tooltip("Layers bullets and the knife can hit. Leave the Player layer out.")]
        [SerializeField] private LayerMask hitMask = ~0;
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

        /// <summary>A shot or swing damaged something; true if it killed it (the HUD's hit marker, Y4).</summary>
        public event Action<bool> HitConfirmed;
        /// <summary>The held weapon changed (the HUD's slot highlight, Y4).</summary>
        public event Action<WeaponData> WeaponChanged;

        public WeaponData ActiveWeapon => weapons != null && weapons.Length > 0 ? weapons[switcher.ActiveIndex].Data : null;
        public bool IsSwitching => switcher != null && !switcher.IsReady;

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
            context = new WeaponContext(aim, motor.gameObject, hitMask, noiseChannel, tracers, impacts, OnHit);
            int start = Mathf.Clamp(startIndex, 0, weapons.Length - 1);
            switcher = new WeaponSwitcher(weapons.Length, start);
            for (int i = 0; i < weapons.Length; i++)
            {
                weapons[i].Initialize(context);
            }

            ShowOnly(start);
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
        }

        private void Start()
        {
            WeaponChanged?.Invoke(ActiveWeapon);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return; // paused
            }

            bool hasInput = input.isActiveAndEnabled && Cursor.lockState == CursorLockMode.Locked;
            cooldown.Tick(dt);
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
            }

            UpdateHolderPose(dt);
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
                Request(switcher.Neighbour(scroll < 0f ? 1 : -1));
            }
        }

        private void Request(int index)
        {
            if (index >= 0)
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

        /// <summary>Not while cooling down or switching, and guns not while sprinting (the knife can).</summary>
        private bool CanFire(WeaponData data)
        {
            bool sprinting = motor.State == MovementState.Sprint;
            return cooldown.IsReady && switcher.IsReady && (data.kind == WeaponKind.Melee || !sprinting);
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
