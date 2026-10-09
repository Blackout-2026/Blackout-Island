// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>What the registry needs to know about an agent. Implemented by AgentRoot.</summary>
    public interface IAgentInfo
    {
        /// <summary>Unique name such as "Patrol-1"; it is the key on the Blackboard and the Owner of its markers.</summary>
        string AgentName { get; }
        Transform Transform { get; }
        bool IsAlive { get; }
    }
}
