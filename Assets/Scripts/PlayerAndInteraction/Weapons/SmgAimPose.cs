// SG1: a weapon-specific visual pose. WeaponController still owns recoil, switching and shots.
using Blackout.Player;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Weapons
{
    [DefaultExecutionOrder(30)]
    [DisallowMultipleComponent]
    public sealed class SmgAimPose : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerLook look;
        [SerializeField] private Health health;
        [SerializeField] private WeaponController weapons;
        [SerializeField] private WeaponData data;
        [SerializeField] private ViewmodelHands hands;
        [SerializeField] private Transform aimReference;
        [SerializeField, Min(.1f)] private float eyeDistance = .32f;
        [SerializeField, Min(1f)] private float blendRate = 14f;

        private Vector3 hipPosition;
        private Quaternion hipRotation;
        private bool initialized, hasHipPose, focused = true;
        public float Blend { get; private set; }

        private void Awake()
        {
            CacheHipPose();
            initialized = input != null && motor != null && look != null && health != null && weapons != null
                && data != null && hands != null && aimReference != null;
            if (!initialized) Debug.LogError("[SG1] SMG aim pose needs its player, weapon and sight references.", this);
        }

        private void LateUpdate() => Tick(Time.deltaTime, Cursor.lockState == CursorLockMode.Locked);

        public void Tick(float dt, bool mouseCaptured)
        {
            if (!initialized || !isActiveAndEnabled) return;
            bool allowed = focused && mouseCaptured && Time.timeScale > 0 && health.IsAlive
                && input.isActiveAndEnabled && look.isActiveAndEnabled && motor.isActiveAndEnabled
                && weapons.isActiveAndEnabled && weapons.ActiveWeapon == data && !weapons.IsSwitching
                && !(weapons.AmmoOf(data)?.Reloading ?? false) && hands.ShownIgnoringSight
                && motor.State != MovementState.Sprint && !input.PausePressed;
            if (!allowed) { ResetPose(); return; }
            Blend = PlayerMovementLogic.Damp(Blend, input.AimHeld ? 1 : 0, blendRate, dt);
            if (Blend < .001f) Blend = 0;
            if (Blend > .999f) Blend = 1;
            Apply(Blend);
        }

        // Also used by the saved-camera review. Parent-holder recoil is deliberately left in place.
        public void Apply(float amount)
        {
            if (aimReference == null) return;
            CacheHipPose();
            Quaternion rotation = Quaternion.Inverse(aimReference.localRotation);
            Vector3 position = new Vector3(0, 0, eyeDistance / transform.parent.localScale.z)
                - rotation * aimReference.localPosition;
            transform.SetLocalPositionAndRotation(Vector3.Lerp(hipPosition, position, amount),
                Quaternion.Slerp(hipRotation, rotation, amount));
        }

        private void CacheHipPose()
        {
            if (hasHipPose) return;
            hipPosition = transform.localPosition;
            hipRotation = transform.localRotation;
            hasHipPose = true;
        }

        private void ResetPose()
        {
            Blend = 0;
            if (initialized) transform.SetLocalPositionAndRotation(hipPosition, hipRotation);
        }

        private void OnDisable() => ResetPose();
        private void OnApplicationFocus(bool focus)
        {
            focused = focus;
            if (!focus) ResetPose();
        }
    }
}
