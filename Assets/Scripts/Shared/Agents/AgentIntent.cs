// Owner: Agent Controller. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
// Created by the Systems Engineer in Y1; only the Agent Controller edits it from now on.
using UnityEngine;

namespace Blackout.Shared
{
    public enum MoveSpeed
    {
        Stop,
        Walk,
        Run
    }

    /// <summary>
    /// What a brain wants to do this tick. Brains only fill this in; the mover, the animator driver and the
    /// shooter carry it out. A struct, so returning one every tick allocates nothing.
    /// </summary>
    public struct AgentIntent
    {
        /// <summary>Where to go. Ignored when <see cref="moveSpeed"/> is Stop.</summary>
        public Vector3 moveTarget;
        public MoveSpeed moveSpeed;
        /// <summary>Where to face. Ignored when <see cref="hasLookTarget"/> is false (face the move direction).</summary>
        public Vector3 lookTarget;
        public bool hasLookTarget;
        public bool aim;
        public bool fire;

        /// <summary>Stand still, face forward, do not aim or fire.</summary>
        public static AgentIntent Idle => default;
    }
}
