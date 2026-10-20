// Owner: World Builder (Security Patrol AI).
namespace Blackout.AI.SecurityPatrol
{
    public enum SecurityPatrolState
    {
        Patrol,
        Investigate,
        Pursue,
        Attack,
        Search
    }

    /// <summary>Decision signals after the brain evaluates sensing, ranges and state timers.</summary>
    public enum SecurityPatrolTrigger
    {
        HeardNoise,
        DetectedPlayer,
        CanAttack,
        InvestigationComplete,
        LostPlayer,
        CannotAttack,
        SearchComplete,
        InvestigationAlerted
    }

    internal static class SecurityPatrolTransitions
    {
        private readonly struct Transition
        {
            public readonly SecurityPatrolState From;
            public readonly SecurityPatrolTrigger Trigger;
            public readonly SecurityPatrolState To;

            public Transition(SecurityPatrolState from, SecurityPatrolTrigger trigger, SecurityPatrolState to)
            {
                From = from;
                Trigger = trigger;
                To = to;
            }
        }

        // The only transition table. States return intents; they never switch states themselves.
        // The brain evaluates conditions/timers before requesting these transitions.
        private static readonly Transition[] Table =
        {
            new Transition(SecurityPatrolState.Patrol, SecurityPatrolTrigger.HeardNoise, SecurityPatrolState.Investigate),
            new Transition(SecurityPatrolState.Patrol, SecurityPatrolTrigger.DetectedPlayer, SecurityPatrolState.Pursue),
            new Transition(SecurityPatrolState.Patrol, SecurityPatrolTrigger.CanAttack, SecurityPatrolState.Attack),
            new Transition(SecurityPatrolState.Investigate, SecurityPatrolTrigger.DetectedPlayer, SecurityPatrolState.Pursue),
            new Transition(SecurityPatrolState.Investigate, SecurityPatrolTrigger.CanAttack, SecurityPatrolState.Attack),
            new Transition(SecurityPatrolState.Investigate, SecurityPatrolTrigger.InvestigationComplete, SecurityPatrolState.Patrol),
            new Transition(SecurityPatrolState.Investigate, SecurityPatrolTrigger.InvestigationAlerted, SecurityPatrolState.Search),
            new Transition(SecurityPatrolState.Pursue, SecurityPatrolTrigger.CanAttack, SecurityPatrolState.Attack),
            new Transition(SecurityPatrolState.Pursue, SecurityPatrolTrigger.LostPlayer, SecurityPatrolState.Search),
            new Transition(SecurityPatrolState.Attack, SecurityPatrolTrigger.CannotAttack, SecurityPatrolState.Pursue),
            new Transition(SecurityPatrolState.Search, SecurityPatrolTrigger.DetectedPlayer, SecurityPatrolState.Pursue),
            new Transition(SecurityPatrolState.Search, SecurityPatrolTrigger.CanAttack, SecurityPatrolState.Attack),
            new Transition(SecurityPatrolState.Search, SecurityPatrolTrigger.SearchComplete, SecurityPatrolState.Patrol)
        };

        public static bool TryGetNext(SecurityPatrolState from, SecurityPatrolTrigger trigger,
            out SecurityPatrolState next)
        {
            for (int i = 0; i < Table.Length; i++)
            {
                Transition transition = Table[i];
                if (transition.From == from && transition.Trigger == trigger)
                {
                    next = transition.To;
                    return true;
                }
            }

            next = from;
            return false;
        }
    }
}
