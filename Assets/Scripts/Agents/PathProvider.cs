// Owner: Agent Controller.
using UnityEngine;
using UnityEngine.AI;

namespace Blackout.Agents
{
    /// <summary>
    /// Path planning on the baked NavMesh: <c>NavMesh.CalculatePath</c> turned into the corner array that the
    /// brains' destinations become (replaces the prototype's grid A*). The NavMesh itself is baked by the World
    /// Builder (agent radius 0.4, height 1.8, step 0.4, slope 40) and updated by carved obstacles such as the barricade.
    /// </summary>
    public sealed class PathProvider : IPathProvider
    {
        private readonly NavMeshPath path = new NavMeshPath(); // reused: CalculatePath fills it in place
        private readonly float endpointSampleDistance;
        private readonly int areaMask;

        /// <param name="endpointSampleDistance">How far the start and the destination may be from the NavMesh and still be snapped onto it.</param>
        public PathProvider(float endpointSampleDistance = 2f, int areaMask = NavMesh.AllAreas)
        {
            this.endpointSampleDistance = endpointSampleDistance;
            this.areaMask = areaMask;
        }

        public bool TryCalculatePath(Vector3 from, Vector3 to, out Vector3[] corners)
        {
            corners = null;
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, endpointSampleDistance, areaMask) ||
                !NavMesh.SamplePosition(to, out NavMeshHit end, endpointSampleDistance, areaMask))
            {
                return false;
            }

            // Only complete paths: a partial path would walk the agent into a wall and leave it there pretending to arrive.
            if (!NavMesh.CalculatePath(start.position, end.position, areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            corners = path.corners; // one array per plan; plans are rare (new destination or every few seconds), not per frame
            return corners.Length > 0;
        }

        public bool TrySnap(Vector3 position, float maxDistance, out Vector3 snapped)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, areaMask))
            {
                snapped = hit.position;
                return true;
            }

            snapped = position;
            return false;
        }

        public bool IsBlocked(Vector3 from, Vector3 to)
        {
            // If the start is not on the floor the question cannot be answered, so do not report a block.
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, endpointSampleDistance, areaMask))
            {
                return false;
            }

            return NavMesh.Raycast(start.position, to, out _, areaMask);
        }
    }
}
