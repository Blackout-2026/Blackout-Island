// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// What the animator layer reads from the mover: the agent's realised velocity, not the requested one, so
    /// the humanoid blend tree and the robot's procedural gait match what the agent is actually doing (still
    /// accelerating out of a stop, slowing into arrival) rather than snapping to a target speed. Not a shared
    /// contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): only the two animator implementations in this
    /// assembly read it, and both are owned by the Agent Controller, same as <see cref="IAgentFacing"/>.
    /// </summary>
    public interface IAgentVelocitySource
    {
        /// <summary>World-space velocity, flat (no Y component).</summary>
        Vector3 Velocity { get; }
        float Speed { get; }
    }
}
