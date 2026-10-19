// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// One pellet's ray and what the weapon's raycast hit, reported through <see cref="WeaponContext.PelletTraced"/>
    /// so the Stress Test can check for missed hits (docs/reference/systems-engineer.md section 12). A struct, so
    /// reporting allocates nothing.
    /// </summary>
    public readonly struct PelletTrace
    {
        public PelletTrace(Ray ray, float range, Collider hit, float distance)
        {
            Ray = ray;
            Range = range;
            Hit = hit;
            Distance = distance;
        }

        public Ray Ray { get; }
        public float Range { get; }
        /// <summary>The first collider in the weapon's mask along the ray, or null if nothing was in range.</summary>
        public Collider Hit { get; }
        /// <summary>Distance to <see cref="Hit"/>, or the range if nothing was hit.</summary>
        public float Distance { get; }
    }
}
