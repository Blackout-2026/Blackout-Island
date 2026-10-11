using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// How much of each terrain layer covers a point of the island. It ports the prototype's terrain shader
    /// (prototype/src/world/terrain.js, the "map_fragment" mix) to four Unity terrain layers:
    /// grass, with forest floor under the trees and bushes; sand on the beach and along every waterline; the dirt
    /// trail; gravel in the compound; rock on steep slopes. Each step blends toward its layer, so the four weights
    /// always add up to 1.
    /// URP Terrain Lit draws four layers in one pass, so the prototype's dirt and gravel are not extra layers: they
    /// are fixed mixes of these four (<see cref="Dirt"/>, <see cref="Gravel"/>), which keeps the terrain at one pass.
    /// Pure and deterministic, so EditMode tests can check it.
    /// </summary>
    public static class TerrainSplat
    {
        /// <summary>The terrain layers in splat-map order; each name is a surface in <see cref="SurfaceLibrary"/>.</summary>
        public static readonly string[] Layers = { "Sand", "Grass", "ForestFloor", "Rock" };

        public const int Sand = 0;
        public const int Grass = 1;
        public const int ForestFloor = 2;
        public const int Rock = 3;

        /// <summary>
        /// The trail. The prototype's dirt (#4E3D2C) is about 35 % sand and 65 % forest floor; half and half is used
        /// instead, so the trail is about 50 % brighter than the grass beside it and stays easy to follow at night.
        /// </summary>
        public static readonly Vector4 Dirt = new Vector4(0.5f, 0f, 0.5f, 0f);

        /// <summary>The compound gravel: the prototype's darkened concrete (#56544B) is about 70 % rock and 30 % sand.</summary>
        public static readonly Vector4 Gravel = new Vector4(0.3f, 0f, 0f, 0.7f);

        // Prototype: the dirt path is a 3.4 m stroke blurred by about 0.8 m.
        private const float TrailHalfWidth = 1.7f;
        private const float TrailBlur = 0.8f;

        // Prototype: litter circles are painted at 55 % and blurred by about 2 m.
        private const float LitterStrength = 0.55f;
        private const float LitterBlur = 2f;

        // Prototype: the compound gravel (1 m inside its east, west and south edges) and a strip through the gate,
        // blurred by about 1.5 m. Unity coordinates.
        private static readonly Rect CompoundGravel = Rect.MinMaxRect(
            IslandLayout.CompoundMinX + 1f, IslandLayout.CompoundMinZ + 1f, IslandLayout.CompoundMaxX - 1f, IslandLayout.CompoundMaxZ);

        private static readonly Rect GateGravel = Rect.MinMaxRect(-3f, 24f, 3f, 32f);
        private const float GravelBlur = 1.5f;

        /// <summary>
        /// The four layer weights (x Sand, y Grass, z ForestFloor, w Rock) at a point.
        /// </summary>
        /// <param name="height">Ground height (m).</param>
        /// <param name="normalY">Up component of the ground normal: 1 on flat ground.</param>
        /// <param name="z">Unity z (sand only reaches up the beach south of z -60).</param>
        /// <param name="trail"><see cref="TrailMask"/> at the point.</param>
        /// <param name="gravel"><see cref="GravelMask"/> at the point.</param>
        /// <param name="litter">Sum of <see cref="LitterFootprint"/> of the trees and bushes nearby.</param>
        /// <param name="macro"><see cref="Macro"/> at the point.</param>
        public static Vector4 Weights(float height, float normalY, float z, float trail, float gravel, float litter, float macro)
        {
            float litterWeight = Mathf.Clamp01(litter * 1.6f + (macro - 0.35f) * 1.2f);
            var weights = new Vector4(0f, 1f - litterWeight, litterWeight, 0f);

            // Sand up to 1.9 .. 2.8 m on the beach (prototype z >= 60), and below 0.4 .. 1.4 m on every shore.
            float sand = (1f - SmoothStep(1.9f, 2.8f, height)) * (z <= -60f ? 1f : 0f);
            sand = Mathf.Max(sand, 1f - SmoothStep(0.4f, 1.4f, height));
            weights = Vector4.Lerp(weights, Only(Sand), sand);

            // The trail fades out where it crosses the sand.
            weights = Vector4.Lerp(weights, Dirt, SmoothStep(0.25f, 0.75f, trail) * (1f - sand * 0.7f));
            weights = Vector4.Lerp(weights, Gravel, SmoothStep(0.3f, 0.8f, gravel));

            // Rock from slope 0.30 to 0.48 (1 - normal y), about 46 to 59 degrees: cliffs and steep banks.
            weights = Vector4.Lerp(weights, Only(Rock), SmoothStep(0.30f, 0.48f, 1f - normalY));
            return weights;
        }

        /// <summary>1 on the trail, 0.5 at its edge (1.7 m from the centre line), 0 beyond about 2.5 m.</summary>
        public static float TrailMask(float trailDistance)
        {
            return 1f - SmoothStep(TrailHalfWidth - TrailBlur, TrailHalfWidth + TrailBlur, trailDistance);
        }

        /// <summary>1 inside the compound gravel and the gate strip, 0.5 on their edges, 0 beyond 1.5 m outside.</summary>
        public static float GravelMask(float x, float z)
        {
            float distance = Mathf.Min(SignedDistance(CompoundGravel, x, z), SignedDistance(GateGravel, x, z));
            return 1f - SmoothStep(-GravelBlur, GravelBlur, distance);
        }

        /// <summary>
        /// Forest floor painted under one tree or bush: 55 % inside its radius (3.2 m x scale for trees, 1.6 m x scale
        /// for bushes), fading over 2 m either side of the edge. Footprints add up; the caller clamps the sum to 1.
        /// </summary>
        public static float LitterFootprint(float distance, float radius)
        {
            return LitterStrength * (1f - SmoothStep(radius - LitterBlur, radius + LitterBlur, distance));
        }

        /// <summary>
        /// Slow variation (features about 75 m across) that shifts the grass/forest-floor boundary. The prototype reads
        /// it from the forest-floor texture's green channel at 0.013 repeats per metre (about 0.11 .. 0.20); noise in
        /// that range stands in for it until the texture exists.
        /// </summary>
        public static float Macro(float x, float z)
        {
            return 0.11f + 0.09f * (float)IslandHeightField.Fbm(x * 0.013, -z * 0.013, 2, 61);
        }

        private static Vector4 Only(int layer)
        {
            Vector4 weights = Vector4.zero;
            weights[layer] = 1f;
            return weights;
        }

        // Negative inside the rectangle (minus the distance to its nearest edge), positive outside.
        private static float SignedDistance(Rect rect, float x, float z)
        {
            if (rect.Contains(new Vector2(x, z)))
            {
                return -Mathf.Min(Mathf.Min(x - rect.xMin, rect.xMax - x), Mathf.Min(z - rect.yMin, rect.yMax - z));
            }

            float dx = Mathf.Max(Mathf.Max(rect.xMin - x, 0f), x - rect.xMax);
            float dz = Mathf.Max(Mathf.Max(rect.yMin - z, 0f), z - rect.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            return (float)IslandHeightField.SmoothStep(edge0, edge1, x);
        }
    }
}
