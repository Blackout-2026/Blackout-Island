// Owner: World Builder (Security Patrol AI).
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>
    /// Unity adapter for sight queries. Supply Level and Interactable layers to include walls
    /// and closed doors, while excluding the observer, player and trigger volumes.
    /// Create once per listener; the query does not allocate a hit array.
    /// </summary>
    public sealed class PhysicsPerceptionRaycast : IPerceptionRaycast
    {
        private readonly int geometryMask;

        public PhysicsPerceptionRaycast(int geometryMask)
        {
            this.geometryMask = geometryMask;
        }

        public bool IsBlocked(Vector3 origin, Vector3 target)
        {
            Vector3 offset = target - origin;
            float distance = offset.magnitude;
            if (distance <= 0f)
                return false;

            return Physics.Raycast(origin, offset / distance, distance, geometryMask,
                QueryTriggerInteraction.Ignore);
        }
    }
}
