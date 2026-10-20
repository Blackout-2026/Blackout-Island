// Owner: Systems Engineer (Hunter AI).
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>Counts the walls a sound crosses between two points. Tests can plug in a fake.</summary>
    public interface IOcclusionCounter
    {
        /// <summary>Walls between <paramref name="from"/> and <paramref name="to"/>, at most <paramref name="maxWalls"/>.</summary>
        int CountWalls(Vector3 from, Vector3 to, int maxWalls);
    }
}
