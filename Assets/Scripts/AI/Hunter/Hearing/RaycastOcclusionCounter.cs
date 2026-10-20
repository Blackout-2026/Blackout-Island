// Owner: Systems Engineer (Hunter AI).
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// Counts walls with one raycast (docs/reference/systems-engineer.md section 14). Unity returns one hit per
    /// collider, so each wall counts once, like the prototype's "continue 0.3 m past it". Triggers are ignored, and
    /// the terrain is skipped as in the prototype, so a hill is not a wall. The hit buffer is allocated once.
    /// </summary>
    public sealed class RaycastOcclusionCounter : IOcclusionCounter
    {
        // Enough for 3 walls plus the terrain and a few props; extra hits are dropped by Unity.
        private const int BufferSize = 16;

        private readonly RaycastHit[] hits = new RaycastHit[BufferSize];

        public RaycastOcclusionCounter(int layerMask)
        {
            LayerMask = layerMask;
        }

        public int LayerMask { get; }

        public int CountWalls(Vector3 from, Vector3 to, int maxWalls)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (maxWalls <= 0 || distance < 1e-4f)
            {
                return 0;
            }

            int count = Physics.RaycastNonAlloc(from, delta / distance, hits, distance, LayerMask,
                QueryTriggerInteraction.Ignore);
            int walls = 0;
            for (int i = 0; i < count && walls < maxWalls; i++)
            {
                if (!(hits[i].collider is TerrainCollider))
                {
                    walls++;
                }
            }

            return walls;
        }
    }
}
