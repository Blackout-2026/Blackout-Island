// Owner: Agent Controller (Commander AI).
using System;

namespace Blackout.AI.Commander
{
    public enum CommanderFact
    {
        PlayerLocated,
        CureTaken,
        GeneratorDisabled,
        AgentsAlive,
        SquadAssembled,
        PlayerNeutralized
    }

    /// <summary>The G2 vocabulary shared by Blackboard observation and the action config.</summary>
    public static class CommanderFacts
    {
        public const string PlayerLocated = "playerLocated";
        public const string CureTaken = "cureTaken";
        public const string GeneratorDisabled = "generatorDisabled";
        public const string AgentsAlive = "agentsAlive";
        public const string SquadAssembled = "squadAssembled";
        public const string PlayerNeutralized = "playerNeutralized";

        public static string Key(CommanderFact fact)
        {
            switch (fact)
            {
                case CommanderFact.PlayerLocated: return PlayerLocated;
                case CommanderFact.CureTaken: return CureTaken;
                case CommanderFact.GeneratorDisabled: return GeneratorDisabled;
                case CommanderFact.AgentsAlive: return AgentsAlive;
                case CommanderFact.SquadAssembled: return SquadAssembled;
                case CommanderFact.PlayerNeutralized: return PlayerNeutralized;
                default: throw new ArgumentOutOfRangeException(nameof(fact));
            }
        }
    }
}
