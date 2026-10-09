// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Walks along a corner array: which corner to head for, when a corner counts as reached, when the destination
    /// is reached, and how much to slow down on the last leg. Pure C#, with no NavMesh and no Transform, so the
    /// corner and arrival rules are tested without a scene. All distances are flat (x and z only), like the
    /// prototype, because the NavMesh already keeps the agent on the floor.
    /// </summary>
    public sealed class PathTracker
    {
        private Vector3[] corners;
        private int index;

        public bool HasPath => corners != null && corners.Length > 0;
        public bool Arrived { get; private set; }
        public Vector3[] Corners => corners;
        public int CornerIndex => index;
        public bool OnLastCorner => HasPath && index == corners.Length - 1;

        /// <summary>The corner the agent is heading for. Only valid when <see cref="HasPath"/>.</summary>
        public Vector3 Target => corners[index];

        /// <summary>
        /// Starts following a new path. The first corner of a NavMesh path is where the agent stood when it was
        /// planned, so it is skipped (prototype: pathIdx = 1): an agent bumped away in the meantime heads for the
        /// next corner instead of walking back to its old position. A one-corner path starts at that corner.
        /// </summary>
        public void SetPath(Vector3[] newCorners)
        {
            corners = newCorners != null && newCorners.Length > 0 ? newCorners : null;
            index = corners != null && corners.Length > 1 ? 1 : 0;
            Arrived = false;
        }

        public void Clear()
        {
            corners = null;
            index = 0;
            Arrived = false;
        }

        /// <summary>
        /// Moves on to the next corner while the current one is within <see cref="MovementSettings.cornerReachDistance"/>
        /// (several corners may be skipped in one call), and sets <see cref="Arrived"/> once the last corner is within
        /// <see cref="MovementSettings.arriveDistance"/>. Arrival is sticky until a new path is set.
        /// </summary>
        public void Advance(Vector3 position, MovementSettings settings)
        {
            if (!HasPath || Arrived)
            {
                return;
            }

            while (index < corners.Length - 1 && FlatDistance(position, corners[index]) < settings.cornerReachDistance)
            {
                index++;
            }

            if (index == corners.Length - 1 && FlatDistance(position, corners[index]) < settings.arriveDistance)
            {
                Arrived = true;
            }
        }

        /// <summary>
        /// 1 while there is distance to cover, easing down to <see cref="MovementSettings.arrivalMinSpeedFactor"/> over
        /// the last <see cref="MovementSettings.arrivalSlowDistance"/> metres of the final leg (prototype: min(1, d/1.4 + 0.15)).
        /// </summary>
        public float SpeedFactor(Vector3 position, MovementSettings settings)
        {
            if (!OnLastCorner)
            {
                return 1f;
            }

            float distance = FlatDistance(position, corners[index]);
            return Mathf.Min(1f, distance / settings.arrivalSlowDistance + settings.arrivalMinSpeedFactor);
        }

        /// <summary>Distance left along the path: to the current corner, then corner to corner. Zero without a path.</summary>
        public float RemainingDistance(Vector3 position)
        {
            if (!HasPath)
            {
                return 0f;
            }

            float total = FlatDistance(position, corners[index]);
            for (int i = index; i < corners.Length - 1; i++)
            {
                total += FlatDistance(corners[i], corners[i + 1]);
            }
            return total;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
