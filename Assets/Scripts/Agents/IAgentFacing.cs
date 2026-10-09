// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Optional extra for a mover that also turns the agent. IAgentMover is a shared contract that only covers
    /// movement, so facing the look target (an agent shooting while it walks sideways) is passed separately.
    /// AgentRoot calls it with every intent when the mover implements it.
    /// </summary>
    public interface IAgentFacing
    {
        /// <param name="hasLookTarget">False means face the direction of travel.</param>
        /// <param name="lookTarget">World position to face, flat (height is ignored).</param>
        /// <param name="aiming">Aiming agents turn faster so they can keep up with a moving player.</param>
        void SetFacing(bool hasLookTarget, Vector3 lookTarget, bool aiming);
    }
}
