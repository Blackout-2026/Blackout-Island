// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// The time between dying and respawning, as pure C# so it is tested without a scene. The camera falls over
    /// during the first 0.8 s; the respawn is due after the delay (prototype: 2.2 s). No game-over screen.
    /// </summary>
    public sealed class DeathSequence
    {
        /// <summary>Seconds for the fall-over camera to finish.</summary>
        public const float FallTime = 0.8f;

        private readonly float respawnDelay;

        public DeathSequence(float respawnDelay)
        {
            this.respawnDelay = Mathf.Max(0f, respawnDelay);
        }

        public bool IsDead { get; private set; }
        /// <summary>Seconds since death.</summary>
        public float Elapsed { get; private set; }
        /// <summary>0 while alive, then 0 → 1 over <see cref="FallTime"/>.</summary>
        public float FallProgress => IsDead ? Mathf.Clamp01(Elapsed / FallTime) : 0f;
        public float TimeUntilRespawn => IsDead ? Mathf.Max(0f, respawnDelay - Elapsed) : 0f;

        public void Begin()
        {
            IsDead = true;
            Elapsed = 0f;
        }

        /// <summary>Advances the timer. Returns true once, on the frame the respawn is due; the sequence then ends.</summary>
        public bool Tick(float dt)
        {
            if (!IsDead)
            {
                return false;
            }

            Elapsed += dt;
            if (Elapsed < respawnDelay)
            {
                return false;
            }

            IsDead = false;
            return true;
        }
    }
}
