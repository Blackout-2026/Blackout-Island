// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>
    /// A door agents can open on their way through (prototype: agent-controller.md 3.2, "door awareness").
    /// The Systems Engineer's doors implement it; the Agent Controller's movement only talks to this interface,
    /// for example by finding <c>IDoor</c> components once at start and checking the ones ahead of the agent.
    /// </summary>
    public interface IDoor
    {
        /// <summary>Centre of the doorway at floor level.</summary>
        Vector3 Position { get; }

        /// <summary>True once the door is open far enough to walk through.</summary>
        bool IsOpen { get; }

        bool IsLocked { get; }

        /// <summary>
        /// Opens the door if it is unlocked and not already opening. Returns false if it is locked or already open.
        /// <paramref name="by"/> is who opened it (used as the door noise's source).
        /// </summary>
        bool RequestOpen(GameObject by);
    }
}
