// Owner: Systems Engineer (Hunter AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;
using UnityEngine.AI;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// The real <see cref="IHunterWorld"/> (docs/reference/systems-engineer.md section 15):
    /// - Waypoints: the Waypoint markers owned by this agent (its name, for example Hunter-1), in Id order.
    /// - Cover: every CoverPoint marker, every terrain tree (pines, palms, rocks) and every bush (a terrain detail) on
    ///   the loaded terrains, gathered once on first use. A cover point is offset behind the cover from the threat and
    ///   snapped to the NavMesh if one is near.
    /// - Randomness: a System.Random seeded from the agent's name, so a run can be repeated.
    /// </summary>
    public sealed class HunterWorld : IHunterWorld
    {
        private const float NavMeshSnapDistance = 1.5f;

        private static readonly Dictionary<TerrainData, Vector3[]> BushCache = new Dictionary<TerrainData, Vector3[]>();

        private readonly IAgentContext context;
        private readonly HunterConfig config;
        private readonly System.Random random;
        private IReadOnlyList<Marker> sourceWaypoints;
        private Marker[] route = Array.Empty<Marker>();
        private List<Vector3> covers;

        public HunterWorld(IAgentContext context, HunterConfig config)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.config = config;
            random = new System.Random(Seed(context.Self != null ? context.Self.name : string.Empty));
        }

        /// <summary>Gathered cover positions (markers and terrain trees). For the H4 gizmos and tests.</summary>
        public IReadOnlyList<Vector3> Covers => GatherCovers();

        public int WaypointCount
        {
            get
            {
                RefreshRoute();
                return route.Length;
            }
        }

        public Vector3 GetWaypoint(int index)
        {
            RefreshRoute();
            return route[index].transform.position;
        }

        public bool TryFindCover(Vector3 self, Vector3 threat, float radius, out Vector3 point)
        {
            List<Vector3> all = GatherCovers();
            float best = radius;
            int found = -1;
            for (int i = 0; i < all.Count; i++)
            {
                float distance = CoverMath.FlatDistance(self, all[i]);
                if (distance <= best)
                {
                    best = distance;
                    found = i;
                }
            }

            point = found >= 0 ? Snap(CoverMath.Behind(all[found], threat, config.coverOffset)) : self;
            return found >= 0;
        }

        public bool TryFindAdvanceCover(Vector3 self, Vector3 target, float range, float minGain, out Vector3 point)
        {
            List<Vector3> all = GatherCovers();
            float limit = CoverMath.FlatDistance(self, target) - minGain;
            float best = range;
            int found = -1;
            for (int i = 0; i < all.Count; i++)
            {
                float fromSelf = CoverMath.FlatDistance(self, all[i]);
                if (fromSelf <= best && CoverMath.FlatDistance(all[i], target) <= limit)
                {
                    best = fromSelf;
                    found = i;
                }
            }

            point = found >= 0 ? Snap(CoverMath.Behind(all[found], target, config.coverOffset)) : self;
            return found >= 0;
        }

        public float Random01()
        {
            return (float)random.NextDouble();
        }

        // The markers may load after the agent: follow whichever list the registry gives now (it is cached there).
        private void RefreshRoute()
        {
            IReadOnlyList<Marker> markers = context.GetMarkers(MarkerType.Waypoint, context.Self.name);
            if (ReferenceEquals(markers, sourceWaypoints))
            {
                return;
            }

            sourceWaypoints = markers;
            var sorted = new Marker[markers.Count];
            for (int i = 0; i < sorted.Length; i++)
            {
                sorted[i] = markers[i];
            }

            Array.Sort(sorted, (a, b) => string.CompareOrdinal(a.Id, b.Id)); // Waypoint_Hunter-1_01, _02, ...
            route = sorted;
        }

        private List<Vector3> GatherCovers()
        {
            if (covers != null)
            {
                return covers;
            }

            covers = new List<Vector3>();
            IReadOnlyList<Marker> markers = context.GetMarkers(MarkerType.CoverPoint);
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] != null)
                {
                    covers.Add(markers[i].transform.position);
                }
            }

            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                TerrainData data = terrain.terrainData;
                if (data == null)
                {
                    continue;
                }

                Vector3 origin = terrain.transform.position;
                foreach (TreeInstance tree in data.treeInstances)
                {
                    covers.Add(origin + Vector3.Scale(tree.position, data.size));
                }

                covers.AddRange(Bushes(terrain));
            }

            return covers;
        }

        /// <summary>
        /// The bushes on a terrain. They are terrain details (W6), stored as a count per detail cell, not tree
        /// instances: every cell of a bush layer with a count above 0 gives one cover position on the ground. Read once
        /// per terrain and shared by every Hunter, because the layer is a 512 x 512 array.
        /// </summary>
        public static IReadOnlyList<Vector3> Bushes(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            if (BushCache.TryGetValue(data, out Vector3[] cached))
            {
                return cached;
            }

            var bushes = new List<Vector3>();
            DetailPrototype[] prototypes = data.detailPrototypes;
            int width = data.detailWidth;
            int height = data.detailHeight;
            Vector3 origin = terrain.transform.position;
            for (int layer = 0; layer < prototypes.Length; layer++)
            {
                if (!IsBush(prototypes[layer]))
                {
                    continue;
                }

                int[,] counts = data.GetDetailLayer(0, 0, width, height, layer);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (counts[y, x] > 0)
                        {
                            Vector3 point = DetailCellCentre(origin, data.size, width, height, x, y);
                            point.y = origin.y + terrain.SampleHeight(point);
                            bushes.Add(point);
                        }
                    }
                }
            }

            cached = bushes.ToArray();
            BushCache[data] = cached;
            return cached;
        }

        /// <summary>A bush layer: its prefab or texture is named like the World Builder's Bush (or Bush_Placeholder).</summary>
        public static bool IsBush(DetailPrototype prototype)
        {
            string name = prototype.prototype != null ? prototype.prototype.name
                : prototype.prototypeTexture != null ? prototype.prototypeTexture.name : string.Empty;
            return name.IndexOf("bush", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>The world position (height 0) of the centre of detail cell (x, y) on a terrain at origin.</summary>
        public static Vector3 DetailCellCentre(Vector3 origin, Vector3 size, int width, int height, int x, int y)
        {
            return new Vector3(origin.x + (x + 0.5f) / width * size.x, 0f, origin.z + (y + 0.5f) / height * size.z);
        }

        private static Vector3 Snap(Vector3 point)
        {
            return NavMesh.SamplePosition(point, out NavMeshHit hit, NavMeshSnapDistance, NavMesh.AllAreas)
                ? hit.position
                : point;
        }

        private static int Seed(string name)
        {
            int hash = 17;
            for (int i = 0; i < name.Length; i++)
            {
                hash = hash * 31 + name[i];
            }

            return hash;
        }
    }
}
