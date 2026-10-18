// Owner: Agent Controller (Commander AI).
using System;

namespace Blackout.AI.Commander
{
    /// <summary>A desired world state and its relative importance to the planner.</summary>
    public sealed class GoapGoal
    {
        private readonly WorldState desiredState;

        public GoapGoal(WorldState desiredState, int priority = 0)
        {
            if (desiredState == null)
                throw new ArgumentNullException(nameof(desiredState));

            this.desiredState = desiredState.Clone();
            Priority = priority;
        }

        public int Priority { get; }

        public WorldState DesiredState => desiredState.Clone();

        public bool IsSatisfiedBy(WorldState state)
        {
            return state != null && state.Satisfies(desiredState);
        }
    }
}
