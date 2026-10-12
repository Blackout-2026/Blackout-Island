// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// Time until the next shot. One cooldown for the whole weapon system, as in the prototype, so switching
    /// weapons never lets the player fire early.
    /// </summary>
    public sealed class WeaponCooldown
    {
        public float Remaining { get; private set; }
        public bool IsReady => Remaining <= 0f;

        public void Start(float seconds)
        {
            Remaining = Mathf.Max(0f, seconds);
        }

        public void Tick(float dt)
        {
            Remaining = Mathf.Max(0f, Remaining - dt);
        }
    }
}
