// Owner: Systems Engineer.
using System;
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// Fall damage and checkpoint rules as pure functions (docs/reference/systems-engineer.md section 8),
    /// so EditMode tests check them without a scene.
    /// </summary>
    public static class PlayerHealthRules
    {
        /// <summary>Markers with this id prefix are the escape route's checkpoints.</summary>
        public const string EscapeCheckpointPrefix = "Checkpoint_Escape";

        /// <summary>
        /// Damage for a landing: (speed − safeSpeed) × damagePerSpeed above the safe speed, otherwise 0.
        /// Prototype: safe up to 13 m/s, 8 HP per m/s above it.
        /// </summary>
        public static float FallDamage(float landingSpeed, float safeSpeed, float damagePerSpeed)
        {
            return landingSpeed > safeSpeed ? (landingSpeed - safeSpeed) * damagePerSpeed : 0f;
        }

        /// <summary>
        /// Escape checkpoints only count after the cure is taken, and the others only before, so a death on the way
        /// out never sends the player back underground.
        /// </summary>
        public static bool IsCheckpointEligible(string id, bool cureTaken)
        {
            bool escape = id != null && id.StartsWith(EscapeCheckpointPrefix, StringComparison.Ordinal);
            return escape == cureTaken;
        }

        public static bool InReach(Vector3 player, Vector3 checkpoint, float radius)
        {
            return (player - checkpoint).sqrMagnitude <= radius * radius;
        }
    }
}
