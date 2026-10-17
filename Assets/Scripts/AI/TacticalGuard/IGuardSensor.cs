// Owner: Core Developer (Tactical Guard AI).
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>
    /// The one question the guard asks the physics world: can these two points see each other? Behind an interface
    /// so tests answer it with a fake and the brain needs no scene geometry.
    /// </summary>
    public interface IGuardSensor
    {
        bool HasLineOfSight(Vector3 from, Vector3 to);
    }
}
