// Owner: World Builder (Security Patrol AI).
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>One think tick's sensing result. Tests supply values without Transforms or physics queries.</summary>
    public struct PatrolPerceptionInput
    {
        public bool canSeePlayer;
        /// <summary>Player feet/root position; Attack adds the configured chest height for its look target.</summary>
        public Vector3 playerPosition;
        public bool heardNoise;
        public Vector3 noisePosition;
    }
}
