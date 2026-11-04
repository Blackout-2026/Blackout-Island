using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// The wet sand band along the waterline. A strip of triangles is laid 3.5 cm above the terrain between two heights,
    /// from just under the water (-0.10 m) up to about +0.55 m, where the top edge wobbles so it is not a straight line.
    /// Vertex alpha is the wetness (1 at the waterline, fading to 0 at the dry edge); the Blackout/WetSand shader turns it
    /// into a dark glossy overlay. The strip is baked in world space, from whatever height function it is given, so the
    /// tool passes in the real terrain and the tests pass in a plain slope.
    /// </summary>
    public static class WetSandStrip
    {
        public const float MinX = -55f;
        public const float MaxX = 55f;
        public const float ColumnSpacing = 1f;
        public const int Rows = 7;
        public const float LowHeight = -0.10f;
        public const float HighHeight = 0.55f;
        public const float Lift = 0.035f;

        // Where the search for the waterline starts (open sea) and ends (inland), and how finely it walks.
        private const float SeaZ = -118f;
        private const float LandZ = -80f;
        private const float MarchStep = 0.1f;

        // A column is skipped when the wet band would be narrower than this (a cliff) or wider than the maximum (a flat).
        private const float MinBand = 1f;
        private const float MaxBand = 14f;

        public static Mesh Create(Func<float, float, float> height)
        {
            int columns = Mathf.RoundToInt((MaxX - MinX) / ColumnSpacing) + 1;
            var low = new float[columns];
            var high = new float[columns];
            var valid = new bool[columns];
            for (int c = 0; c < columns; c++)
            {
                float x = MinX + c * ColumnSpacing;
                float? z0 = FindCrossing(height, x, LowHeight);
                float? z1 = FindCrossing(height, x, HighHeight + TopEdgeWobble(x));
                if (z0.HasValue && z1.HasValue && z1.Value - z0.Value >= MinBand && z1.Value - z0.Value <= MaxBand)
                {
                    low[c] = z0.Value;
                    high[c] = z1.Value;
                    valid[c] = true;
                }
            }

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var first = new int[columns]; // index of row 0 of each valid column
            for (int c = 0; c < columns; c++)
            {
                first[c] = -1;
                if (!valid[c]) continue;
                first[c] = vertices.Count;
                float x = MinX + c * ColumnSpacing;
                for (int r = 0; r < Rows; r++)
                {
                    float t = r / (float)(Rows - 1);
                    float z = Mathf.Lerp(low[c], high[c], t);
                    vertices.Add(new Vector3(x, height(x, z) + Lift, z));
                    normals.Add(SurfaceNormal(height, x, z));
                    float wet = 1f - Mathf.SmoothStep(0f, 1f, t);
                    colors.Add(new Color(0f, 0f, 0f, wet));
                    uv.Add(new Vector2(x * 0.25f, t));
                }
            }

            // Same winding as the ocean: x grows with the column, z with the row.
            var triangles = new List<int>();
            for (int c = 0; c < columns - 1; c++)
            {
                if (!valid[c] || !valid[c + 1]) continue;
                for (int r = 0; r < Rows - 1; r++)
                {
                    int a = first[c] + r;
                    int b = a + 1;
                    int d = first[c + 1] + r;
                    int e = d + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(d);
                    triangles.Add(b); triangles.Add(e); triangles.Add(d);
                }
            }

            var mesh = new Mesh { name = "ShoreWetSand" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Height added to the top edge so it meanders (up to about 0.28 m, a couple of metres along the slope).</summary>
        public static float TopEdgeWobble(float x)
        {
            return 0.18f * Mathf.Sin(x * 0.45f) + 0.10f * Mathf.Sin(x * 1.1f + 2f);
        }

        /// <summary>First z, walking inland from the sea, where the ground reaches <paramref name="target"/>; null if none.</summary>
        public static float? FindCrossing(Func<float, float, float> height, float x, float target)
        {
            float previousZ = SeaZ;
            float previousH = height(x, previousZ);
            if (previousH >= target) return null; // the sea end is already above the target: not a beach column
            for (float z = SeaZ + MarchStep; z <= LandZ; z += MarchStep)
            {
                float h = height(x, z);
                if (h >= target)
                {
                    float f = (target - previousH) / Mathf.Max(h - previousH, 1e-5f);
                    return Mathf.Lerp(previousZ, z, f);
                }

                previousZ = z;
                previousH = h;
            }

            return null;
        }

        private static Vector3 SurfaceNormal(Func<float, float, float> height, float x, float z)
        {
            const float d = 0.5f;
            float dx = (height(x + d, z) - height(x - d, z)) / (2f * d);
            float dz = (height(x, z + d) - height(x, z - d)) / (2f * d);
            return new Vector3(-dx, 1f, -dz).normalized;
        }
    }

    /// <summary>A bent, tapering, slightly lumpy log lying along local x with its underside near y 0. Seeded, so it is repeatable.</summary>
    public static class DriftwoodMesh
    {
        private const int Rings = 9;
        private const int Sides = 8;

        public static Mesh Create(string name, uint seed, float length, float radius)
        {
            var rng = new Mulberry32(seed);
            float bendUp = (float)rng.Range(0.01, 0.05) * length;
            float bendSide = (float)rng.Range(-0.08, 0.08) * length;

            var ringRadius = new float[Rings, Sides];
            for (int r = 0; r < Rings; r++)
            {
                float t = r / (float)(Rings - 1);
                float baseRadius = radius * (1f - 0.3f * t) * (0.92f + 0.16f * (float)rng.Next());
                for (int s = 0; s < Sides; s++)
                {
                    ringRadius[r, s] = baseRadius * (0.9f + 0.2f * (float)rng.Next());
                }
            }

            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var ringCentres = new Vector3[Rings];
            for (int r = 0; r < Rings; r++)
            {
                float t = r / (float)(Rings - 1);
                float lift = Mathf.Sin(t * Mathf.PI);
                ringCentres[r] = new Vector3((t - 0.5f) * length, radius * 1.1f + bendUp * lift, bendSide * lift);
                for (int s = 0; s <= Sides; s++) // the last vertex repeats the first so the uv can wrap
                {
                    float angle = s * Mathf.PI * 2f / Sides;
                    float rr = ringRadius[r, s % Sides];
                    vertices.Add(ringCentres[r] + new Vector3(0f, Mathf.Sin(angle) * rr, Mathf.Cos(angle) * rr));
                    uv.Add(new Vector2(s / (float)Sides, t * length * 0.7f));
                }
            }

            int row = Sides + 1;
            for (int r = 0; r < Rings - 1; r++)
            {
                for (int s = 0; s < Sides; s++)
                {
                    int a = r * row + s;
                    int b = a + row;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }

            AddCap(vertices, uv, triangles, ringCentres[0], ringRadius, 0, facingStart: true);
            AddCap(vertices, uv, triangles, ringCentres[Rings - 1], ringRadius, Rings - 1, facingStart: false);

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddCap(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Vector3 centre,
            float[,] ringRadius, int ring, bool facingStart)
        {
            int centreIndex = vertices.Count;
            vertices.Add(centre);
            uv.Add(new Vector2(0.5f, 0.5f));
            for (int s = 0; s < Sides; s++)
            {
                float angle = s * Mathf.PI * 2f / Sides;
                float rr = ringRadius[ring, s];
                vertices.Add(centre + new Vector3(0f, Mathf.Sin(angle) * rr, Mathf.Cos(angle) * rr));
                uv.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
            }

            for (int s = 0; s < Sides; s++)
            {
                int current = centreIndex + 1 + s;
                int next = centreIndex + 1 + (s + 1) % Sides;
                triangles.Add(centreIndex);
                triangles.Add(facingStart ? current : next);
                triangles.Add(facingStart ? next : current);
            }
        }
    }

    /// <summary>
    /// A small tileable normal map for the terrain's Sand layer: soft undulation and fine grain from five octaves of noise,
    /// with only a faint, bent, patchy trace of wind ripple. Everything repeats exactly at the tile edge (whole numbers of
    /// cycles and wrapped noise), so the terrain can tile it. Seeded, so the same PNG comes out every time.
    /// </summary>
    public static class SandRippleTexture
    {
        public const int Size = 256;
        private const uint Seed = 9201;
        private const float Strength = 3f;

        // Octaves of wrapped value noise, from soft undulation (4 cells across the tile) to grain (64 cells). The amplitude
        // halves as the frequency doubles, so each octave tilts the surface by about the same small angle.
        private static readonly int[] OctaveCells = { 4, 8, 16, 32, 64 };
        private static readonly float[] OctaveAmplitude = { 0.5f, 0.6f, 0.35f, 0.2f, 0.1f };
        private const float RippleAmplitude = 0.12f;

        public static Texture2D Create(int size = Size)
        {
            var rng = new Mulberry32(Seed);
            var octaves = new float[OctaveCells.Length][,];
            for (int i = 0; i < octaves.Length; i++) octaves[i] = Lattice(rng, OctaveCells[i]);
            float[,] warpNoise = Lattice(rng, 4);
            float[,] patchNoise = Lattice(rng, 4);

            var height = new float[size, size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float v = y / (float)size;
                    float h = 0f;
                    for (int i = 0; i < octaves.Length; i++) h += OctaveAmplitude[i] * Sample(octaves[i], u, v);

                    // Only a faint trace of wind ripple, and an irregular one: the crests are bent hard by low-frequency
                    // noise and only exist in patches. Regular parallel ripples read as corduroy under moonlight.
                    float bent = v + 0.06f * Sample(warpNoise, u, v);
                    float patch = Mathf.SmoothStep(0f, 1f, 0.5f + 1.2f * Sample(patchNoise, u, v));
                    h += RippleAmplitude * patch * Mathf.Sin(2f * Mathf.PI * 14f * bent);
                    height[x, y] = h;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "Sand_Ripple_Normal" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (height[(x + 1) % size, y] - height[(x - 1 + size) % size, y]) * 0.5f;
                    float dy = (height[x, (y + 1) % size] - height[x, (y - 1 + size) % size]) * 0.5f;
                    Vector3 n = new Vector3(-dx * Strength, -dy * Strength, 1f).normalized;
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                        (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f),
                        255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        private static float[,] Lattice(Mulberry32 rng, int count)
        {
            var values = new float[count, count];
            for (int y = 0; y < count; y++)
            {
                for (int x = 0; x < count; x++)
                {
                    values[x, y] = (float)rng.Range(-1.0, 1.0);
                }
            }

            return values;
        }

        /// <summary>Smooth value noise at (u, v) in [0, 1), wrapping at the lattice edge.</summary>
        private static float Sample(float[,] lattice, float u, float v)
        {
            int count = lattice.GetLength(0);
            float fx = u * count;
            float fy = v * count;
            int x0 = Mathf.FloorToInt(fx) % count;
            int y0 = Mathf.FloorToInt(fy) % count;
            int x1 = (x0 + 1) % count;
            int y1 = (y0 + 1) % count;
            float tx = Mathf.SmoothStep(0f, 1f, fx - Mathf.Floor(fx));
            float ty = Mathf.SmoothStep(0f, 1f, fy - Mathf.Floor(fy));
            return Mathf.Lerp(Mathf.Lerp(lattice[x0, y0], lattice[x1, y0], tx), Mathf.Lerp(lattice[x0, y1], lattice[x1, y1], tx), ty);
        }
    }

    /// <summary>One thing to put on the beach: what it is, where, and how it sits.</summary>
    public struct ShoreProp
    {
        public ShoreKind kind;
        public Vector3 position;
        public Vector3 euler;
        public float scale;
    }

    public enum ShoreKind { Rock, RockSmall, RockRound, Driftwood0, Driftwood1, Driftwood2 }

    /// <summary>
    /// Decides where the shore rocks and driftwood go, with a fixed seed: rocks at the waterline, driftwood on the drier
    /// sand. The middle of the beach (the dock, its cargo, the sign and the trail start, x -7 .. 9) is kept clear, nothing
    /// lands within 1.6 m of a tree, and every piece is at least 3.5 m from the others.
    /// </summary>
    public static class ShorePlacement
    {
        public const uint Seed = 9301;
        public const int RockCount = 7;
        public const int DriftwoodCount = 5;
        public const float ClearMinX = -7f;
        public const float ClearMaxX = 9f;
        public const float TreeClearance = 1.6f;
        public const float Spacing = 3.5f;
        public static readonly float[] DriftwoodLength = { 2.6f, 1.8f, 3.2f };

        public static List<ShoreProp> Plan(Func<float, float, float> height, IReadOnlyList<Vector2> treeXZ)
        {
            var rng = new Mulberry32(Seed);
            var placed = new List<ShoreProp>();
            int rocks = 0;
            int logs = 0;
            for (int attempt = 0; attempt < 4000 && (rocks < RockCount || logs < DriftwoodCount); attempt++)
            {
                bool wantRock = rocks < RockCount && (logs >= DriftwoodCount || rng.Next() < 0.5);
                float x = (float)rng.Range(-52.0, 52.0);
                float z = (float)rng.Range(-116.0, -84.0);
                int variant = rng.Int(0, 2);
                float yaw = (float)rng.Range(0.0, 360.0);
                float scale = wantRock ? (float)rng.Range(0.22, 0.5) : 1f;
                float h = height(x, z);

                if (x > ClearMinX && x < ClearMaxX) continue;
                if (wantRock ? (h < -0.25f || h > 0.8f) : (h < 0.35f || h > 1.3f)) continue;
                if (Slope(height, x, z) > 0.25f) continue;
                if (NearAnyTree(treeXZ, x, z) || TooCloseToOthers(placed, x, z)) continue;

                var prop = new ShoreProp { scale = scale };
                if (wantRock)
                {
                    prop.kind = (ShoreKind)variant; // Rock, RockSmall or RockRound
                    // The mesh pivot is its centre; sink about 30 % of the rock into the sand and tilt it a few degrees.
                    prop.position = new Vector3(x, h + 0.1f * scale, z);
                    prop.euler = new Vector3((float)rng.Range(-8.0, 8.0), yaw, (float)rng.Range(-8.0, 8.0));
                    rocks++;
                }
                else
                {
                    prop.kind = ShoreKind.Driftwood0 + variant;
                    // Lie roughly along the shore (x axis, either way round) and follow the slope under the log.
                    float alongShore = (float)rng.Range(-35.0, 35.0) + (rng.Next() < 0.5 ? 0f : 180f);
                    Vector3 axis = Quaternion.Euler(0f, alongShore, 0f) * Vector3.right;
                    float half = DriftwoodLength[variant] * 0.5f;
                    float rise = height(x + axis.x * half, z + axis.z * half) - height(x - axis.x * half, z - axis.z * half);
                    float pitch = Mathf.Atan2(rise, DriftwoodLength[variant]) * Mathf.Rad2Deg;
                    prop.position = new Vector3(x, h - 0.03f, z);
                    prop.euler = new Vector3(0f, alongShore, pitch);
                    logs++;
                }

                placed.Add(prop);
            }

            return placed;
        }

        private static float Slope(Func<float, float, float> height, float x, float z)
        {
            float dx = (height(x + 0.5f, z) - height(x - 0.5f, z)) / 1f;
            float dz = (height(x, z + 0.5f) - height(x, z - 0.5f)) / 1f;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static bool NearAnyTree(IReadOnlyList<Vector2> trees, float x, float z)
        {
            float limit = TreeClearance * TreeClearance;
            for (int i = 0; i < trees.Count; i++)
            {
                float dx = trees[i].x - x;
                float dz = trees[i].y - z;
                if (dx * dx + dz * dz < limit) return true;
            }

            return false;
        }

        private static bool TooCloseToOthers(List<ShoreProp> placed, float x, float z)
        {
            float limit = Spacing * Spacing;
            for (int i = 0; i < placed.Count; i++)
            {
                float dx = placed[i].position.x - x;
                float dz = placed[i].position.z - z;
                if (dx * dx + dz * dz < limit) return true;
            }

            return false;
        }
    }
}
