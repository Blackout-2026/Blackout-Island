// Owner: Agent Controller (Commander AI).
using System;

namespace Blackout.AI.Commander
{
    /// <summary>A state transition the planner may use when its preconditions hold.</summary>
    public sealed class GoapAction
    {
        private readonly WorldState preconditions;
        private readonly WorldState effects;

        public GoapAction(string name, WorldState preconditions, WorldState effects, float cost = 1f)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("An action must have a name.", nameof(name));
            if (preconditions == null)
                throw new ArgumentNullException(nameof(preconditions));
            if (effects == null)
                throw new ArgumentNullException(nameof(effects));
            if (float.IsNaN(cost) || float.IsInfinity(cost) || cost < 0f)
                throw new ArgumentOutOfRangeException(nameof(cost), "Action cost must be finite and non-negative.");

            Name = name;
            this.preconditions = preconditions.Clone();
            this.effects = effects.Clone();
            Cost = cost;
        }

        public string Name { get; }

        public float Cost { get; }

        public WorldState Preconditions => preconditions.Clone();

        public WorldState Effects => effects.Clone();

        public bool IsApplicable(WorldState state)
        {
            return state != null && state.Satisfies(preconditions);
        }

        internal WorldState Apply(WorldState state)
        {
            return state.WithEffects(effects);
        }
    }
}
