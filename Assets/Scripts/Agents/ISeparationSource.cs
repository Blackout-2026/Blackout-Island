// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// What a mover needs to steer around other agents and the player (A4, docs/reference/agent-controller.md
    /// section 3.2). A mover with none configured simply never separates — used in tests, and safe before an
    /// AgentRegistry exists in the loaded scenes.
    /// </summary>
    public interface ISeparationSource
    {
        /// <summary>
        /// The combined separation push (world X/Z) for an agent at <paramref name="position"/>, added
        /// straight into the mover's desired velocity. <paramref name="self"/> is excluded from the agents
        /// considered (compare by reference, not position, so a destroyed/moved agent cannot push itself).
        /// </summary>
        Vector2 ComputeSeparation(Vector3 position, Transform self);
    }
}
