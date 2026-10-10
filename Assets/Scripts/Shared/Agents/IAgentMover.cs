// Owner: Agent Controller. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
// Created by the Systems Engineer in Y1; only the Agent Controller edits it from now on.
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>Moves an agent along a NavMesh path. The brain never calls it; the agent root applies the intent through it.</summary>
    public interface IAgentMover
    {
        void SetDestination(Vector3 destination, MoveSpeed speed);
        void Stop();
        bool HasArrived { get; }
        float RemainingDistance { get; }
        /// <summary>Path corners from the agent to the destination. Read only: do not modify the array.</summary>
        Vector3[] CurrentPath { get; }
    }
}
