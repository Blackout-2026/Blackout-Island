// Owner: Core Developer (Tactical Guard AI).
using System;
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>The inspector form of a <see cref="Consideration"/>'s response curve (the input value is supplied at run time).</summary>
    [Serializable]
    public sealed class ConsiderationSettings
    {
        public ResponseCurve curve = ResponseCurve.Linear;
        [Min(0f)] public float steepness = 10f;
        [Range(0f, 1f)] public float midpoint = 0.5f;
        [Range(0f, 1f)] public float floor;

        public ConsiderationSettings() { }

        public ConsiderationSettings(ResponseCurve curve, float steepness = 10f, float midpoint = 0.5f, float floor = 0f)
        {
            this.curve = curve;
            this.steepness = steepness;
            this.midpoint = midpoint;
            this.floor = floor;
        }

        public void CopyTo(Consideration target)
        {
            target.responseCurve = curve;
            target.steepness = steepness;
            target.midpoint = midpoint;
            target.floor = floor;
        }
    }

    /// <summary>
    /// Every number the Tactical Guard's decisions use, so tuning never touches code. Defaults are the prototype's
    /// (agents/tactical_guard_utility.js) where it has the number, and are tuned for the multiplicative scoring otherwise.
    /// </summary>
    [CreateAssetMenu(fileName = "TacticalGuardConfig", menuName = "Blackout/Tactical Guard Config")]
    public sealed class TacticalGuardConfig : ScriptableObject
    {
        [Header("Ranges (metres)")]
        [Tooltip("Distance at which the attack proximity input reaches 0. The prototype's range curve ends at 36 m.")]
        [Min(1f)] public float attackRange = 36f;
        [Tooltip("The guard cannot see the player beyond this.")]
        [Min(1f)] public float sightRange = 36f;
        [Tooltip("Distance from the post that counts as fully away (input 1). The prototype divides by 12 m.")]
        [Min(1f)] public float postRadius = 12f;
        [Tooltip("How far from the post a reposition target may be. The prototype uses 15 m.")]
        [Min(1f)] public float repositionLimit = 15f;
        [Tooltip("Same, while the Blackboard alert is raised. The prototype uses 30 m.")]
        [Min(1f)] public float repositionLimitAlert = 30f;

        [Header("Cover search (metres)")]
        [Tooltip("Cover points further than this from the guard are ignored. The prototype uses 14 m.")]
        [Min(1f)] public float coverSearchRadius = 14f;
        [Tooltip("Cover points further than this from the post are ignored, so cover never drags a guard off its post.")]
        [Min(1f)] public float coverPostRadius = 20f;
        [Tooltip("Cover closer than this to the player scores lower; at or beyond it the player-distance factor is 1.")]
        [Min(1f)] public float coverStandoff = 8f;

        [Header("Arrival distances (metres)")]
        [Min(0.1f)] public float coverArriveDistance = 1f;
        [Min(0.1f)] public float postArriveDistance = 1.2f;
        [Min(0.1f)] public float repositionArriveDistance = 1.2f;

        [Header("Heights (metres above the feet)")]
        [Tooltip("Where the guard's sight ray starts. The prototype and the Agent Controller's guard prefab both use 1.95 m (the robot is tall).")]
        [Min(0f)] public float guardEyeHeight = 1.95f;
        [Min(0f)] public float playerEyeHeight = 1.6f;
        [Tooltip("Height of a crouching guard's head; a cover point must hide this from the player's eye.")]
        [Min(0f)] public float coverHeadHeight = 1f;
        [Tooltip("Where the guard aims on the player.")]
        [Min(0f)] public float playerChestHeight = 1.2f;

        [Header("Memory (seconds)")]
        [Tooltip("Knowledge of the player fades linearly to 0 over this time. The prototype uses 10 s.")]
        [Min(0.1f)] public float knowledgeWindow = 10f;
        [Tooltip("Damage counts as recent for this long, fading linearly. The prototype uses 3 s.")]
        [Min(0.1f)] public float damageWindow = 3f;

        [Header("Threat = sum of these, clamped to 0..1")]
        [Range(0f, 1f)] public float threatVisible = 0.4f;
        [Tooltip("Scaled by how recent the damage is. Higher than the prototype's 0.5 because it fades.")]
        [Range(0f, 1f)] public float threatRecentDamage = 0.7f;
        [Tooltip("Player is visible and looking at the guard (camera forward dot direction to guard above Aimed Dot).")]
        [Range(0f, 1f)] public float threatAimedAt = 0.25f;
        [Range(0f, 1f)] public float threatAlert = 0.15f;
        [Range(0.5f, 1f)] public float aimedDot = 0.97f;

        [Header("Selection")]
        [Tooltip("Bonus added to the current action's score so it does not flicker. The prototype uses 0.1.")]
        [Min(0f)] public float hysteresis = 0.1f;

        [Header("Hold post: head scan")]
        [Min(0f)] public float scanRadius = 6f;
        [Min(0f)] public float scanHeight = 1.6f;
        [Tooltip("Radians per second of the scan angle (the prototype uses 0.5).")]
        [Min(0f)] public float scanSpeed = 0.5f;

        [Header("Action weights (the ceiling of each action's score)")]
        [Min(0f)] public float attackWeight = 1f;
        [Min(0f)] public float moveToCoverWeight = 1f;
        [Min(0f)] public float repositionWeight = 0.6f;
        [Min(0f)] public float holdPostWeight = 0.8f;

        [Header("Attack considerations")]
        [Tooltip("Input: 1 when the guard can see the player, else 0.")]
        public ConsiderationSettings attackLineOfSight = new ConsiderationSettings(ResponseCurve.Linear);
        [Tooltip("Input: 1 - distance / Attack Range. The logistic drops the score between about 20 m and 35 m.")]
        public ConsiderationSettings attackProximity = new ConsiderationSettings(ResponseCurve.Logistic, 12f, 0.25f);
        [Tooltip("Input: health fraction. A low floor means a nearly dead guard barely attacks.")]
        public ConsiderationSettings attackHealth = new ConsiderationSettings(ResponseCurve.Linear, floor: 0.1f);

        [Header("Move to cover considerations")]
        [Tooltip("Input: quality of the best unclaimed cover point (0 = none).")]
        public ConsiderationSettings coverAvailability = new ConsiderationSettings(ResponseCurve.Linear);
        [Tooltip("Input: threat level.")]
        public ConsiderationSettings coverThreat = new ConsiderationSettings(ResponseCurve.Linear);

        [Header("Reposition considerations")]
        [Tooltip("Input: how fresh the guard's knowledge of the player is.")]
        public ConsiderationSettings repositionKnowledge = new ConsiderationSettings(ResponseCurve.Linear);
        [Tooltip("Input: how well the guard can already engage (visible x attack proximity). Inverse: not engaged yet = reposition.")]
        public ConsiderationSettings repositionDisengaged = new ConsiderationSettings(ResponseCurve.Inverse);

        [Header("Hold post considerations")]
        [Tooltip("Input: distance from the post / Post Radius. The floor is the pull of the post when standing on it.")]
        public ConsiderationSettings holdPostPull = new ConsiderationSettings(ResponseCurve.Linear, floor: 0.3f);
        [Tooltip("Input: knowledge freshness. Inverse: the less the guard knows, the more it stays put.")]
        public ConsiderationSettings holdCalm = new ConsiderationSettings(ResponseCurve.Inverse, floor: 0.4f);
    }
}
