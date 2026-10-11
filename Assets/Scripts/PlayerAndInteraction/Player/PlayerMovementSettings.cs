// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// Every tunable number of the first-person controller, in one asset so designers can tune it without code.
    /// Defaults are the prototype's values (docs/reference/systems-engineer.md sections 1-4).
    /// </summary>
    [CreateAssetMenu(menuName = "Blackout/Player/Movement Settings", fileName = "PlayerMovementSettings")]
    public class PlayerMovementSettings : ScriptableObject
    {
        [Header("Speeds (m/s)")]
        [Min(0f)] public float walkSpeed = 4.3f;
        [Tooltip("Only while sprint is held, moving forward and on the ground.")]
        [Min(0f)] public float sprintSpeed = 7f;

        [Header("Acceleration (m/s²)")]
        [Tooltip("On the ground with movement input.")]
        [Min(0f)] public float groundAcceleration = 38f;
        [Tooltip("On the ground without input (friction).")]
        [Min(0f)] public float groundDeceleration = 30f;
        [Tooltip("In the air: little air control, as in the prototype.")]
        [Min(0f)] public float airAcceleration = 7f;

        [Header("Jump and gravity")]
        [Tooltip("Upward speed at take-off. 5.6 m/s with gravity 17 gives a 0.92 m jump.")]
        [Min(0f)] public float jumpSpeed = 5.6f;
        [Min(0f)] public float gravity = 17f;
        [Tooltip("When walking off a step or down a ramp, snap down this far instead of falling.")]
        [Min(0f)] public float groundSnapDistance = 0.35f;

        [Header("Body (CharacterController)")]
        [Min(0.5f)] public float height = 1.75f;
        [Min(0.1f)] public float radius = 0.35f;
        [Tooltip("Eyes sit this far below the top of the head.")]
        [Min(0f)] public float eyeBelowTop = 0.12f;
        [Min(0f)] public float stepOffset = 0.45f;
        [Tooltip("atan(1.15), the prototype's steepest climbable rise/run.")]
        [Range(0f, 89f)] public float slopeLimit = 49f;

        [Header("Look")]
        [Tooltip("Degrees per pixel of mouse movement (prototype: 0.0021 rad).")]
        [Min(0f)] public float lookSensitivity = 0.12f;
        [Range(0f, 89f)] public float pitchLimit = 87f;
        [Tooltip("Vertical field of view in degrees.")]
        [Range(30f, 110f)] public float fieldOfView = 72f;

        [Header("Footsteps and noise")]
        [Tooltip("No footsteps below this horizontal speed (m/s).")]
        [Min(0f)] public float minFootstepSpeed = 0.6f;
        [Tooltip("Metres walked per footstep.")]
        [Min(0.1f)] public float walkStride = 0.78f;
        [Min(0.1f)] public float sprintStride = 1.05f;
        [Tooltip("Noise radius (m) of a footstep at walk speed. It scales linearly from 0 at a standstill.")]
        [Min(0f)] public float walkNoise = 4.5f;
        [Tooltip("Noise radius (m) of a footstep at sprint speed and above.")]
        [Min(0f)] public float sprintNoise = 13f;
        [Min(0f)] public float jumpNoise = 7f;
        [Tooltip("Landings faster than this (m/s, downward) make a noise.")]
        [Min(0f)] public float loudLandingSpeed = 6f;
        [Min(0f)] public float landingNoise = 9f;

        [Header("Head bob")]
        [Tooltip("Peak-to-peak bob height (m) at amplitude 1 (walk speed).")]
        [Min(0f)] public float bobHeight = 0.05f;
        [Tooltip("Sideways sway (m) at amplitude 1.")]
        [Min(0f)] public float bobSway = 0.025f;
        [Tooltip("Amplitude is speed / walk speed, capped here (sprint = 1.6).")]
        [Min(0f)] public float bobMaxAmplitude = 1.6f;
        [Min(0f)] public float bobAmplitudeDamping = 8f;

        [Header("Landing shake (spring)")]
        [Tooltip("Landings slower than this (m/s, downward) do not kick the camera.")]
        [Min(0f)] public float landingMinSpeed = 2.5f;
        [Min(0f)] public float landingKickPerSpeed = 0.32f;
        [Min(0f)] public float landingMaxKick = 2.8f;
        [Min(0f)] public float landingStiffness = 120f;
        [Min(0f)] public float landingDamping = 14f;
        [Tooltip("Camera drop (m) per unit of spring offset.")]
        [Min(0f)] public float landingCameraDrop = 0.12f;
        [Tooltip("Camera pitch (degrees) per unit of spring offset. Prototype: 0.02 rad.")]
        [Min(0f)] public float landingCameraPitch = 1.15f;

        [Header("Camera smoothing")]
        [Tooltip("How fast the camera follows the body up a step (higher = snappier).")]
        [Min(0f)] public float stepSmoothing = 18f;
        [Tooltip("Roll (degrees) when strafing at sprint speed.")]
        [Min(0f)] public float strafeRoll = 0.7f;
        [Min(0f)] public float strafeRollDamping = 6f;

        /// <summary>Eye height above the feet.</summary>
        public float EyeHeight => height - eyeBelowTop;
    }
}
