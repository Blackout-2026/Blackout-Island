// Owner: Systems Engineer (Hunter AI).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// The keys of the Hunter tree's <see cref="LocalBlackboard"/> (docs/reference/systems-engineer.md section 15).
    /// The brain writes the senses every think tick; the tree reads them and writes only the "handled" times, so a
    /// test can drive every branch by filling a blackboard by hand. Times are on the brain's clock
    /// (<see cref="Now"/>), not the tree's.
    /// </summary>
    public static class HunterFacts
    {
        // Written by the brain every think tick.
        public static readonly BlackboardKey<float> Now = new BlackboardKey<float>("Now");
        public static readonly BlackboardKey<Vector3> Self = new BlackboardKey<Vector3>("Self");
        public static readonly BlackboardKey<Vector3> PlayerPosition = new BlackboardKey<Vector3>("PlayerPosition");
        public static readonly BlackboardKey<bool> CanSeePlayer = new BlackboardKey<bool>("CanSeePlayer");
        public static readonly BlackboardKey<bool> PlayerDetected = new BlackboardKey<bool>("PlayerDetected");
        /// <summary>Negative infinity (or unset) means never seen.</summary>
        public static readonly BlackboardKey<float> LastSeenTime = new BlackboardKey<float>("LastSeenTime");
        public static readonly BlackboardKey<Vector3> LastSeenPosition = new BlackboardKey<Vector3>("LastSeenPosition");
        /// <summary>The Commander's alert vision multiplier; it stretches the attack range too. 1 when calm.</summary>
        public static readonly BlackboardKey<float> RangeMultiplier = new BlackboardKey<float>("RangeMultiplier");
        public static readonly BlackboardKey<bool> HasNoise = new BlackboardKey<bool>("HasNoise");
        public static readonly BlackboardKey<NoiseType> HeardNoiseType = new BlackboardKey<NoiseType>("HeardNoiseType");
        public static readonly BlackboardKey<Vector3> NoisePosition = new BlackboardKey<Vector3>("NoisePosition");
        /// <summary>How loud the noise was when heard (after walls, before fading).</summary>
        public static readonly BlackboardKey<float> NoiseLoudness = new BlackboardKey<float>("NoiseLoudness");
        public static readonly BlackboardKey<float> NoiseHeardAt = new BlackboardKey<float>("NoiseHeardAt");
        public static readonly BlackboardKey<bool> HasOrder = new BlackboardKey<bool>("HasOrder");
        public static readonly BlackboardKey<Directive> Order = new BlackboardKey<Directive>("Order");
        public static readonly BlackboardKey<float> OrderReceivedAt = new BlackboardKey<float>("OrderReceivedAt");

        // Written by the tree when it has finished with something, so it is not done twice.
        public static readonly BlackboardKey<float> NoiseHandledAt = new BlackboardKey<float>("NoiseHandledAt");
        public static readonly BlackboardKey<float> SightingHandledAt = new BlackboardKey<float>("SightingHandledAt");
        public static readonly BlackboardKey<float> OrderHandledAt = new BlackboardKey<float>("OrderHandledAt");
    }
}
