// Owner: Agent Controller.
using System;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// An agent's weapon stats. Defaults are the prototype's fallback values (docs/reference/agent-controller.md
    /// section 5: "Defaults for any weapon field not given"). Each agent type overrides these on its own
    /// prefab (for example the Hunter's damage 12, range 45) once A5's spawner differentiates them; until then
    /// every agent fires identically. Same pattern as MovementSettings: a plain serializable class, not a
    /// ScriptableObject, so it is tuned per-prefab in the Inspector.
    /// </summary>
    [Serializable]
    public class AgentWeaponConfig
    {
        [Header("Damage and range")]
        [Min(0f)] public float damage = 7f;
        [Min(0f)] public float range = 30f;

        [Header("Burst")]
        [Tooltip("Shots per burst before the pause.")]
        [Min(1)] public int burstCount = 3;
        [Tooltip("Seconds between shots within a burst.")]
        [Min(0.01f)] public float burstInterval = 0.11f;
        [Tooltip("Seconds between bursts (randomised 0.8 to 1.2 times this).")]
        [Min(0f)] public float burstPause = 0.9f;

        [Header("Aim")]
        [Tooltip("Radians. The base aim-error spread before distance/engagement scaling (ShotMath.AimError).")]
        [Min(0f)] public float spread = 0.035f;
        [Tooltip("Seconds after first wanting to fire before a shot is allowed.")]
        [Min(0f)] public float reactionDelay = 0.55f;
        [Tooltip("Minimum forward-dot-direction to fire: 0.85 is about a 31.8 degree half-cone.")]
        [Range(0f, 1f)] public float facingDotThreshold = 0.85f;

        [Header("Noise and tracer")]
        [Tooltip("Gunshot NoiseEvent loudness, published on every shot.")]
        [Min(0f)] public float noiseLoudness = 40f;
        [Tooltip("The ray starts this far in front of the muzzle, so the shooter's own geometry cannot self-block it.")]
        [Min(0f)] public float muzzleForwardOffset = 0.15f;
    }
}
