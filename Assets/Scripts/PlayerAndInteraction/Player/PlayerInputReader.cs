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

        /// <summary>x = strafe, y = forward, each -1..1.</summary>
        public Vector2 Move => move.ReadValue<Vector2>();
        /// <summary>Mouse movement this frame, in pixels.</summary>
        public Vector2 LookDelta => look.ReadValue<Vector2>();
        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool SprintHeld => sprint.IsPressed();
        public bool PausePressed => pause.WasPressedThisFrame();
        public bool FirePressed => fire.WasPressedThisFrame();

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
