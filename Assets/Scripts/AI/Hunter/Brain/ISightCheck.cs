// Owner: Systems Engineer (Hunter AI).
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>Whether nothing blocks the view between two points. Tests plug in a fake.</summary>
    public interface ISightCheck
    {
        bool IsClear(Vector3 from, Vector3 to);
    }

    /// <summary>One Physics.Linecast against the sight blockers (walls and doors); triggers never block.</summary>
    public sealed class RaycastSightCheck : ISightCheck
    {
        public RaycastSightCheck(int blockers)
        {
            Blockers = blockers;
        }

        public int Blockers { get; }

        public bool IsClear(Vector3 from, Vector3 to)
        {
            return !Physics.Linecast(from, to, Blockers, QueryTriggerInteraction.Ignore);
        }
    }
}
