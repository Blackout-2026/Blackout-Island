// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// Camera feel on its own transform between the body and the pitch pivot: head bob, landing shake,
    /// smoothing when stepping up stairs, and a slight roll when strafing. Sitting above the pivot keeps the
    /// offsets vertical whatever the pitch. It only offsets the camera; it never moves the player.
    /// </summary>
    public class PlayerCameraEffects : MonoBehaviour
    {
        // Larger height jumps than this (teleport, respawn) are not smoothed.
        private const float MaxStepLag = 1f;

        [SerializeField] private PlayerMotor motor;

        private PlayerMovementSettings settings;
        private HeadBob bob;
        private LandingSpring landing;
        private float smoothedFeetY;
        private float roll;

        private void Awake()
        {
            if (motor == null || motor.Settings == null)
            {
                Debug.LogError("[Blackout] PlayerCameraEffects needs the PlayerMotor (with settings) assigned.", this);
                enabled = false;
                return;
            }

            settings = motor.Settings;
            bob = new HeadBob(settings);
            landing = new LandingSpring(settings);
        }

        private void OnEnable()
        {
            if (motor != null)
            {
                motor.Landed += OnLanded;
                smoothedFeetY = motor.transform.position.y;
            }
        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.Landed -= OnLanded;
            }
        }

        private void OnLanded(float landingSpeed)
        {
            landing.Impact(landingSpeed);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !motor.isActiveAndEnabled)
            {
                return;
            }

            bool grounded = motor.Grounded;
            bob.Update(grounded, motor.LastMoveDistance, motor.CurrentStride, motor.HorizontalSpeed, dt);
            landing.Update(dt);

            // Stairs move the body up in one jump; the camera catches up smoothly. Only on the ground, so jumps
            // and falls are never delayed.
            float feetY = motor.transform.position.y;
            smoothedFeetY = grounded
                ? PlayerMovementLogic.Damp(smoothedFeetY, feetY, settings.stepSmoothing, dt)
                : feetY;
            if (Mathf.Abs(smoothedFeetY - feetY) > MaxStepLag)
            {
                smoothedFeetY = feetY;
            }

            float sideways = Vector3.Dot(motor.Velocity, motor.transform.right);
            float targetRoll = settings.sprintSpeed > 0f
                ? -Mathf.Clamp(sideways / settings.sprintSpeed, -1f, 1f) * settings.strafeRoll
                : 0f;
            roll = PlayerMovementLogic.Damp(roll, targetRoll, settings.strafeRollDamping, dt);

            Vector2 bobOffset = bob.Offset;
            float drop = landing.Offset * settings.landingCameraDrop;
            transform.localPosition = new Vector3(bobOffset.x, smoothedFeetY - feetY + bobOffset.y + drop, 0f);
            // A negative spring offset (the dip) nods the view down, which is a positive X rotation in Unity.
            transform.localRotation = Quaternion.Euler(-landing.Offset * settings.landingCameraPitch, 0f, roll);
        }
    }
}
