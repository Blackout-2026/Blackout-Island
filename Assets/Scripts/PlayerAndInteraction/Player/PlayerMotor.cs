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
        /// <summary>Raised on standing walk/sprint footsteps for audio. Crouching and prone movement are silent.</summary>
        public event Action<MovementState> Footstep;
        public event Action Jumped;

        public PlayerMovementSettings Settings => settings;
        public MovementState State { get; private set; }
        /// <summary>Standing, crouching or prone (C and Z toggle it). Crouching and prone slow the player down.</summary>
        public Stance Stance { get; private set; }
        /// <summary>Eye height above the feet now: it glides between the stances' heights, and PlayerLook puts the camera pivot there.</summary>
        public float EyeHeight { get; private set; }
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
            EyeHeight = settings.EyeHeight;
            footsteps = new FootstepTracker(settings);
            // What the capsule collides with, minus the player's own layer, so the ground probe never snaps onto
            // something the player would fall through (hitboxes, ragdolls) or onto the player itself.
            groundMask = PhysicsLayers.CollisionMask(gameObject.layer) & ~(1 << gameObject.layer);
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

            // Sprinting forward or jumping stands the player up; C and Z toggle crouching and lying prone.
            bool sprintHeld = hasInput && input.SprintHeld;
            bool wantsToStand = (sprintHeld && moveInput.y > 0f) || (Grounded && hasInput && input.JumpPressed);
            Stance wanted = PlayerMovementLogic.NextStance(Stance, hasInput && input.CrouchPressed, hasInput && input.PronePressed, wantsToStand);
            if (wanted != Stance)
            {
                TrySetStance(wanted);
            }

            EyeHeight = PlayerMovementLogic.Damp(EyeHeight, settings.EyeHeightFor(Stance), settings.stanceEyeDamping, dt);

            // No sprint (and no jump) while there is no room to stand.
            bool canSprint = Stance == Stance.Standing;
            State = PlayerMovementLogic.ChooseState(Grounded, moveInput, sprintHeld && canSprint);
            float speed = PlayerMovementLogic.TargetSpeed(State, Stance, settings);
            Vector2 wish = PlayerMovementLogic.WishVelocity(moveInput, transform.eulerAngles.y, speed);
            float acceleration = PlayerMovementLogic.Acceleration(Grounded, wish.sqrMagnitude > 0f, settings);
            horizontalVelocity = PlayerMovementLogic.Accelerate(horizontalVelocity, wish, acceleration, dt);

            if (Grounded && hasInput && input.JumpPressed && Stance == Stance.Standing)
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
        /// Changes the capsule to the stance's height. Getting taller needs room: a capsule of the new height must not touch anything,
        /// otherwise the player stays as he is (crouched under a low ceiling or a table).
        /// </summary>
        private bool TrySetStance(Stance target)
        {
            float height = PlayerMovementLogic.StanceHeight(target, settings);
            if (height > controller.height + 0.001f)
            {
                float radius = controller.radius * 0.95f;
                Vector3 bottom = transform.position + Vector3.up * (controller.radius + controller.skinWidth + 0.02f);
                Vector3 top = transform.position + Vector3.up * Mathf.Max(height - controller.radius, controller.radius + controller.skinWidth + 0.03f);
                if (Physics.CheckCapsule(bottom, top, radius, groundMask, QueryTriggerInteraction.Ignore))
                {
                    return false;
                }
            }

            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f); // pivot at the feet
            Stance = target;
            return true;
        }

        /// <summary>Back to a standing capsule at once (teleport, respawn). Nothing is checked: the player is being placed.</summary>
        public void ResetStance()
        {
            Stance = Stance.Standing;
            ApplyBodySettings(controller, settings);
            EyeHeight = settings.EyeHeight;
        }

        /// <summary>Temporary cinematic ownership must not resume a pre-cutscene sprint or falling velocity.</summary>
        public void StopMotion()
        {
            horizontalVelocity = Vector2.zero;
            verticalVelocity = Grounded ? -GroundedStickSpeed : 0f;
            HorizontalSpeed = LastMoveDistance = 0f;
            footsteps.Reset();
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
            if (Stance != Stance.Standing)
            {
                // Suppress both playback and AI hearing; do not carry silent travel into the next standing step.
                footsteps.Reset();
                return;
            }

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
            ResetStance();
            controller.enabled = true;
            horizontalVelocity = Vector2.zero;
            verticalVelocity = 0f;
            Grounded = false;
            footsteps.Reset();
        }
    }
}
