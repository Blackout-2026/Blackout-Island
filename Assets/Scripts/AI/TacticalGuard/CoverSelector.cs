// Owner: Core Developer (Tactical Guard AI).
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>
    /// Finds the best unclaimed CoverPoint that hides the guard from the player. A point scores 0..1 as the
    /// geometric mean of three factors: near the guard (1 - d / search radius), away from the player
    /// (d / standoff, capped at 1) and near the post (1 - d / cover post radius). Any factor at 0 rules it out.
    /// </summary>
    public sealed class CoverSelector
    {
        private readonly TacticalGuardConfig config;
        private readonly IGuardSensor sensor;
        private readonly CoverClaims claims;

        public CoverSelector(TacticalGuardConfig config, IGuardSensor sensor, CoverClaims claims)
        {
            this.config = config;
            this.sensor = sensor;
            this.claims = claims;
        }

        /// <summary>The best cover for this claimant (null if none) and its quality 0..1. Skips points others hold.</summary>
        public Marker Pick(IReadOnlyList<Marker> covers, Vector3 guardPos, Vector3 playerPos, Vector3 postPos,
            object claimant, out float quality)
        {
            Marker best = null;
            float bestQuality = 0f;
            for (int i = 0; i < covers.Count; i++)
            {
                Marker cover = covers[i];
                if (cover == null || claims.IsClaimedByOther(cover, claimant))
                {
                    continue;
                }

                // Cheap geometry first; the raycast only runs for a point that could beat the best so far.
                float q = GeometryQuality(cover.transform.position, guardPos, playerPos, postPos);
                if (q <= bestQuality || !IsHidden(cover.transform.position, playerPos))
                {
                    continue;
                }

                best = cover;
                bestQuality = q;
            }

            quality = bestQuality;
            return best;
        }

        /// <summary>Quality of a cover the guard already holds: 0 once the player can see it, otherwise its geometry score.</summary>
        public float Evaluate(Marker cover, Vector3 guardPos, Vector3 playerPos, Vector3 postPos)
        {
            if (cover == null)
            {
                return 0f;
            }

            Vector3 point = cover.transform.position;
            return IsHidden(point, playerPos) ? GeometryQuality(point, guardPos, playerPos, postPos) : 0f;
        }

        private float GeometryQuality(Vector3 cover, Vector3 guardPos, Vector3 playerPos, Vector3 postPos)
        {
            float nearGuard = 1f - GuardMath.Normalise(GuardMath.FlatDistance(cover, guardPos), config.coverSearchRadius);
            float awayFromPlayer = GuardMath.Normalise(GuardMath.FlatDistance(cover, playerPos), config.coverStandoff);
            float nearPost = 1f - GuardMath.Normalise(GuardMath.FlatDistance(cover, postPos), config.coverPostRadius);
            return Mathf.Pow(nearGuard * awayFromPlayer * nearPost, 1f / 3f);
        }

        private bool IsHidden(Vector3 cover, Vector3 playerPos)
        {
            Vector3 playerEye = playerPos + Vector3.up * config.playerEyeHeight;
            Vector3 crouchedHead = cover + Vector3.up * config.coverHeadHeight;
            return !sensor.HasLineOfSight(playerEye, crouchedHead);
        }
    }
}
