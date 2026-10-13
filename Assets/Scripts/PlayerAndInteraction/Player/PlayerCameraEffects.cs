// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// Camera feel on its own transform between the body and the pitch pivot: head bob, landing shake,
    /// smoothing when stepping up stairs, a slight roll when strafing, a shake when hurt and the fall-over
    /// when dead. Sitting above the pivot keeps the offsets vertical whatever the pitch. It only offsets the
    /// camera; it never moves the player.
    /// </summary>
    public class PlayerCameraEffects : MonoBehaviour
    {
        // Larger height jumps than this (teleport, respawn) are not smoothed.
        private const float MaxStepLag = 1f;
        // Damage shake (prototype): +0.03 per point of damage (max 1), decays 2.5/s, jitter shake² × 2.3°
        // as sin(47 t) on pitch and cos(39 t) on yaw (rates in radians per second).
        private const float ShakePerDamage = 0.03f;
        private const float ShakeDecay = 2.5f;
        private const float ShakeDegrees = 2.3f;
        private const float ShakePitchRate = 47f;
        private const float ShakeYawRate = 39f;
        // Death camera (prototype): eye drops to 0.25 m, rolls 63° and looks 11.5° down.
        private const float DeadEyeHeight = 0.25f;
        private const float DeadRoll = 63f;
        private const float DeadPitch = 11.5f;

        [SerializeField] private PlayerMotor motor;
        [Tooltip("Optional: shakes the camera when hurt.")]
        [SerializeField] private Health health;
        [Tooltip("Optional: falls the camera over while dead.")]
        [SerializeField] private PlayerHealth playerHealth;

        private PlayerMovementSettings settings;
        private HeadBob bob;
        private LandingSpring landing;
        private float smoothedFeetY;
        private float roll;
        private float shake;

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

            if (health != null)
            {
                health.OnDamaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.Landed -= OnLanded;
            }

            if (health != null)
            {
                health.OnDamaged -= OnDamaged;
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            shake = Mathf.Min(1f, shake + info.amount * ShakePerDamage);
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

            shake = Mathf.Max(0f, shake - ShakeDecay * dt);
            float jitter = shake * shake * ShakeDegrees;
            float time = Time.time;

            float dead = playerHealth != null ? playerHealth.DeathProgress : 0f;
            float deadDrop = (settings.EyeHeight - DeadEyeHeight) * dead;

            Vector2 bobOffset = bob.Offset;
            float drop = landing.Offset * settings.landingCameraDrop;
            transform.localPosition = new Vector3(bobOffset.x, smoothedFeetY - feetY + bobOffset.y + drop - deadDrop, 0f);
            // A negative spring offset (the dip) nods the view down, which is a positive X rotation in Unity.
            float pitch = -landing.Offset * settings.landingCameraPitch + Mathf.Sin(time * ShakePitchRate) * jitter
                + DeadPitch * dead;
            float yaw = Mathf.Cos(time * ShakeYawRate) * jitter;
            transform.localRotation = Quaternion.Euler(pitch, yaw, roll + DeadRoll * dead);
        }
    }
}
