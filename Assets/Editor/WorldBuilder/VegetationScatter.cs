using System;
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Editor
{
    public enum VegetationKind
    {
        Pine,
        Palm,
        Rock,
        Bush,
        Grass,
    }

    /// <summary>One plant or rock: where it stands (Unity x, z), how big it is and how it is turned.</summary>
    public readonly struct VegetationInstance
    {
        public readonly VegetationKind Kind;
        public readonly float X;
        public readonly float Z;
        public readonly float Scale; // 1 = the prototype's model size
        public readonly float Yaw; // radians about +Y: the prototype's rotation negated, because z is flipped
        public readonly float Sink; // metres pushed into the ground, as the prototype does
        public readonly int Variant; // the prototype's model variant: 0 or 1 for trees and bushes, 0 .. 2 for rocks

        public VegetationInstance(VegetationKind kind, double x, double z, double scale, double prototypeRotation,
            double sink, int variant)
        {
            Kind = kind;
            X = (float)x;
            Z = (float)z;
            Scale = (float)scale;
            Yaw = (float)-prototypeRotation;
            Sink = (float)sink;
            Variant = variant;
        }
    }

    /// <summary>The scatter's result, and how many of each kind the clearings removed.</summary>
    public sealed class VegetationSet
    {
        public readonly List<VegetationInstance> Trees = new List<VegetationInstance>(); // pines and palms
        public readonly List<VegetationInstance> Rocks = new List<VegetationInstance>();
        public readonly List<VegetationInstance> Bushes = new List<VegetationInstance>();
        public readonly List<VegetationInstance> Grass = new List<VegetationInstance>();
        public readonly int[] Cleared = new int[Enum.GetValues(typeof(VegetationKind)).Length];

        public int Count(VegetationKind kind)
        {
            int count = 0;
            foreach (List<VegetationInstance> list in new[] { Trees, Rocks, Bushes, Grass })
            {
                foreach (VegetationInstance instance in list)
                {
                    if (instance.Kind == kind)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Where the island's trees, rocks, bushes and grass go. It ports the prototype's seeded placement
    /// (Forest.place in prototype/src/world/forest.js) rule by rule, drawing random numbers in the same order with
    /// the same seeds (<see cref="Mulberry32"/>), so it reproduces the prototype's forest: denser near the trail,
    /// noise clearings, palms on the low ground by the beach, sparse pines on the hill. Then one Unity rule:
    /// trees, rocks and bushes are removed around the Hunters' waypoints (<see cref="VegetationLayout.ClearingRadius"/>).
    /// The ground comes from <see cref="IslandHeightField"/> (the terrain's own heights), so a few candidates near a
    /// height or slope limit can land differently from the prototype's 2 m grid. Pure and deterministic: no scene or
    /// asset access, so EditMode tests can check it. Numbers are in <see cref="VegetationLayout"/>.
    /// </summary>
    public static class VegetationScatter
    {
        // Each kind is sunk a little so it never floats on a slope (prototype values).
        private const double TreeSink = 0.15;
        private const double BushSink = 0.1;
        private const double GrassSink = 0.05;
        private const double RockSinkPerScale = 0.25;

        private const double TwoPi = Math.PI * 2;
        private const double PrototypeTwoPi = 6.28; // bushes, grass and rocks use 6.28 in the prototype
        private const double NormalStep = 0.75; // the prototype's T.normal samples the ground +-0.75 m

        public static VegetationSet Scatter(IReadOnlyList<Vector2> clearings)
        {
            var set = new VegetationSet();
            PlaceTrees(set.Trees);
            PlaceBushes(set.Bushes);
            PlaceGrass(set.Grass);
            PlaceRocks(set.Rocks);

            foreach (List<VegetationInstance> list in new[] { set.Trees, set.Rocks, set.Bushes })
            {
                list.RemoveAll(instance =>
                {
                    bool cleared = InClearing(instance.X, instance.Z, clearings);
                    if (cleared)
                    {
                        set.Cleared[(int)instance.Kind]++;
                    }

                    return cleared;
                });
            }

            return set;
        }

        /// <summary>The (x, z) of every Hunter waypoint and spawn: the centres of the clearings.</summary>
        public static List<Vector2> HunterClearings(IEnumerable<MarkerSpec> markers)
        {
            var points = new List<Vector2>();
            foreach (MarkerSpec marker in markers)
            {
                bool hunter = marker.Owner.StartsWith("Hunter", StringComparison.Ordinal);
                bool routePoint = marker.Type == MarkerType.Waypoint || marker.Type == MarkerType.AgentSpawn;
                var point = new Vector2(marker.Position.x, marker.Position.z);
                if (hunter && routePoint && !points.Contains(point))
                {
                    points.Add(point);
                }
            }

            return points;
        }

        public static bool InClearing(float x, float z, IReadOnlyList<Vector2> clearings)
        {
            float radiusSq = VegetationLayout.ClearingRadius * VegetationLayout.ClearingRadius;
            foreach (Vector2 centre in clearings)
            {
                float dx = x - centre.x;
                float dz = z - centre.y;
                if (dx * dx + dz * dz < radiusSq)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The prototype's "blocked" areas, in Unity coordinates: the compound with a 5 m margin, the cliff face
        /// behind it, and the dock approach on the beach.
        /// </summary>
        public static bool Blocked(double x, double z)
        {
            if (x > -27 && x < 27 && z > 23 && z < 67)
            {
                return true;
            }

            double cliff = IslandHeightField.CliffLineZ(x);
            if (z > cliff - 3 && z < cliff + 5)
            {
                return true;
            }

            return Math.Abs(x) < 4 && z < -88;
        }

        /// <summary>Up component of the ground normal, from heights 0.75 m either side (as the prototype's T.normal).</summary>
        public static double GroundNormalY(double x, double z)
        {
            double hx = IslandHeightField.Sample((float)(x + NormalStep), (float)z) - IslandHeightField.Sample((float)(x - NormalStep), (float)z);
            double hz = IslandHeightField.Sample((float)x, (float)(z + NormalStep)) - IslandHeightField.Sample((float)x, (float)(z - NormalStep));
            double up = 2 * NormalStep;
            return up / Math.Sqrt(hx * hx + up * up + hz * hz);
        }

        // Every comment "prototype:" quotes the condition being ported, in the prototype's coordinates (pz = -z).
        private static void PlaceTrees(List<VegetationInstance> trees)
        {
            var random = new Mulberry32(VegetationLayout.TreeSeed);
            double step = VegetationLayout.TreeGridStep;
            for (double gx = VegetationLayout.TreeGridMinX; gx < VegetationLayout.TreeGridMaxX; gx += step)
            {
                for (double gz = VegetationLayout.TreeGridMinPz; gz < VegetationLayout.TreeGridMaxPz; gz += step)
                {
                    double x = gx + random.Next() * step * VegetationLayout.TreeJitter;
                    double pz = gz + random.Next() * step * VegetationLayout.TreeJitter;
                    double z = -pz;
                    double ground = Ground(x, z);
                    double trail = IslandHeightField.TrailDistance(x, z);
                    double keep = random.Next();
                    double keepOnBeach = random.Next();
                    double palmChance = random.Next();
                    if (ground < VegetationLayout.TreeMinGround || trail < VegetationLayout.TreeTrailClearance || Blocked(x, z))
                    {
                        continue;
                    }

                    if (GroundNormalY(x, z) < VegetationLayout.TreeMinNormalY)
                    {
                        continue;
                    }

                    // Denser near the trail (1.0 within 18 m, 0.2 beyond 85 m), half on the hill, 30 % in noise clearings.
                    double density = 0.2 + 0.8 * IslandHeightField.SmoothStep(85, 18, trail);
                    if (z > IslandHeightField.CliffLineZ(x)) // prototype: z < cliffZ(x)
                    {
                        density *= 0.5;
                    }

                    if (IslandHeightField.Fbm(x * 0.035, pz * 0.035, 2, 77) < 0.4)
                    {
                        density *= 0.3;
                    }

                    if (keep > density * 0.85)
                    {
                        continue;
                    }

                    bool lowBeach = ground < VegetationLayout.PalmBeachMaxGround && z < VegetationLayout.PalmBeachMaxZ; // prototype: z > 60
                    if (lowBeach && keepOnBeach > VegetationLayout.PalmBeachKeep)
                    {
                        continue;
                    }

                    bool palm = lowBeach || (z < VegetationLayout.PalmInlandMaxZ && ground < VegetationLayout.PalmInlandMaxGround
                        && palmChance < VegetationLayout.PalmInlandChance);
                    double scale = VegetationLayout.TreeMinScale + random.Next() * VegetationLayout.TreeScaleRange;
                    double rotation = random.Next() * TwoPi;
                    int variant = random.Next() < 0.5 ? 0 : 1;
                    trees.Add(new VegetationInstance(palm ? VegetationKind.Palm : VegetationKind.Pine, x, z, scale, rotation,
                        TreeSink, variant));
                }
            }
        }

        private static void PlaceBushes(List<VegetationInstance> bushes)
        {
            var random = new Mulberry32(VegetationLayout.BushSeed);
            for (int i = 0; i < VegetationLayout.BushTries && bushes.Count < VegetationLayout.BushCap; i++)
            {
                double x = random.Range(-VegetationLayout.BushMaxX, VegetationLayout.BushMaxX);
                double z = -random.Range(VegetationLayout.BushMinPz, VegetationLayout.BushMaxPz);
                double trail = IslandHeightField.TrailDistance(x, z);
                if (trail < VegetationLayout.BushTrailMin || trail > VegetationLayout.BushTrailMax || Blocked(x, z))
                {
                    continue;
                }

                if (Ground(x, z) < VegetationLayout.BushMinGround)
                {
                    continue;
                }

                double scale = random.Range(0.6, 1.4);
                double rotation = random.Next() * PrototypeTwoPi;
                int variant = random.Next() < 0.5 ? 0 : 1;
                bushes.Add(new VegetationInstance(VegetationKind.Bush, x, z, scale, rotation, BushSink, variant));
            }
        }

        private static void PlaceGrass(List<VegetationInstance> grass)
        {
            var random = new Mulberry32(VegetationLayout.GrassSeed);
            for (int i = 0; i < VegetationLayout.GrassTries && grass.Count < VegetationLayout.GrassCap; i++)
            {
                double x = random.Range(-VegetationLayout.GrassMaxX, VegetationLayout.GrassMaxX);
                double pz = random.Range(VegetationLayout.GrassMinPz, VegetationLayout.GrassMaxPz);
                double z = -pz;
                double trail = IslandHeightField.TrailDistance(x, z);
                if (trail < VegetationLayout.GrassTrailMin || trail > VegetationLayout.GrassTrailMax || Blocked(x, z))
                {
                    continue;
                }

                if (Ground(x, z) < VegetationLayout.GrassMinGround)
                {
                    continue;
                }

                // Clumps where the noise says so, and lining the trail.
                double clump = IslandHeightField.Fbm(x * 0.08, pz * 0.08, 2, 55);
                if (clump < VegetationLayout.GrassClumpMin && trail > VegetationLayout.GrassTrailLining)
                {
                    continue;
                }

                double scale = random.Range(0.6, 1.25);
                double rotation = random.Next() * PrototypeTwoPi;
                grass.Add(new VegetationInstance(VegetationKind.Grass, x, z, scale, rotation, GrassSink, 0));
            }
        }

        private static void PlaceRocks(List<VegetationInstance> rocks)
        {
            var random = new Mulberry32(VegetationLayout.RockSeed);
            for (int i = 0; i < VegetationLayout.RockTries && rocks.Count < VegetationLayout.RockCap; i++)
            {
                double x = random.Range(-VegetationLayout.RockMaxX, VegetationLayout.RockMaxX);
                double z = -random.Range(VegetationLayout.RockMinPz, VegetationLayout.RockMaxPz);
                if (Ground(x, z) < VegetationLayout.RockMinGround)
                {
                    continue;
                }

                bool nearCliff = Math.Abs(z - IslandHeightField.CliffLineZ(x)) < VegetationLayout.RockCliffBand;
                double trail = IslandHeightField.TrailDistance(x, z);
                bool inCompound = x > -24 && x < 24 && z > 26 && z < 63; // prototype: z -63 .. -26
                if (trail < VegetationLayout.RockTrailClearance || inCompound)
                {
                    continue;
                }

                if (!nearCliff && random.Next() < VegetationLayout.RockSkipChance)
                {
                    continue;
                }

                double scale = nearCliff ? random.Range(1.6, 4.2) : random.Range(0.3, 1.5);
                double rotation = random.Next() * PrototypeTwoPi;
                int variant = random.Int(0, 2);
                rocks.Add(new VegetationInstance(VegetationKind.Rock, x, z, scale, rotation, scale * RockSinkPerScale, variant));
            }
        }

        private static double Ground(double x, double z)
        {
            return IslandHeightField.Sample((float)x, (float)z);
        }
    }
}
