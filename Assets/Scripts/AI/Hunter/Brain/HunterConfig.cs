// Owner: Systems Engineer (Hunter AI).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// Every number the Hunter's eyes and tree use (docs/reference/systems-engineer.md section 15). Values are the
    /// prototype's (agents/hunter_bt.js, agents/perception.js) unless the tooltip says [GUESS]. Hearing has its own
    /// <see cref="HearingConfig"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "HunterConfig", menuName = "Blackout/Hunter/Hunter Config")]
    public sealed class HunterConfig : ScriptableObject
    {
        /// <summary>Level (walls, props) and Interactable (doors): what blocks sight.</summary>
        public const int DefaultSightBlockers = 1 << PhysicsLayers.Level | 1 << PhysicsLayers.Interactable;

        [Header("Vision")]
        [Tooltip("Full view cone in degrees. The prototype's Hunter has 2.0 rad (115 degrees).")]
        [Range(10f, 360f)] public float fieldOfViewDegrees = 114.6f;
        [Tooltip("Sight range in metres (25 for the Hunter). The Commander's alert multiplies it (x1.4).")]
        [Min(1f)] public float sightRange = 25f;
        [Tooltip("Within this distance the Hunter notices the player in any direction.")]
        [Min(0f)] public float closeSenseRange = 2.2f;
        [Tooltip("The Hunter's eyes, above its feet.")]
        [Min(0f)] public float eyeHeight = 1.6f;
        [Tooltip("The player's eyes above their feet (height 1.75 - 0.12); the first sight ray aims here.")]
        [Min(0f)] public float playerHeadHeight = 1.63f;
        [Tooltip("The second sight ray, if the head is hidden: 0.55 x the player's 1.75 m height.")]
        [Min(0f)] public float playerChestHeight = 0.96f;
        [Tooltip("Layers that block sight: Level (walls) and Interactable (doors). Triggers never block.")]
        public LayerMask sightBlockers = DefaultSightBlockers;

        [Header("Awareness: += rate x max(minNear, nearOffset - d / range) x visibility x (detected ? 3 : 1) x dt")]
        [Min(0f)] public float awarenessRate = 2.2f;
        [Tooltip("Far away, awareness still fills at least this fraction of the rate.")]
        [Min(0f)] public float minNearFactor = 0.15f;
        [Tooltip("Up close the distance factor is this minus distance / range, so it can exceed 1.")]
        [Min(0f)] public float nearOffset = 1.25f;
        [Tooltip("Once detected, awareness fills this much faster.")]
        [Min(1f)] public float detectedRateMultiplier = 3f;
        [Min(1f)] public float awarenessCap = 1.5f;
        [Tooltip("Awareness at which the player is detected.")]
        [Min(0.01f)] public float detectThreshold = 1f;
        [Tooltip("Per second while the player is not seen.")]
        [Min(0f)] public float awarenessDecay = 0.35f;
        [Tooltip("Per second while not seen but still detected.")]
        [Min(0f)] public float awarenessDecayDetected = 0.25f;
        [Tooltip("Per second while the player is dead.")]
        [Min(0f)] public float awarenessDecayPlayerDead = 1f;
        [Tooltip("Detection is lost this long after the player was last seen.")]
        [Min(0f)] public float loseDetectionSeconds = 6f;

        [Header("Player visibility (from the player's speed) [GUESS]")]
        [Tooltip("[GUESS: the playbook's camouflage] Awareness fills this much as fast while the player stands still: " +
                 "a motionless player blends into the forest.")]
        [Range(0f, 1f)] public float camouflageMultiplier = 0.5f;
        [Tooltip("[GUESS] Below this speed (m/s) the player counts as standing still.")]
        [Min(0f)] public float stillSpeed = 0.3f;
        [Tooltip("[GUESS] Above this speed (m/s) the player counts as sprinting (walk 4.3, sprint 7.0).")]
        [Min(0f)] public float sprintSpeed = 5.5f;
        [Tooltip("A sprinting player is easier to see (the prototype's x1.25).")]
        [Min(0f)] public float sprintVisibility = 1.25f;

        [Header("Attack and Pursue")]
        [Tooltip("Engage needs the player seen within this many seconds.")]
        [Min(0f)] public float seenRecentlySeconds = 0.6f;
        [Tooltip("Fires within this distance (32 m in the prototype; the alert multiplies it too).")]
        [Min(0f)] public float attackRange = 32f;
        [Tooltip("Pursue stops this far from the player.")]
        [Min(0f)] public float closeInDistance = 6f;
        [Tooltip("Where the Hunter aims: 0.6 x the player's height.")]
        [Min(0f)] public float aimHeight = 1.05f;

        [Header("Search (last known position)")]
        [Min(0f)] public float searchMemorySeconds = 10f;
        [Min(0f)] public float searchArriveDistance = 1.8f;
        [Min(0f)] public float searchLookAroundSeconds = 3f;

        [Header("Investigate (footsteps, doors, crate, melee, sprinting)")]
        [Min(0f)] public float preyNoiseSeconds = 5f;
        [Tooltip("Heard loudness (after walls) needed to investigate.")]
        [Min(0f)] public float preyNoiseThreshold = 0.04f;
        [Min(0f)] public float investigateArriveDistance = 3f;
        [Tooltip("After a footstep: wait this long aiming at the spot.")]
        [Min(0f)] public float ambushSeconds = 4f;
        [Tooltip("[GUESS] After sprinting: the prey is moving, so a shorter ambush.")]
        [Min(0f)] public float sprintAmbushSeconds = 2f;

        [Header("Cautious approach (gunfire, generator, alarm)")]
        [Min(0f)] public float gunfireSeconds = 6f;
        [Min(0f)] public float gunfireThreshold = 0.06f;
        [Tooltip("Closer than this, take cover instead of advancing.")]
        [Min(0f)] public float gunfireCloseDistance = 16f;
        [Tooltip("Cover is looked for within this distance of the Hunter.")]
        [Min(0f)] public float coverSearchRadius = 12f;
        [Tooltip("The cover point is this far behind the tree or marker, away from the threat.")]
        [Min(0f)] public float coverOffset = 0.9f;
        [Min(0f)] public float holdCoverSeconds = 2.5f;
        [Tooltip("[GUESS] Next cover on the advance: within this distance of the Hunter...")]
        [Min(0f)] public float advanceCoverRange = 15f;
        [Tooltip("[GUESS] ...and at least this much nearer the noise.")]
        [Min(0f)] public float advanceMinGain = 4f;
        [Tooltip("[GUESS] Wait at each cover on the advance.")]
        [Min(0f)] public float advancePauseSeconds = 1.5f;
        [Tooltip("[GUESS] The advance ends this far from the noise.")]
        [Min(0f)] public float advanceStopDistance = 8f;

        [Header("Commander orders")]
        [Min(0f)] public float orderArriveDistance = 2.5f;
        [Tooltip("[GUESS] A search-zone order visits this many points inside the radius, then completes.")]
        [Min(1)] public int searchZonePoints = 3;

        [Header("Roam (forest waypoints)")]
        [Min(0f)] public float waypointArriveDistance = 2f;
        [Min(0f)] public float lookAroundMinSeconds = 2f;
        [Min(0f)] public float lookAroundMaxSeconds = 4f;

        [Header("Movement")]
        [Tooltip("A move gives up after this long, as in the prototype.")]
        [Min(1f)] public float moveTimeoutSeconds = 25f;
    }
}
