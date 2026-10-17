// Owner: Core Developer (Tactical Guard AI).
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>Line of sight by a single Linecast against the layers that block sight (walls, doors, props). Allocation-free.</summary>
    public sealed class RaycastGuardSensor : IGuardSensor
    {
        private readonly LayerMask blockers;

        public RaycastGuardSensor(LayerMask blockers)
        {
            this.blockers = blockers;
        }

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            return !Physics.Linecast(from, to, blockers, QueryTriggerInteraction.Ignore);
        }
    }
}
