using System;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Ground height of the island at any (x, z), in Unity coordinates (metres, +Z north, water at y 0).
    /// It ports the prototype's analytic heightmap (prototype/src/world/terrain.js) term by term: an island
    /// ellipse with a noisy coast, a route that climbs from the beach (y 2) to the compound (y 10), forest
    /// relief, the hill over the bunker, the flat compound pad, the walkable trail and the beach bay.
    /// <see cref="Sample"/> adds two Unity layout fixes on top: a dredged boat berth and a dock landing.
    /// Pure and deterministic (fixed noise seeds, no Random, no scene objects), so EditMode tests can check it.
    /// </summary>
    public static class IslandHeightField
    {
        /// <summary>
        /// Added to every noise seed. 0 reproduces the prototype island. Another value gives a different
        /// coastline and relief, while the pad, beach, trail, berth and dock landing stay where the layout needs them.
        /// </summary>
        public const int Seed = 0;

        // The trail as a polyline: Catmull-Rom through the control points, 10 samples per span (as the prototype).
        private const int TrailSamplesPerSpan = 10;
        private static readonly Vector2[] Trail = BuildTrail(IslandLayout.TrailControlPoints, TrailSamplesPerSpan);

        /// <summary>Height used for the terrain: the prototype island plus the berth and dock landing.</summary>
        public static float Sample(float x, float z)
        {
            double h = Prototype(x, z);

            // Berth: keep the water around the boat at least BerthDepth deep, blending over 3 m.
            double berth = 1 - SmoothStep(0, 3, DistanceOutside(IslandLayout.Berth, x, z));
            h = Lerp(h, Math.Min(h, -IslandLayout.BerthDepth), berth);

            // Dock landing: raise the sand at the shore end of the dock to just below the deck, blending over 3 m.
            double landing = 1 - SmoothStep(0, 3, DistanceOutside(IslandLayout.DockLanding, x, z));
            double landingY = IslandLayout.DockDeckY - IslandLayout.DockLandingBelowDeck;
            h = Math.Max(h, Lerp(h, landingY, landing));

            return (float)h;
        }

        /// <summary>The prototype's height function alone, without the Unity additions.</summary>
        public static float PrototypeHeight(float x, float z)
        {
            return (float)Prototype(x, z);
        }

        /// <summary>Unity z of the beach shoreline (where the sand reaches the water) at this x.</summary>
        public static double ShorelineZ(double x)
        {
            double ax = Math.Abs(x) / 55.0;
            return -105.0 + ax * ax * 12.0 - Math.Sin(x * 0.11) * 1.2;
        }

        /// <summary>Unity z of the cliff line: straight behind the compound (z 62), wavy further out.</summary>
        public static double CliffLineZ(double x)
        {
            double ax = Math.Abs(x);
            if (ax <= 24)
            {
                return IslandLayout.CompoundMaxZ;
            }

            double w = SmoothStep(24, 40, ax);
            return IslandLayout.CompoundMaxZ - w * (Math.Sin(x * 0.09 + 1.3) * 5 + (Noise(x * 0.05, 3.3, 41) - 0.5) * 8 + 3);
        }

        /// <summary>Horizontal distance from (x, z) to the trail's centre line.</summary>
        public static double TrailDistance(double x, double z)
        {
            double best = double.MaxValue;
            for (int i = 0; i < Trail.Length - 1; i++)
            {
                Vector2 a = Trail[i];
                Vector2 b = Trail[i + 1];

                // Cheap reject: this span cannot be closer than the best one so far.
                if (z < Math.Min(a.y, b.y) - best || z > Math.Max(a.y, b.y) + best)
                {
                    continue;
                }

                best = Math.Min(best, DistanceToSegment(x, z, a, b));
            }

            return best;
        }

        private static double Prototype(double x, double z)
        {
            // Noise is sampled at the prototype's z, so the island matches the heights in the reference doc.
            double pz = -z;

            // Island ellipse centred on z 28; the coast is noisy, but calmer in the beach bay.
            double ex = x / IslandLayout.IslandRadiusX;
            double ez = (z - IslandLayout.IslandCentreZ) / IslandLayout.IslandRadiusZ;
            double d = Math.Sqrt(ex * ex + ez * ez);
            double bay = SmoothStep(-40, -90, z);
            d += (Fbm(x * 0.012 + 11.3, pz * 0.012 - 4.1, 3, 5) - 0.5) * 0.24 * (1 - bay * 0.85);
            double inland = 1 - d;

            // Route profile: y 2 at the top of the beach (z -92) rising to y 10 at the compound gate (z 30).
            double t = SmoothStep(-92, 30, z);
            double route = IslandLayout.BeachTopY + (IslandLayout.PlateauY - IslandLayout.BeachTopY) * t;
            double h = route;

            // Rolling forest relief: flat on the trail, higher either side of it, fading out toward the beach.
            double trail = TrailDistance(x, z);
            double n1 = Fbm(x * 0.022 + 3.7, pz * 0.022 + 1.3, 4, 21);
            double n2 = Fbm(x * 0.08, pz * 0.08, 2, 23);
            double relief = ((n1 - 0.5) * 9 + (n2 - 0.5) * 1.4) * SmoothStep(3, 14, trail) * SmoothStep(-92, -74, z);
            double valley = SmoothStep(5, 35, trail) * 2.2 * SmoothStep(-92, -76, z);
            h += relief + valley;

            // Hill north of the cliff line (14.6 m at the cliff top, up to about 21.5 m): it hides the bunker.
            double cliff = CliffLineZ(x);
            if (z > cliff)
            {
                double k = SmoothStep(cliff, cliff + 22, z);
                h = 14.6 + 6 * k + (Fbm(x * 0.04, pz * 0.04, 3, 29) - 0.5) * 3 * k;
            }
            else if (z > cliff - 22)
            {
                // East and west of the compound the ground rises toward the cliff foot.
                h = Math.Max(h, Lerp(h, IslandLayout.PlateauY, SmoothStep(cliff - 22, cliff - 4, z)));
            }

            // Compound pad: exactly PlateauY inside the compound rectangle, blending out over 16 m to the south.
            if (z <= IslandLayout.CompoundMaxZ + 0.01)
            {
                double dx = Math.Max(Math.Max(IslandLayout.CompoundMinX - x, 0), x - IslandLayout.CompoundMaxX);
                double dz = Math.Max(0, IslandLayout.CompoundMinZ - z);
                double pad = 1 - SmoothStep(0, 16, Math.Sqrt(dx * dx + dz * dz));
                h = Lerp(h, IslandLayout.PlateauY, pad);
            }

            // Trail: pulled toward the smooth route profile so it stays walkable.
            if (z < 40)
            {
                double onTrail = 1 - SmoothStep(1.5, 6, trail);
                h = Lerp(h, route + (n2 - 0.5) * 0.3, onTrail * 0.85);
            }

            // Beach bay: a plane 2 m high 17 m inland of the shoreline, sloping BeachSlope per metre into the sea.
            double shore = ShorelineZ(x);
            double beachY = IslandLayout.BeachTopY - (shore + 17 - z) * IslandLayout.BeachSlope;
            double beach = SmoothStep(shore + 26, shore + 15, z)
                * (1 - SmoothStep(42, 72, Math.Abs(x)))
                * (1 - SmoothStep(shore - 22, shore - 40, z));
            h = Lerp(h, Math.Min(h, beachY), beach);

            // Everywhere but the bay, the coast falls to a noisy sea bed at about -5 m.
            double coast = SmoothStep(0, 0.11, inland);
            double seabed = -5 + (Fbm(x * 0.03, pz * 0.03, 2, 31) - 0.5) * 2;
            h = Lerp(seabed, h, Lerp(coast, 1, beach));
            return Math.Max(h, -7);
        }

        private static Vector2[] BuildTrail(Vector2[] points, int samplesPerSpan)
        {
            var result = new Vector2[(points.Length - 1) * samplesPerSpan + 1];
            int n = 0;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = points[Math.Max(0, i - 1)];
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                Vector2 p3 = points[Math.Min(points.Length - 1, i + 2)];
                for (int s = 0; s < samplesPerSpan; s++)
                {
                    float t = (float)s / samplesPerSpan;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    result[n++] = 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                        + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                }
            }

            result[n] = points[points.Length - 1];
            return result;
        }

        private static double DistanceToSegment(double x, double z, Vector2 a, Vector2 b)
        {
            double abx = b.x - a.x;
            double abz = b.y - a.y;
            double lengthSq = abx * abx + abz * abz;
            double t = lengthSq > 0 ? ((x - a.x) * abx + (z - a.y) * abz) / lengthSq : 0;
            t = Math.Max(0, Math.Min(1, t));
            double cx = a.x + abx * t - x;
            double cz = a.y + abz * t - z;
            return Math.Sqrt(cx * cx + cz * cz);
        }

        private static double DistanceOutside(Rect rect, double x, double z)
        {
            double dx = Math.Max(Math.Max(rect.xMin - x, 0), x - rect.xMax);
            double dz = Math.Max(Math.Max(rect.yMin - z, 0), z - rect.yMax);
            return Math.Sqrt(dx * dx + dz * dz);
        }

        // ---- Value noise, identical to the prototype's hash2 / noise2 / fbm2 -------------------------------

        private static double Hash(int ix, int iy, int seed)
        {
            unchecked
            {
                uint h = (uint)ix * 374761393u + (uint)iy * 668265263u + (uint)seed * 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / 4294967296.0;
            }
        }

        /// <summary>The prototype's value noise (noise2), 0 .. 1. Public so the vegetation scatter samples the same noise.</summary>
        public static double Noise(double x, double y, int seed)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            double fx = Fade(x - x0);
            double fy = Fade(y - y0);
            int s = seed + Seed;
            double bottom = Lerp(Hash(x0, y0, s), Hash(x0 + 1, y0, s), fx);
            double top = Lerp(Hash(x0, y0 + 1, s), Hash(x0 + 1, y0 + 1, s), fx);
            return Lerp(bottom, top, fy);
        }

        /// <summary>The prototype's fractal noise (fbm2): octaves at double the frequency and half the amplitude, roughly 0 .. 1.</summary>
        public static double Fbm(double x, double y, int octaves, int seed)
        {
            double amplitude = 0.5;
            double frequency = 1;
            double sum = 0;
            double norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amplitude * Noise(x * frequency, y * frequency, seed + i * 17);
                norm += amplitude;
                amplitude *= 0.5;
                frequency *= 2;
            }

            return sum / norm;
        }

        private static double Fade(double t)
        {
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        /// <summary>GLSL-style smoothstep (Mathf.SmoothStep is an interpolation, not this). Works with edge0 &gt; edge1.</summary>
        public static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = Math.Max(0, Math.Min(1, (x - edge0) / (edge1 - edge0)));
            return t * t * (3 - 2 * t);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }
    }
}
