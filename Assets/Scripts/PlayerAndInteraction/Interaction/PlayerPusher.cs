// Owner: Systems Engineer.
using Blackout.Player;
using UnityEngine;

namespace Blackout.Interaction
{
    /// <summary>
    /// A CharacterController does not push rigidbodies by itself. This passes the player's walking direction and
    /// speed to a PushableCrate it walks into. It must sit next to the CharacterController (OnControllerColliderHit).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerPusher : MonoBehaviour
    {
        // Hits from above (standing on the crate) do not push it.
        private const float MaxDownward = -0.3f;

        [SerializeField] private PlayerMotor motor;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (motor == null || motor.State == MovementState.Idle || hit.moveDirection.y < MaxDownward
                || hit.rigidbody == null)
            {
                return;
            }

            if (hit.rigidbody.TryGetComponent(out PushableCrate crate))
            {
                // The intended speed, not the achieved one: against the crate the player barely moves.
                float speed = PlayerMovementLogic.TargetSpeed(motor.State, motor.Settings);
                crate.Push(hit.moveDirection, speed, gameObject);
            }
        }
    }
}
