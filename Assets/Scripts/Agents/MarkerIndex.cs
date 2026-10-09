// Owner: Agent Controller.
using System.Collections.Generic;
using Blackout.Shared;

namespace Blackout.Agents
{
    /// <summary>
    /// Markers grouped by type and owner. Built once from the markers found in the loaded scenes, so brains can
    /// ask "my waypoints" every decision without searching the scene or allocating: each (type, owner) answer is
    /// created on first use and reused afterwards.
    /// </summary>
    public sealed class MarkerIndex
    {
        private static readonly List<Marker> NoMarkers = new List<Marker>(0);

        private readonly List<Marker> all;
        private readonly Dictionary<(MarkerType, string), List<Marker>> cache = new Dictionary<(MarkerType, string), List<Marker>>();

        public MarkerIndex(IEnumerable<Marker> markers)
        {
            all = markers == null ? NoMarkers : new List<Marker>(markers);
        }

        public int Count => all.Count;

        /// <summary>Markers of a type. A null owner means any owner; otherwise only markers whose Owner matches exactly.</summary>
        public IReadOnlyList<Marker> Get(MarkerType type, string owner = null)
        {
            if (cache.TryGetValue((type, owner), out List<Marker> found))
            {
                return found;
            }

            found = new List<Marker>();
            for (int i = 0; i < all.Count; i++)
            {
                Marker marker = all[i];
                if (marker != null && marker.Type == type && (owner == null || marker.Owner == owner))
                {
                    found.Add(marker);
                }
            }

            cache[(type, owner)] = found;
            return found;
        }
    }
}
