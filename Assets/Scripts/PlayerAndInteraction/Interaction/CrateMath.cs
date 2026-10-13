// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Interaction
{
    /// <summary>The crate's push rule from the prototype, as a pure function so it is tested without physics.</summary>
    public static class CrateMath
    {
        /// <summary>A push never goes faster than this player speed (m/s)…</summary>
        public const float MaxPusherSpeed = 4.5f;
        /// <summary>…and the crate moves at this fraction of it: it is heavy (max 1.9 m/s).</summary>
        public const float SpeedFactor = 0.42f;

        /// <summary>
        /// Velocity the crate is pushed towards: only along the dominant horizontal axis of the push, so it slides
        /// straight along the corridor instead of drifting into the walls.
        /// </summary>
        public static Vector3 PushVelocity(Vector3 direction, float pusherSpeed)
        {
            float speed = Mathf.Min(Mathf.Max(0f, pusherSpeed), MaxPusherSpeed) * SpeedFactor;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            {
                return new Vector3(Mathf.Sign(direction.x) * speed, 0f, 0f);
            }

            if (Mathf.Approximately(direction.z, 0f))
            {
                return Vector3.zero;
            }

            return new Vector3(0f, 0f, Mathf.Sign(direction.z) * speed);
        }
    }
}
