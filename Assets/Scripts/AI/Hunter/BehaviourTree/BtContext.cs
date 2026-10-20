// Owner: Systems Engineer (Hunter AI).
using System;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// What every node of one tree can read while it ticks: the agent's <see cref="LocalBlackboard"/> and the tree's
    /// own clock. Nodes read <see cref="Time"/> instead of UnityEngine.Time, so EditMode tests can step it by exact
    /// amounts and the Hunter's timers pause with its tree (docs/reference/systems-engineer.md section 13).
    /// </summary>
    public sealed class BtContext
    {
        public BtContext() : this(new LocalBlackboard())
        {
        }

        public BtContext(LocalBlackboard blackboard)
        {
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
        }

        public LocalBlackboard Blackboard { get; }

        /// <summary>Seconds the tree has been ticked for.</summary>
        public float Time { get; private set; }

        /// <summary>Seconds since the previous tick.</summary>
        public float DeltaTime { get; private set; }

        /// <summary>Moves the clock forward. A negative or non-finite step counts as 0, so time never runs backwards.</summary>
        public void Advance(float deltaTime)
        {
            DeltaTime = deltaTime > 0f && !float.IsInfinity(deltaTime) ? deltaTime : 0f;
            Time += DeltaTime;
        }
    }
}
