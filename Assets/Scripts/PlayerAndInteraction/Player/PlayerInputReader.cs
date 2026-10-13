// Owner: Systems Engineer.
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blackout.Player
{
    /// <summary>
    /// The only player script that knows about the Input System. It caches the actions of the "Player" map once
    /// and exposes plain values, so the motor and look scripts never depend on key names.
    /// Runs before them so every script sees the same input in a frame.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerInputReader : MonoBehaviour
    {
        private const string MapName = "Player";

        [Tooltip("PlayerControls.inputactions (Assets/Scripts/PlayerAndInteraction/Input).")]
        [SerializeField] private InputActionAsset actions;

        private InputActionMap map;
        private InputAction move;
        private InputAction look;
        private InputAction jump;
        private InputAction sprint;
        private InputAction pause;
        private InputAction fire;
        private InputAction weapon1;
        private InputAction weapon2;
        private InputAction weapon3;
        private InputAction weaponScroll;
        private InputAction interact;

        /// <summary>x = strafe, y = forward, each -1..1.</summary>
        public Vector2 Move => move.ReadValue<Vector2>();
        /// <summary>Mouse movement this frame, in pixels.</summary>
        public Vector2 LookDelta => look.ReadValue<Vector2>();
        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool SprintHeld => sprint.IsPressed();
        public bool PausePressed => pause.WasPressedThisFrame();
        public bool FirePressed => fire.WasPressedThisFrame();
        public bool FireHeld => fire.IsPressed();
        public bool InteractPressed => interact.WasPressedThisFrame();
        public bool InteractHeld => interact.IsPressed();
        /// <summary>Mouse wheel this frame: positive = up. Only the sign matters (step size differs per OS).</summary>
        public float WeaponScroll => weaponScroll.ReadValue<float>();

        /// <summary>The weapon slot key (1, 2 or 3) pressed this frame, or 0 if none.</summary>
        public int WeaponSlotPressed
        {
            get
            {
                if (weapon1.WasPressedThisFrame())
                {
                    return 1;
                }

                if (weapon2.WasPressedThisFrame())
                {
                    return 2;
                }

                return weapon3.WasPressedThisFrame() ? 3 : 0;
            }
        }

        private void Awake()
        {
            if (actions == null)
            {
                Debug.LogError("[Blackout] PlayerInputReader has no InputActionAsset assigned.", this);
                enabled = false;
                return;
            }

            map = actions.FindActionMap(MapName, true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            jump = map.FindAction("Jump", true);
            sprint = map.FindAction("Sprint", true);
            pause = map.FindAction("Pause", true);
            fire = map.FindAction("Fire", true);
            weapon1 = map.FindAction("Weapon1", true);
            weapon2 = map.FindAction("Weapon2", true);
            weapon3 = map.FindAction("Weapon3", true);
            weaponScroll = map.FindAction("WeaponScroll", true);
            interact = map.FindAction("Interact", true);
        }

        private void OnEnable()
        {
            map?.Enable();
        }

        private void OnDisable()
        {
            map?.Disable();
        }
    }
}
