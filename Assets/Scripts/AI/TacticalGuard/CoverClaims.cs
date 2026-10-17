// Owner: Core Developer (Tactical Guard AI).
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>
    /// Who has claimed which cover point. A claimant (a guard's brain) holds at most one claim, and a point can
    /// belong to only one claimant, so two guards never run to the same cover. Guards share one table
    /// (<see cref="Shared"/>) instead of referencing each other.
    /// </summary>
    public sealed class CoverClaims
    {
        private readonly Dictionary<Marker, object> ownerByCover = new Dictionary<Marker, object>();
        private readonly Dictionary<object, Marker> coverByOwner = new Dictionary<object, Marker>();

        /// <summary>The table every guard uses by default. Reset on entering play mode so no claims leak between runs.</summary>
        public static CoverClaims Shared { get; private set; } = new CoverClaims();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShared()
        {
            Shared = new CoverClaims();
        }

        public int Count => ownerByCover.Count;

        /// <summary>True if a different claimant holds this cover. Unclaimed, or claimed by this claimant, is false.</summary>
        public bool IsClaimedByOther(Marker cover, object claimant)
        {
            return ownerByCover.TryGetValue(cover, out object owner) && !ReferenceEquals(owner, claimant);
        }

        /// <summary>The cover this claimant holds, or null.</summary>
        public Marker GetClaim(object claimant)
        {
            return coverByOwner.TryGetValue(claimant, out Marker cover) ? cover : null;
        }

        /// <summary>Claims the cover for the claimant, giving up its previous one. False (and no change) if another holds it.</summary>
        public bool TryClaim(Marker cover, object claimant)
        {
            if (cover == null || IsClaimedByOther(cover, claimant))
            {
                return false;
            }

            Release(claimant);
            ownerByCover[cover] = claimant;
            coverByOwner[claimant] = cover;
            return true;
        }

        /// <summary>Gives up whatever this claimant holds. Safe to call when it holds nothing.</summary>
        public void Release(object claimant)
        {
            if (coverByOwner.TryGetValue(claimant, out Marker cover))
            {
                coverByOwner.Remove(claimant);
                ownerByCover.Remove(cover);
            }
        }
    }
}
