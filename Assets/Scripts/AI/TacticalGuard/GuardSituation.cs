// Owner: Core Developer (Tactical Guard AI).
namespace Blackout.AI.TacticalGuard
{
    /// <summary>The four things the guard can decide to do. The values index the scorer's score array.</summary>
    public enum GuardActionType
    {
        Attack = 0,
        MoveToCover = 1,
        Reposition = 2,
        HoldPost = 3
    }

    /// <summary>
    /// Everything the scorer needs, already measured. The brain fills it from the world; tests fill it with fake
    /// numbers, which is what lets each action be tested without a scene.
    /// </summary>
    public struct GuardSituation
    {
        /// <summary>Line of sight to the player right now.</summary>
        public bool canSeePlayer;
        public float distanceToPlayer;
        /// <summary>0..1: 1 = seen this moment, fading to 0 over the knowledge window (0 = never seen or too old).</summary>
        public float knowledge;
        /// <summary>0..1, see <see cref="GuardMath.Threat"/>.</summary>
        public float threat;
        /// <summary>0..1, current health / max health.</summary>
        public float healthFraction;
        public float distanceFromPost;
        /// <summary>0..1 quality of the best unclaimed cover point, 0 when there is none.</summary>
        public float coverQuality;
    }
}
