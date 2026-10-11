// Owner: Systems Engineer.
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// CharacterController movement: walk, sprint, jump, gravity, steps, slopes and snapping down stairs.
    /// The rules live in <see cref="PlayerMovementLogic"/>; this script only reads input, applies them and
    /// publishes footstep, jump and landing noises for the Hunter's hearing.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        // Keeps the capsule pressed onto the ground while grounded, so isGrounded does not flicker on bumps.
        private const float GroundedStickSpeed = 1f;

        [SerializeField] private PlayerMovementSettings settings;
        [SerializeField] private PlayerInputReader input;
        [Tooltip("Noise.asset in Assets/ScriptableObjects/Events. Leave empty to make no noise.")]
        [SerializeField] private NoiseEventChannel noiseChannel;

        private CharacterController controller;
        private FootstepTracker footsteps;
        private Vector2 horizontalVelocity;
        private float verticalVelocity;
        private int groundMask;

        /// <summary>Downward speed (m/s) at the moment of landing.</summary>
        public event Action<float> Landed;
        /// <summary>Raised on every footstep, with the state it was taken in (for footstep audio later).</summary>
        public event Action<MovementState> Footstep;
        public event Action Jumped;

        public PlayerMovementSettings Settings => settings;
        public MovementState State { get; private set; }
        public bool Grounded { get; private set; }
        public Vector3 Velocity => new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.y);
        /// <summary>Horizontal distance actually travelled last frame (after collisions).</summary>
        public float LastMoveDistance { get; private set; }
        /// <summary>Horizontal speed actually achieved last frame (after collisions).</summary>
        public float HorizontalSpeed { get; private set; }
        public float CurrentStride => footsteps.Stride(State == MovementState.Sprint);

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (settings == null || input == null)
            {
                Debug.LogError("[Blackout] PlayerMotor needs its settings and input assigned.", this);
                enabled = false;
                return;
            }

            ApplyBodySettings(controller, settings);
            footsteps = new FootstepTracker(settings);
            // Everything except the player's own layer, so the ground probe never hits the player.
            groundMask = ~(1 << gameObject.layer);
        }

        /// <summary>Copies the body numbers onto a CharacterController (also used by the prefab builder).</summary>
        public static void ApplyBodySettings(CharacterController target, PlayerMovementSettings values)
        {
            target.height = values.height;
            target.radius = values.radius;
            target.center = new Vector3(0f, values.height * 0.5f, 0f); // pivot at the feet
            target.stepOffset = values.stepOffset;
            target.slopeLimit = values.slopeLimit;
            target.minMoveDistance = 0f; // otherwise very slow movement is ignored
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return; // paused
            }

            bool hasInput = input.isActiveAndEnabled;
            Vector2 moveInput = hasInput ? input.Move : Vector2.zero;
            bool wasGrounded = Grounded;

            State = PlayerMovementLogic.ChooseState(Grounded, moveInput, hasInput && input.SprintHeld);
            float speed = PlayerMovementLogic.TargetSpeed(State, settings);
            Vector2 wish = PlayerMovementLogic.WishVelocity(moveInput, transform.eulerAngles.y, speed);
            float acceleration = PlayerMovementLogic.Acceleration(Grounded, wish.sqrMagnitude > 0f, settings);
            horizontalVelocity = PlayerMovementLogic.Accelerate(horizontalVelocity, wish, acceleration, dt);

            if (Grounded && hasInput && input.JumpPressed)
            {
                verticalVelocity = settings.jumpSpeed;
                Grounded = false;
                Jumped?.Invoke();
                PublishNoise(settings.jumpNoise, NoiseType.Footstep);
            }

            verticalVelocity -= settings.gravity * dt;

            Vector3 start = transform.position;
            Vector3 motion = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.y) * dt;
            CollisionFlags flags = controller.Move(motion);
            Vector3 travelled = transform.position - start;

            if ((flags & CollisionFlags.Sides) != 0)
            {
                // Sliding along a wall: keep only the velocity that actually happened, so it does not build up.
                horizontalVelocity = new Vector2(travelled.x, travelled.z) / dt;
            }

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f; // head hit the ceiling
            }

            Grounded = (flags & CollisionFlags.Below) != 0;
            if (!Grounded && wasGrounded && verticalVelocity <= 0f)
            {
                Grounded = SnapToGround();
            }

            if (Grounded)
            {
                if (!wasGrounded)
                {
                    Land(-verticalVelocity);
                }

                verticalVelocity = -GroundedStickSpeed;
            }

            LastMoveDistance = new Vector2(travelled.x, travelled.z).magnitude;
            HorizontalSpeed = LastMoveDistance / dt;
            UpdateFootsteps();
        }

        /// <summary>
        /// Walking off a step or down a ramp would start a fall. If ground is within the snap distance below,
        /// move straight down onto it instead (the prototype's 0.35 m ground snap).
        /// </summary>
        private bool SnapToGround()
        {
            float radius = controller.radius;
            Vector3 bottomSphere = transform.position + Vector3.up * radius;
            float distance = settings.groundSnapDistance + controller.skinWidth;
            if (!Physics.SphereCast(bottomSphere, radius * 0.95f, Vector3.down, out RaycastHit hit, distance,
                    groundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (Vector3.Angle(hit.normal, Vector3.up) > controller.slopeLimit)
            {
                return false; // too steep to stand on: let the player fall
            }

            CollisionFlags flags = controller.Move(Vector3.down * hit.distance);
            return (flags & CollisionFlags.Below) != 0;
        }

        private void Land(float landingSpeed)
        {
            footsteps.Reset();
            Landed?.Invoke(landingSpeed);
            if (landingSpeed > settings.loudLandingSpeed)
            {
                PublishNoise(settings.landingNoise, NoiseType.Footstep);
            }
        }

        private void UpdateFootsteps()
        {
            bool sprinting = State == MovementState.Sprint;
            if (!footsteps.Advance(Grounded, sprinting, LastMoveDistance, HorizontalSpeed))
            {
                return;
            }

            Footstep?.Invoke(State);
            PublishNoise(PlayerMovementLogic.FootstepLoudness(HorizontalSpeed, settings),
                sprinting ? NoiseType.Sprint : NoiseType.Footstep);
        }

        private void PublishNoise(float loudness, NoiseType type)
        {
            if (noiseChannel != null && loudness > 0f)
            {
                noiseChannel.Raise(new NoiseEvent(transform.position, loudness, type, gameObject));
            }
        }

        /// <summary>
        /// Moves the player instantly (debug teleport, respawn). The controller is disabled for the move,
        /// because a CharacterController overwrites a direct position change.
        /// </summary>
        public void Teleport(Vector3 position, float yawDegrees)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            controller.enabled = true;
            horizontalVelocity = Vector2.zero;
            verticalVelocity = 0f;
            Grounded = false;
            footsteps.Reset();
        }
    }
}
