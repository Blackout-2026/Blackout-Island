// Owner: Systems Engineer. SC1: a transient camera/visibility effect; weapon simulation stays on.
using Blackout.Player;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Weapons
{
    [DefaultExecutionOrder(-20)] // Input (-50), scope, then look (-10).
    [DisallowMultipleComponent]
    public sealed class SniperScope : MonoBehaviour
    {
        private const float BlendRate = 14f;
        private const float OverlayThreshold = 0.85f;

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerLook look;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private WeaponController weapons;
        [SerializeField] private WeaponData rifle;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private ViewmodelHands hands;
        [SerializeField] private GameObject overlay;
        [SerializeField] private CanvasGroup crosshair;
        [SerializeField, Range(1f, 110f)] private float scopedFieldOfView = 22f;
        [SerializeField, Range(0.01f, 1f)] private float scopedSensitivity = 0.35f;

        private Health health;
        private float normalFieldOfView;
        private bool initialized;
        private bool focused = true;
        public float Zoom { get; private set; }
        public bool IsScoped => Zoom > OverlayThreshold;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (input == null || look == null || motor == null || weapons == null || rifle == null
                || playerCamera == null || hands == null || overlay == null || crosshair == null || health == null)
            {
                Debug.LogError("[Blackout] SniperScope needs the player, rifle, camera, hands and HUD references assigned.", this);
                enabled = false;
                return;
            }
            normalFieldOfView = playerCamera.fieldOfView;
            initialized = true;
            ResetScope();
        }

        private void OnEnable()
        {
            if (weapons != null)
            {
                weapons.SwitchStarted += ResetScope;
                weapons.WeaponChanged += OnWeaponChanged;
            }
            if (health != null) health.OnDied += ResetScope;
        }

        private void OnDisable()
        {
            if (weapons != null)
            {
                weapons.SwitchStarted -= ResetScope;
                weapons.WeaponChanged -= OnWeaponChanged;
            }
            if (health != null) health.OnDied -= ResetScope;
            ResetScope();
        }

        private bool CanScope(bool mouseCaptured)
        {
            return initialized && isActiveAndEnabled && focused && Time.timeScale > 0f && health.IsAlive
                && input.isActiveAndEnabled && look.isActiveAndEnabled && motor.isActiveAndEnabled
                && mouseCaptured && !input.PausePressed
                && weapons.isActiveAndEnabled && !weapons.IsSwitching && weapons.ActiveWeapon == rifle
                && motor.State != MovementState.Sprint;
        }

        private void Update() => Tick(Time.deltaTime, Cursor.lockState == CursorLockMode.Locked);

        /// <summary>One scope frame. Update supplies the real cursor state; simulations can supply their capture state.</summary>
        public void Tick(float dt, bool mouseCaptured)
        {
            if (!CanScope(mouseCaptured)) { ResetScope(); return; }
            Zoom = PlayerMovementLogic.Damp(Zoom, input.AimHeld ? 1f : 0f, BlendRate, dt);
            if (Zoom < 0.01f) Zoom = 0f;
            if (Zoom > 0.99f) Zoom = 1f;
            Apply();
        }

        private void LateUpdate()
        {
            // Motor/switch/pause changes later in this frame must remove the effect before rendering.
            if (Zoom > 0f && !CanScope(Cursor.lockState == CursorLockMode.Locked)) ResetScope();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) ResetScope();
        }

        private void OnWeaponChanged(WeaponData _) => ResetScope(); // Includes checkpoint restores/debug equips.

        private void ResetScope()
        {
            Zoom = 0f;
            if (initialized) Apply();
        }

        private void Apply()
        {
            playerCamera.fieldOfView = Mathf.Lerp(normalFieldOfView, scopedFieldOfView, Zoom);
            look.ScopeSensitivityMultiplier = Mathf.Lerp(1f, scopedSensitivity, Zoom);
            if (overlay.activeSelf != IsScoped) overlay.SetActive(IsScoped);
            crosshair.alpha = IsScoped ? 0f : 1f;
            hands.SetScopeHidden(IsScoped);
        }
    }
}
