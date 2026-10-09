// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>What caused the damage, so receivers can react differently (hit-flash, fall camera shake, ...).</summary>
    public enum DamageType
    {
        Generic,
        Bullet,
        Melee,
        Fall
    }

    /// <summary>
    /// One hit. A struct, so passing it to TakeDamage and to OnDamaged allocates nothing.
    /// Field names match the contract: amount, point, direction, source, type.
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float amount;
        /// <summary>World position of the hit.</summary>
        public readonly Vector3 point;
        /// <summary>Direction the damage travels (attacker to victim), normalised or zero if unknown.</summary>
        public readonly Vector3 direction;
        /// <summary>The attacker, or null for the world (falls, debug).</summary>
        public readonly GameObject source;
        public readonly DamageType type;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject source, DamageType type)
        {
            this.amount = amount;
            this.point = point;
            this.direction = direction;
            this.source = source;
            this.type = type;
        }
    }
}
