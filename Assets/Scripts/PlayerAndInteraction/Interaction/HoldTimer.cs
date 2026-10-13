// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Interaction
{
    /// <summary>
    /// Hold-to-interact timing, as in the prototype: holding adds dt, releasing resets to 0, and the action fires
    /// once when the hold time is reached. It then waits for E to be released before it can start again, so one
    /// long hold never fires twice. Pure C#, so it is tested without a scene.
    /// </summary>
    public sealed class HoldTimer
    {
        private bool waitingForRelease;

        /// <summary>Seconds held so far.</summary>
        public float Elapsed { get; private set; }

        /// <summary>Advances the timer. Returns true on the frame the hold completes.</summary>
        public bool Update(bool held, float holdTime, float dt)
        {
            if (!held)
            {
                Elapsed = 0f;
                waitingForRelease = false;
                return false;
            }

            if (waitingForRelease)
            {
                return false;
            }

            Elapsed += dt;
            if (Elapsed < holdTime)
            {
                return false;
            }

            Elapsed = 0f;
            waitingForRelease = true;
            return true;
        }

        /// <summary>Starts again from 0, for example when the player looks at a different interactable.</summary>
        public void Reset()
        {
            Elapsed = 0f;
        }

        /// <summary>Progress 0..1 for the HUD ring.</summary>
        public float Fraction(float holdTime)
        {
            return holdTime > 0f ? Mathf.Clamp01(Elapsed / holdTime) : 0f;
        }
    }
}
