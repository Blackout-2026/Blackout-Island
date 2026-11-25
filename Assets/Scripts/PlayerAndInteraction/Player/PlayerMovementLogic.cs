// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Player
{
    public enum MovementState
    {
        Idle,
        Walk,
        Sprint,
        Airborne
    }

    /// <summary>How the body is held: C toggles crouching and Z toggles lying prone (step F2).</summary>
    public enum Stance
    {
        Standing,
        Crouching,
        Prone
    }

    /// <summary>
    /// The movement rules of the first-person controller as pure functions, so EditMode tests can check them
    /// without a scene. PlayerMotor calls these every frame and only applies the result.
    /// </summary>
    public static class PlayerMovementLogic
    {
        private const float InputDeadZone = 0.1f;

        /// <summary>
        /// Picks the state from the inputs. Sprint needs the forward key, as in the prototype:
        /// holding sprint while strafing or backing up only walks.
        /// </summary>
        public static MovementState ChooseState(bool grounded, Vector2 moveInput, bool sprintHeld)
        {
            if (!grounded)
            {
                return MovementState.Airborne;
            }

            if (moveInput.sqrMagnitude < InputDeadZone * InputDeadZone)
            {
                return MovementState.Idle;
            }

            return sprintHeld && moveInput.y > InputDeadZone ? MovementState.Sprint : MovementState.Walk;
        }

        /// <summary>
        /// Top speed for a state. In the air the target is walk speed (the prototype's sprint needs the ground),
        /// so a sprint jump slowly loses its extra speed.
        /// </summary>
        public static float TargetSpeed(MovementState state, PlayerMovementSettings settings)
        {
            return state == MovementState.Sprint ? settings.sprintSpeed : settings.walkSpeed;
        }

        /// <summary>Top speed for a state and a stance: crouching and lying prone slow the player down (they only apply to walking).</summary>
        public static float TargetSpeed(MovementState state, Stance stance, PlayerMovementSettings settings)
        {
            return TargetSpeed(state, settings) * StanceSpeedMultiplier(stance, settings);
        }

        public static float StanceSpeedMultiplier(Stance stance, PlayerMovementSettings settings)
        {
            switch (stance)
            {
                case Stance.Crouching:
                    return settings.crouchSpeedMultiplier;
                case Stance.Prone:
                    return settings.proneSpeedMultiplier;
                default:
                    return 1f;
            }
        }

        /// <summary>Capsule height of a stance.</summary>
        public static float StanceHeight(Stance stance, PlayerMovementSettings settings)
        {
            switch (stance)
            {
                case Stance.Crouching:
                    return settings.crouchHeight;
                case Stance.Prone:
                    return settings.proneHeight;
                default:
                    return settings.height;
            }
        }

        /// <summary>
        /// The stance the player wants after this frame's keys. C toggles crouching (from prone it comes up to a crouch), Z toggles prone, and
        /// sprinting or jumping (<paramref name="wantsToStand"/>) stands up. Whether there is room to stand is the motor's question.
        /// </summary>
        public static Stance NextStance(Stance current, bool crouchPressed, bool pronePressed, bool wantsToStand)
        {
            Stance next = current;
            if (crouchPressed)
            {
                next = current == Stance.Crouching ? Stance.Standing : Stance.Crouching;
            }

            if (pronePressed)
            {
                next = current == Stance.Prone ? Stance.Standing : Stance.Prone;
            }

            return wantsToStand && !pronePressed && !crouchPressed ? Stance.Standing : next;
        }

        public static float Acceleration(bool grounded, bool hasInput, PlayerMovementSettings settings)
        {
            if (!grounded)
            {
                return settings.airAcceleration;
            }

            return hasInput ? settings.groundAcceleration : settings.groundDeceleration;
        }

        /// <summary>
        /// Horizontal velocity (x, z) wanted for the input. Input x is strafe, y is forward; yaw is the body's
        /// rotation about Y in degrees. Diagonal input is clamped so it is not faster than straight input.
        /// </summary>
        public static Vector2 WishVelocity(Vector2 moveInput, float yawDegrees, float speed)
        {
            Vector2 input = Vector2.ClampMagnitude(moveInput, 1f);
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(yaw);
            float cos = Mathf.Cos(yaw);
            // forward = (sin, cos), right = (cos, -sin) on the XZ plane
            return new Vector2(sin * input.y + cos * input.x, cos * input.y - sin * input.x) * speed;
        }

        /// <summary>Moves the velocity towards the target by at most acceleration × dt (the prototype's rule).</summary>
        public static Vector2 Accelerate(Vector2 current, Vector2 target, float acceleration, float dt)
        {
            return Vector2.MoveTowards(current, target, acceleration * dt);
        }

        /// <summary>
        /// Footstep noise radius (m) scaled by speed: a straight line from 0 to walkNoise at walk speed, then from
        /// walkNoise to sprintNoise at sprint speed, clamped above it. Walk and sprint match the prototype exactly.
        /// </summary>
        public static float FootstepLoudness(float horizontalSpeed, PlayerMovementSettings settings)
        {
            if (horizontalSpeed <= 0f || settings.walkSpeed <= 0f)
            {
                return 0f;
            }

            if (horizontalSpeed <= settings.walkSpeed)
            {
                return settings.walkNoise * (horizontalSpeed / settings.walkSpeed);
            }

            float t = Mathf.InverseLerp(settings.walkSpeed, settings.sprintSpeed, horizontalSpeed);
            return Mathf.Lerp(settings.walkNoise, settings.sprintNoise, t);
        }

        /// <summary>
        /// Frame-rate independent smoothing (the prototype's U.damp): moves a fraction 1 - e^(-rate × dt) of the
        /// way to the target, so the result is the same at 30 or 144 fps.
        /// </summary>
        public static float Damp(float current, float target, float rate, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
        }
    }
}
