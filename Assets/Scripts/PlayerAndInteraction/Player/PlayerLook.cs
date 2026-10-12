// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// Mouse look with cursor lock. Yaw turns the whole body (so movement follows the view), pitch tilts only
    /// the camera pivot. Esc releases the cursor and a click locks it again; look is ignored while released.
    /// Runs before <see cref="PlayerMotor"/> so the motor moves along this frame's view direction.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class PlayerLook : MonoBehaviour
    {
        // Recoil returns to the original aim at this rate (the prototype's damp rate 7).
        private const float RecoilRecovery = 7f;

        [SerializeField] private PlayerMovementSettings settings;
        [SerializeField] private PlayerInputReader input;
        [Tooltip("Transform at eye height that pitches up and down (under the camera rig). The camera sits under it.")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera playerCamera;

        private float pitch;
        private float recoilPitch;
        private float recoilYaw;

        public float Pitch => pitch;

        private void Awake()
        {
            if (settings == null || input == null || cameraPivot == null)
            {
                Debug.LogError("[Blackout] PlayerLook needs its settings, input and camera pivot assigned.", this);
                enabled = false;
                return;
            }

            if (playerCamera != null)
            {
                playerCamera.fieldOfView = settings.fieldOfView;
            }

            cameraPivot.localPosition = new Vector3(0f, settings.EyeHeight, 0f);
        }

        private void OnEnable()
        {
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void Update()
        {
            if (!input.isActiveAndEnabled)
            {
                return;
            }

            if (input.PausePressed)
            {
                SetCursorLocked(false);
            }
            else if (Cursor.lockState != CursorLockMode.Locked && input.FirePressed)
            {
                SetCursorLocked(true);
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            // Mouse delta is already "pixels this frame", so it is not multiplied by deltaTime.
            Vector2 delta = input.LookDelta * settings.lookSensitivity;
            // Yaw is read back from the body each frame, so a teleport that sets the rotation is respected.
            float yaw = transform.eulerAngles.y + delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -settings.pitchLimit, settings.pitchLimit);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Recoil is an offset on top of the aim, so the view climbs while firing and settles back afterwards.
            float dt = Time.deltaTime;
            recoilPitch = PlayerMovementLogic.Damp(recoilPitch, 0f, RecoilRecovery, dt);
            recoilYaw = PlayerMovementLogic.Damp(recoilYaw, 0f, RecoilRecovery, dt);
            cameraPivot.localRotation = Quaternion.Euler(pitch - recoilPitch, recoilYaw, 0f);
        }

        /// <summary>Kicks the view up by pitchDegrees and sideways by yawDegrees (weapon recoil).</summary>
        public void AddRecoil(float pitchDegrees, float yawDegrees)
        {
            recoilPitch += pitchDegrees;
            recoilYaw += yawDegrees;
        }

        /// <summary>Levels the view, for example after a teleport or respawn.</summary>
        public void ResetPitch()
        {
            pitch = 0f;
            recoilPitch = 0f;
            recoilYaw = 0f;
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.identity;
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
