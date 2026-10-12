// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Weapons
{
    public enum WeaponKind
    {
        Hitscan,
        Melee
    }

    /// <summary>
    /// Every number of one weapon, so the SMG, rifle and knife share the same code and differ only by data
    /// (the prototype's WEAPONS table). Defaults are the SMG's (docs/reference/systems-engineer.md section 7).
    /// </summary>
    [CreateAssetMenu(menuName = "Blackout/Weapons/Weapon Data", fileName = "WeaponData")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "MP-9 SMG";
        [Tooltip("Number key that selects this weapon.")]
        [Range(1, 3)] public int slot = 1;
        public WeaponKind kind = WeaponKind.Hitscan;

        [Header("Damage")]
        [Tooltip("Damage per pellet before falloff.")]
        [Min(0f)] public float damage = 15f;
        [Tooltip("Rays per shot. 1 for every prototype weapon.")]
        [Min(1)] public int pellets = 1;
        [Tooltip("Metres. For melee: the reach.")]
        [Min(0f)] public float range = 90f;
        [Tooltip("Full damage up to this distance (m).")]
        [Min(0f)] public float falloffStart = 10f;
        [Tooltip("Damage reaches the minimum multiplier at this distance (m).")]
        [Min(0f)] public float falloffEnd = 32f;
        [Range(0f, 1f)] public float falloffMinMultiplier = 0.45f;

        [Header("Fire rate")]
        [Tooltip("Seconds between shots (60 / rounds per minute). SMG 720 rpm = 0.083 s.")]
        [Min(0f)] public float cooldown = 60f / 720f;
        [Tooltip("Hold to keep firing. Off = one shot per click.")]
        public bool automatic = true;

        [Header("Spread (degrees)")]
        [Tooltip("Cone half-angle when standing still.")]
        [Min(0f)] public float spread = 1.38f;
        [Tooltip("Added at walk speed and above, scaled down when slower.")]
        [Min(0f)] public float moveSpread = 1.15f;
        [Tooltip("Added per shot while firing continuously.")]
        [Min(0f)] public float bloomPerShot = 0.23f;
        [Min(0f)] public float bloomMax = 1.26f;

        [Header("Recoil")]
        [Tooltip("Camera kick up per shot (degrees). It recovers, so the aim returns.")]
        [Min(0f)] public float recoilPitch = 0.49f;
        [Tooltip("Random sideways camera kick, ± this many degrees.")]
        [Min(0f)] public float recoilYaw = 0.29f;
        [Tooltip("Viewmodel push back towards the camera (m).")]
        [Min(0f)] public float kickBack = 0.035f;
        [Tooltip("Viewmodel muzzle climb (degrees).")]
        [Min(0f)] public float kickRotate = 2.3f;

        [Header("Melee")]
        [Tooltip("Half-angle of the cone in front of the player that a swing can hit (cos 0.62 = 51.7°).")]
        [Range(0f, 180f)] public float meleeConeAngle = 51.7f;
        [Tooltip("Seconds from the start of the swing until the blade lands.")]
        [Min(0f)] public float meleeHitDelay = 0.11f;

        [Header("Switching")]
        [Tooltip("Seconds to lower the old weapon and raise this one.")]
        [Min(0f)] public float switchTime = 0.4f;

        [Header("Feedback")]
        [Tooltip("Noise radius (m) published to the AI's hearing per shot or swing.")]
        [Min(0f)] public float noiseRadius = 42f;
        [Tooltip("Draw a tracer every Nth shot. 0 = never.")]
        [Min(0)] public int tracerEvery = 2;
        [Tooltip("Muzzle flash size and light strength. 0 = no flash.")]
        [Min(0f)] public float muzzleFlashScale = 0.75f;
    }
}
