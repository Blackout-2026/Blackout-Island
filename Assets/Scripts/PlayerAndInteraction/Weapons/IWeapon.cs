// Owner: Systems Engineer.
namespace Blackout.Weapons
{
    /// <summary>
    /// What WeaponController needs from any weapon. HitscanWeapon and MeleeWeapon implement it, so the
    /// controller handles switching, cooldown and recoil once and never checks which kind it holds.
    /// </summary>
    public interface IWeapon
    {
        WeaponData Data { get; }

        /// <summary>Called once by the controller with the shared aim, layers, effects and noise channel.</summary>
        void Initialize(WeaponContext context);

        /// <summary>Shows or hides the weapon (and cancels anything in progress when hidden).</summary>
        void SetEquipped(bool equipped);

        /// <summary>Called every frame while equipped (bloom recovery, delayed melee hits).</summary>
        void Tick(float dt);

        /// <summary>One shot or swing. The controller has already checked the cooldown and switching.</summary>
        void Fire();
    }
}
