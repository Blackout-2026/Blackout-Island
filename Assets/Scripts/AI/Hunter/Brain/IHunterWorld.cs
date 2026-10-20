// Owner: Systems Engineer (Hunter AI).
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// What the Hunter tree asks of the world besides its facts: cover, its forest waypoints and random numbers.
    /// The brain provides the real one (markers and terrain trees); tests plug in a fake.
    /// </summary>
    public interface IHunterWorld
    {
        int WaypointCount { get; }
        Vector3 GetWaypoint(int index);

        /// <summary>The nearest cover within <paramref name="radius"/> of the Hunter, as a point behind it from the threat.</summary>
        bool TryFindCover(Vector3 self, Vector3 threat, float radius, out Vector3 point);

        /// <summary>
        /// The next cover on an advance: within <paramref name="range"/> of the Hunter and at least
        /// <paramref name="minGain"/> metres nearer <paramref name="target"/> than the Hunter is.
        /// </summary>
        bool TryFindAdvanceCover(Vector3 self, Vector3 target, float range, float minGain, out Vector3 point);

        /// <summary>A random number in [0, 1).</summary>
        float Random01();
    }
}
