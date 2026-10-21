// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// The mover's view of the navigation data: plan a path, stay on the floor, and check the way ahead is open.
    /// The real one asks the NavMesh (<see cref="PathProvider"/>); tests plug in a fake, so the mover's behaviour is
    /// tested without a baked NavMesh.
    /// </summary>
    public interface IPathProvider
    {
        /// <summary>Plans a complete path as a corner array (first corner = start, last = destination). False when there is none.</summary>
        bool TryCalculatePath(Vector3 from, Vector3 to, out Vector3[] corners);

        /// <summary>Finds the nearest point on the floor within <paramref name="maxDistance"/>. False when the position is off the floor.</summary>
        bool TrySnap(Vector3 position, float maxDistance, out Vector3 snapped);

        /// <summary>True when the straight way between two points is cut by an edge of the floor, for example a carved barricade.</summary>
        bool IsBlocked(Vector3 from, Vector3 to);

        /// <summary>
        /// A step from <paramref name="from"/> to <paramref name="to"/> that stays on the floor and never crosses a wall (step F2). The default is
        /// <see cref="TrySnap"/>; the NavMesh one also refuses a step whose snap would carry the agent across an edge of the floor.
        /// </summary>
        bool TryStep(Vector3 from, Vector3 to, float maxDistance, out Vector3 snapped) => TrySnap(to, maxDistance, out snapped);
    }
}
