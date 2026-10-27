using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>A temporary, exclusive claim on a living enemy. Brains remain independent of the player.</summary>
    public interface ITakedownTarget
    {
        Transform Root { get; }
        Health Health { get; }
        Animator Animator { get; }
        bool Available { get; }
        bool TryReserve(Object owner);
        bool IsReservedBy(Object owner);
        void Release(Object owner);
    }

    /// <summary>Optional combat/weapon execution used by the frontal counterattack.</summary>
    public interface ITakedownCombatTarget : ITakedownTarget
    {
        bool IsFiring { get; }
        void PrepareFrontal(Object owner);
        void KickWeapon(Object owner, Vector3 direction);
    }
}
