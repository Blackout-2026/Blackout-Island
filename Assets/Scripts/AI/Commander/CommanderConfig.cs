// Owner: Agent Controller (Commander AI).
using System;
using UnityEngine;

namespace Blackout.AI.Commander
{
    /// <summary>Inspector representation of a bool fact, or the integer AgentsAlive fact.</summary>
    [Serializable]
    public sealed class CommanderFactValue
    {
        public CommanderFact fact;
        public bool booleanValue;
        public int integerValue;

        public CommanderFactValue() { }

        public CommanderFactValue(CommanderFact fact, bool value)
        {
            if (fact == CommanderFact.AgentsAlive)
                throw new ArgumentException("AgentsAlive requires an integer.", nameof(fact));
            this.fact = fact;
            booleanValue = value;
        }

        public CommanderFactValue(CommanderFact fact, int value)
        {
            if (fact != CommanderFact.AgentsAlive)
                throw new ArgumentException("Only AgentsAlive is an integer fact.", nameof(fact));
            this.fact = fact;
            integerValue = value;
        }

        internal void WriteTo(WorldState state)
        {
            string key = CommanderFacts.Key(fact);
            if (fact == CommanderFact.AgentsAlive)
                state.Set(key, integerValue);
            else
                state.Set(key, booleanValue);
        }
    }

    [Serializable]
    public sealed class CommanderActionSettings
    {
        [Min(0f)] public float cost = 1f;
        public CommanderFactValue[] preconditions = Array.Empty<CommanderFactValue>();
        public CommanderFactValue[] effects = Array.Empty<CommanderFactValue>();

        public GoapAction Build(string name)
        {
            return new GoapAction(name, ToState(preconditions), ToState(effects), cost);
        }

        private static WorldState ToState(CommanderFactValue[] facts)
        {
            if (facts == null)
                throw new ArgumentException("Action fact arrays must not be null.");

            var state = new WorldState();
            for (int i = 0; i < facts.Length; i++)
            {
                if (facts[i] == null)
                    throw new ArgumentException("Action facts must not contain null entries.");
                string key = CommanderFacts.Key(facts[i].fact);
                if (state.Values.ContainsKey(key))
                    throw new ArgumentException("Duplicate action fact: " + key);
                facts[i].WriteTo(state);
            }
            return state;
        }
    }

    /// <summary>
    /// Defaults from prototype/src/agents/commander_goap.js and reference section 8.
    /// Alert activation is handled by the cure event; these are the three G2 orders.
    /// Build actions once on initialization, not on every brain tick.
    /// </summary>
    [CreateAssetMenu(fileName = "CommanderConfig", menuName = "Blackout/Commander Config")]
    public sealed class CommanderConfig : ScriptableObject
    {
        public const string OrderConverge = "OrderConverge";
        public const string OrderSearchZone = "OrderSearchZone";
        public const string OrderRegroup = "OrderRegroup";

        [Header("GOAP orders: preconditions, predicted effects, cost")]
        public CommanderActionSettings orderRegroup = new CommanderActionSettings
        {
            cost = 2f,
            preconditions = new[] { new CommanderFactValue(CommanderFact.SquadAssembled, false) },
            effects = new[] { new CommanderFactValue(CommanderFact.SquadAssembled, true) }
        };
        public CommanderActionSettings orderSearchZone = new CommanderActionSettings
        {
            cost = 3f,
            preconditions = new[] { new CommanderFactValue(CommanderFact.PlayerLocated, false) },
            effects = new[] { new CommanderFactValue(CommanderFact.PlayerLocated, true) }
        };
        public CommanderActionSettings orderConverge = new CommanderActionSettings
        {
            cost = 2f,
            preconditions = new[]
            {
                new CommanderFactValue(CommanderFact.PlayerLocated, true),
                new CommanderFactValue(CommanderFact.SquadAssembled, true)
            },
            effects = new[] { new CommanderFactValue(CommanderFact.PlayerNeutralized, true) }
        };

        [Header("Observation")]
        [Min(0.1f)] public float sightingMemory = 6f;
        [Min(1)] public int minimumSquadSize = 2;

        [Header("Converge")]
        [Min(0.1f)] public float convergeInterval = 1f;

        [Header("Search zone")]
        [Min(0.1f)] public float searchInterval = 8f;
        [Min(0.1f)] public float searchTimeout = 24f;
        [Min(0f)] public float searchRingRadius = 7f;
        [Min(0f)] public float searchRadius = 9f;

        [Header("Regroup")]
        [Min(0.1f)] public float regroupTimeout = 12f;
        [Min(0f)] public float regroupRadius = 10f;

        [Header("Cure alert: multipliers, applied once")]
        [Min(1f)] public float alertSpeedMultiplier = 1.25f;
        [Min(1f)] public float alertVisionMultiplier = 1.4f;
        [Range(0f, 1f)] public float alertAimErrorMultiplier = 0.9f;

        public GoapAction[] BuildActions()
        {
            if (orderRegroup == null || orderSearchZone == null || orderConverge == null)
                throw new InvalidOperationException("All three Commander orders must be configured.");

            return new[]
            {
                orderRegroup.Build(OrderRegroup),
                orderSearchZone.Build(OrderSearchZone),
                orderConverge.Build(OrderConverge)
            };
        }
    }
}
