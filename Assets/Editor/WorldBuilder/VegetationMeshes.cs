using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Placeholder vegetation meshes, ported from the prototype's geometry builders in prototype/src/world/forest.js
    /// (pineFoliage, palmGeometry, lumpGeometry, bushGeometry, grassTuft) with the same seeds, plus two cheaper levels
    /// of detail (LOD1, LOD2) for the trees and rocks. The prototype had no LODs; Unity needs them to keep the
    /// visible triangle count down (reference section 6, item 24). Pure and deterministic, so tests can check them.
    /// These are stand-ins: free low-poly tree models with their own LODs replace them (see <see cref="VegetationLibrary"/>).
    /// </summary>
    public static class VegetationMeshes
    {
        public const int LodCount = 3;

        // Submeshes: 0 = bark (trunk, coconuts), 1 = leaves (needles, fronds).
        public const int Bark = 0;
        public const int Leaves = 1;

        // Prototype seeds: pine foliage variant 0 (100), palm (9), rock variant 0 (300), bush variant 0 (200).
        private const uint PineSeed = 100;
        private const uint PalmSeed = 9;
        private const int RockSeed = 300;
        private const uint BushSeed = 200;

        // Per LOD: pine tiers, sides per tier, tier height scale (fewer tiers are taller so they still overlap),
        // trunk sides and rows.
        private static readonly (int tiers, int sides, float heightScale, int trunkSides, int trunkRows)[] PineLods =
        {
            (6, 11, 1f, 7, 3), // the prototype's tree
            (6, 6, 1f, 5, 1),
            (3, 5, 1.6f, 4, 1),
        };

        // Per LOD: palm trunk segments and sides, steps along each frond, coconuts.
        private static readonly (int segments, int sides, int frondSteps, bool coconuts)[] PalmLods =
        {
            (11, 8, 14, true), // the prototype's tree
            (5, 5, 5, false),
            (3, 4, 2, false),
        };

        // Per LOD: icosahedron subdivision (the prototype's rocks use 2).
        private static readonly int[] RockDetail = { 2, 1, 0 };

        // ---- Pine: a trunk and tiers of jagged, drooping cones --------------------------------------------------

        public static Mesh Pine(int lod)
        {
            (int tiers, int sides, float heightScale, int trunkSides, int trunkRows) = PineLods[lod];
            var mesh = new MeshBuilder(2);

            // Trunk: 0.34 -> 0.17 m radius, 9.6 m tall, open ends (the base is in the ground, the top in the needles).
            Tube(mesh, Bark, 0.17f, 0.34f, 9.6f, trunkSides, trunkRows);
            mesh.Transform(0, Quaternion.identity, new Vector3(0f, 4.8f, 0f));

            var random = new Mulberry32(PineSeed);
            for (int i = 0; i < tiers; i++)
            {
                float t = (float)i / (tiers - 1);
                float height = (2.6f - t * 1.2f) * heightScale;
                float radius = 2.4f - t * 1.85f + (float)random.Next() * 0.2f;
                float baseY = 2.2f + t * 6.2f;
                int first = mesh.VertexCount;
                Cone(mesh, Leaves, radius, height, sides);
                JagAndDroop(mesh, first, radius, height, i);
                float yaw = (float)(random.Next() * Math.PI * 2);
                float dx = ((float)random.Next() - 0.5f) * 0.2f;
                float dz = ((float)random.Next() - 0.5f) * 0.2f;
                mesh.Transform(first, RotationY(yaw), new Vector3(dx, baseY + height * 0.5f, dz));
            }

            return mesh.ToMesh($"Pine_LOD{lod}");
        }

        // The prototype bends each tier's rim with noise (jag) and pulls it down 0.55 m (droop), and gives the
        // needles soft "volumetric" normals: outward from the trunk and up.
        private static void JagAndDroop(MeshBuilder mesh, int first, float radius, float height, int tier)
        {
            for (int v = first; v < mesh.VertexCount; v++)
            {
                Vector3 p = mesh.Position(v);
                float rim = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                if (rim > 0.01f)
                {
                    float angle = Mathf.Atan2(p.z, p.x);
                    float jag = 1f + ((float)IslandHeightField.Noise(Mathf.Cos(angle) * 2.2 + tier * 7, Mathf.Sin(angle) * 2.2, (int)PineSeed) - 0.5f) * 0.7f;
                    p.x *= jag;
                    p.z *= jag;
                    p.y -= (rim / radius) * (rim / radius) * 0.55f;
                }

                var normal = new Vector3(p.x, radius * 0.55f + (p.y + height * 0.5f) * 0.25f, p.z);
                mesh.Set(v, p, normal.normalized);
            }
        }

        // ---- Palm: a curved trunk, nine drooping fronds and coconuts ---------------------------------------------

        public static Mesh Palm(int lod)
        {
            (int segments, int sides, int frondSteps, bool coconuts) = PalmLods[lod];
            var mesh = new MeshBuilder(2);
            var random = new Mulberry32(PalmSeed);

            // Trunk: 7.5 m, leaning 1.6 .. 2.4 m sideways at the top along a t^1.8 curve.
            const float trunkHeight = 7.5f;
            float bend = 1.6f + (float)random.Next() * 0.8f;
            Vector3 Curve(float t) => new Vector3(Mathf.Pow(t, 1.8f) * bend, t * trunkHeight, 0f);
            for (int i = 0; i < segments; i++)
            {
                float t0 = (float)i / segments;
                Vector3 a = Curve(t0);
                Vector3 b = Curve((float)(i + 1) / segments);
                float radius = 0.26f - t0 * 0.09f;
                int first = mesh.VertexCount;
                Tube(mesh, Bark, radius * 0.86f, radius, Vector3.Distance(a, b) * 1.02f, sides, 1);
                mesh.Transform(first, Quaternion.FromToRotation(Vector3.up, (b - a).normalized), (a + b) * 0.5f);
            }

            // Nine fronds: a rib that rises then droops, with a folded leaf either side.
            Vector3 top = Curve(1f);
            const int fronds = 9;
            for (int f = 0; f < fronds; f++)
            {
                float angle = (float)(f / (double)fronds * Math.PI * 2 + random.Next() * 0.3);
                float length = 3.4f + (float)random.Next();
                float lift = 0.5f + (float)random.Next() * 0.5f;
                float pitch = -0.15f + (float)random.Next() * 0.2f;
                int first = mesh.VertexCount;
                Frond(mesh, length, lift, frondSteps);
                mesh.SmoothNormals(first);
                mesh.Transform(first, RotationY(angle) * RotationZ(pitch), new Vector3(top.x, top.y - 0.1f, top.z));
            }

            if (coconuts)
            {
                for (int c = 0; c < 4; c++)
                {
                    float a = c / 4f * Mathf.PI * 2f;
                    Octahedron(mesh, Bark, 0.16f, new Vector3(top.x + Mathf.Cos(a) * 0.25f, top.y - 0.35f, top.z + Mathf.Sin(a) * 0.25f));
                }
            }

            return mesh.ToMesh($"Palm_LOD{lod}");
        }

        private static void Frond(MeshBuilder mesh, float length, float lift, int steps)
        {
            int first = mesh.VertexCount;
            for (int s = 0; s <= steps; s++)
            {
                float t = (float)s / steps;
                float along = t * length;
                float y = Mathf.Sin(t * Mathf.PI * 0.8f) * lift - t * t * 1.9f;
                // Max(0, ...): in float, sin(PI) is a tiny negative number, and Pow of a negative number is NaN.
                float width = 0.62f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.05f))), 0.7f)
                    * (s % 2 == 1 ? 0.75f : 1f);
                float fold = width * 0.35f;
                mesh.Add(new Vector3(along, y, 0f), Vector3.up, new Vector2(0.5f, t)); // rib
                mesh.Add(new Vector3(along, y - fold, -width), Vector3.up, new Vector2(0f, t));
                mesh.Add(new Vector3(along, y - fold, width), Vector3.up, new Vector2(1f, t));
                if (s < steps)
                {
                    int b0 = first + s * 3;
                    int b1 = first + (s + 1) * 3;
                    mesh.Triangle(Leaves, b0, b1, b0 + 1);
                    mesh.Triangle(Leaves, b1, b1 + 1, b0 + 1);
                    mesh.Triangle(Leaves, b0, b0 + 2, b1);
                    mesh.Triangle(Leaves, b1, b0 + 2, b1 + 2);
                }
            }
        }

        // ---- Rock: a noisy, squashed icosahedron with flat faces ------------------------------------------------

        /// <summary>
        /// The prototype's rock (lumpGeometry seed 300, roughness 0.75, squash 0.62), about 2 m across before scaling.
        /// Its 0.85 depth scale is built in. UVs are box-projected in metres for Env_Rock's world tiling.
        /// </summary>
        public static Mesh Rock(int lod)
        {
            var mesh = new MeshBuilder(1);
            Lump(mesh, 0, RockSeed, RockDetail[lod], 0.75f, 0.62f, new Vector3(1f, 1f, 0.85f));
            return mesh.ToMesh($"Rock_LOD{lod}");
        }

        // ---- Bush: two noisy lumps with soft normals ------------------------------------------------------------

        /// <summary>The prototype's bush (variant 0), about 1 m tall, with its 0.85 height scale built in.</summary>
        public static Mesh Bush()
        {
            var mesh = new MeshBuilder(1);
            var random = new Mulberry32(BushSeed);
            for (int i = 0; i < 2; i++)
            {
                int first = mesh.VertexCount;
                Lump(mesh, 0, (int)BushSeed + i * 3, 1, 0.6f, 0.75f, Vector3.one);
                float scale = 0.55f + (float)random.Next() * 0.35f;
                var offset = new Vector3(((float)random.Next() - 0.5f) * 0.9f, 0.35f + scale * 0.4f, ((float)random.Next() - 0.5f) * 0.9f);
                for (int v = first; v < mesh.VertexCount; v++)
                {
                    mesh.Set(v, mesh.Position(v) * scale + offset, mesh.Normal(v));
                }
            }

            // Soft normals: half the face normal, half the direction from the bush's centre, tilted up.
            for (int v = 0; v < mesh.VertexCount; v++)
            {
                Vector3 p = mesh.Position(v);
                Vector3 outward = new Vector3(p.x, p.y - 0.2f, p.z).normalized;
                Vector3 n = (mesh.Normal(v) + outward) * 0.5f + new Vector3(0f, 0.2f, 0f);
                mesh.Set(v, new Vector3(p.x, p.y * 0.85f, p.z), n.normalized);
            }

            return mesh.ToMesh("Bush");
        }

        // ---- Grass: three crossed 1.0 x 0.75 m cards -------------------------------------------------------------

        /// <summary>The prototype's grass tuft: 3 crossed quads, normals straight up so tufts shade like the ground.</summary>
        public static Mesh GrassTuft()
        {
            var mesh = new MeshBuilder(1);
            for (int i = 0; i < 3; i++)
            {
                int first = mesh.VertexCount;
                Quaternion turn = RotationY(i * Mathf.PI / 3f);
                mesh.Add(turn * new Vector3(-0.5f, 0.745f, 0f), Vector3.up, new Vector2(0f, 1f));
                mesh.Add(turn * new Vector3(0.5f, 0.745f, 0f), Vector3.up, new Vector2(1f, 1f));
                mesh.Add(turn * new Vector3(-0.5f, -0.005f, 0f), Vector3.up, new Vector2(0f, 0f));
                mesh.Add(turn * new Vector3(0.5f, -0.005f, 0f), Vector3.up, new Vector2(1f, 0f));
                mesh.Triangle(0, first, first + 2, first + 1);
                mesh.Triangle(0, first + 2, first + 3, first + 1);
            }

            return mesh.ToMesh("GrassTuft");
        }

        /// <summary>
        /// The prototype's grass blade texture (grassBlades, seed 301): 26 curved blades, dark green at the root to
        /// light green at the tip, on a transparent background. Row 0 is the bottom. Transparent pixels keep a dark
        /// green colour so mipmaps do not get light fringes.
        /// </summary>
        public static Color32[] GrassBlades(int size)
        {
            var pixels = new Color32[size * size];
            var background = new Color32(30, 50, 18, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = background;
            }

            float k = size / 128f; // the prototype draws on a 128 px canvas
            var random = new Mulberry32(301);
            for (int b = 0; b < 26; b++)
            {
                float x = (10f + (float)random.Next() * 108f) * k;
                float lean = ((float)random.Next() - 0.5f) * 40f * k;
                float height = size * (0.45f + (float)random.Next() * 0.55f);
                float g = 130f + (float)random.Next() * 90f;
                var root = new Color(g * 0.25f / 255f, g * 0.45f / 255f, g * 0.15f / 255f);
                var tip = new Color(g * 0.75f / 255f, g * 0.95f / 255f, g * 0.45f / 255f);
                Blade(pixels, size, x, lean, height, 2.5f * k, root, tip);
            }

            return pixels;
        }

        // Fills one blade: two quadratic curves from the root (x +- halfWidth) to the tip (x + lean, height).
        // Coordinates here are measured up from the bottom row.
        private static void Blade(Color32[] pixels, int size, float x, float lean, float height, float halfWidth, Color root, Color tip)
        {
            const int steps = 32;
            var outline = new Vector2[steps * 2 + 2];
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                outline[i] = Quadratic(new Vector2(x - halfWidth, 0f), new Vector2(x + lean * 0.3f, height * 0.5f), new Vector2(x + lean, height), t);
                outline[steps * 2 + 1 - i] = Quadratic(new Vector2(x + halfWidth, 0f), new Vector2(x + lean * 0.3f + 1.5f, height * 0.5f), new Vector2(x + lean, height), t);
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            foreach (Vector2 point in outline)
            {
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
            }

            int fromX = Mathf.Max(0, Mathf.FloorToInt(minX));
            int toX = Mathf.Min(size - 1, Mathf.CeilToInt(maxX));
            for (int py = 0; py < size && py <= height; py++)
            {
                Color colour = Color.Lerp(root, tip, (py + 0.5f) / height);
                for (int px = fromX; px <= toX; px++)
                {
                    if (Inside(outline, new Vector2(px + 0.5f, py + 0.5f)))
                    {
                        pixels[py * size + px] = colour;
                    }
                }
            }
        }

        private static Vector2 Quadratic(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }

        // Even-odd point in polygon.
        private static bool Inside(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        // ---- Shared shapes (ports of three.js CylinderGeometry, IcosahedronGeometry) ----------------------------

        // An open tube centred on the origin along +Y: rows of (sides + 1) vertices from the top (radius top) down.
        private static void Tube(MeshBuilder mesh, int submesh, float radiusTop, float radiusBottom, float height, int sides, int rows)
        {
            int first = mesh.VertexCount;
            float slope = (radiusBottom - radiusTop) / height;
            for (int y = 0; y <= rows; y++)
            {
                float v = (float)y / rows;
                float radius = v * (radiusBottom - radiusTop) + radiusTop;
                for (int x = 0; x <= sides; x++)
                {
                    float u = (float)x / sides;
                    float theta = u * Mathf.PI * 2f;
                    float sin = Mathf.Sin(theta);
                    float cos = Mathf.Cos(theta);
                    mesh.Add(new Vector3(radius * sin, -v * height + height * 0.5f, radius * cos),
                        new Vector3(sin, slope, cos).normalized, new Vector2(u, 1f - v));
                }
            }

            for (int x = 0; x < sides; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    int a = first + y * (sides + 1) + x;
                    int b = first + (y + 1) * (sides + 1) + x;
                    int c = b + 1;
                    int d = a + 1;
                    if (radiusTop > 0f || y != 0)
                    {
                        mesh.Triangle(submesh, a, b, d);
                    }

                    mesh.Triangle(submesh, b, c, d);
                }
            }
        }

        // An open cone (three.js ConeGeometry with one row): apex at +height/2, rim at -height/2.
        private static void Cone(MeshBuilder mesh, int submesh, float radius, float height, int sides)
        {
            Tube(mesh, submesh, 0f, radius, height, sides, 1);
        }

        // A noisy unit sphere (three.js lumpGeometry): an icosahedron subdivided `detail` times, each vertex pushed in
        // or out by noise of its direction, then squashed vertically. Faces are flat (each has its own vertices).
        private static void Lump(MeshBuilder mesh, int submesh, int seed, int detail, float roughness, float squash, Vector3 scale)
        {
            foreach ((Vector3 a, Vector3 b, Vector3 c) in Icosphere(detail))
            {
                Vector3 pa = Displace(a, seed, roughness, squash, scale);
                Vector3 pb = Displace(b, seed, roughness, squash, scale);
                Vector3 pc = Displace(c, seed, roughness, squash, scale);
                Vector3 normal = Vector3.Cross(pb - pa, pc - pa).normalized;
                int first = mesh.VertexCount;
                mesh.Add(pa, normal, BoxUv(pa, normal));
                mesh.Add(pb, normal, BoxUv(pb, normal));
                mesh.Add(pc, normal, BoxUv(pc, normal));
                mesh.Triangle(submesh, first, first + 1, first + 2);
            }
        }

        private static Vector3 Displace(Vector3 p, int seed, float roughness, float squash, Vector3 scale)
        {
            double n = IslandHeightField.Noise(p.x * 1.7 + p.z * 0.9 + seed, p.y * 1.9 - p.z * 0.7, seed) * 0.7
                + IslandHeightField.Noise(p.x * 4.1 + seed, p.y * 3.7 + p.z * 2.3, seed + 1) * 0.3;
            float s = 1f + ((float)n - 0.5f) * roughness;
            return Vector3.Scale(new Vector3(p.x * s, p.y * s * squash, p.z * s), scale);
        }

        // Projects along the face's main axis, in metres, like the blockout's world UVs.
        private static Vector2 BoxUv(Vector3 p, Vector3 normal)
        {
            Vector3 n = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (n.y >= n.x && n.y >= n.z)
            {
                return new Vector2(p.x, p.z);
            }

            return n.x >= n.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
        }

        // three.js PolyhedronGeometry for an icosahedron: each face split into (detail + 1)^2 triangles, on the unit sphere.
        private static IEnumerable<(Vector3, Vector3, Vector3)> Icosphere(int detail)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            Vector3[] corners =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            int cols = detail + 1;
            for (int f = 0; f < faces.Length; f += 3)
            {
                Vector3 a = corners[faces[f]];
                Vector3 b = corners[faces[f + 1]];
                Vector3 c = corners[faces[f + 2]];
                var grid = new Vector3[cols + 1][];
                for (int i = 0; i <= cols; i++)
                {
                    Vector3 aj = Vector3.Lerp(a, c, (float)i / cols);
                    Vector3 bj = Vector3.Lerp(b, c, (float)i / cols);
                    int rows = cols - i;
                    grid[i] = new Vector3[rows + 1];
                    for (int j = 0; j <= rows; j++)
                    {
                        grid[i][j] = rows == 0 ? aj : Vector3.Lerp(aj, bj, (float)j / rows);
                    }
                }

                for (int i = 0; i < cols; i++)
                {
                    for (int j = 0; j < 2 * (cols - i) - 1; j++)
                    {
                        int k = j / 2;
                        if (j % 2 == 0)
                        {
                            yield return (grid[i][k + 1].normalized, grid[i + 1][k].normalized, grid[i][k].normalized);
                        }
                        else
                        {
                            yield return (grid[i][k + 1].normalized, grid[i + 1][k + 1].normalized, grid[i + 1][k].normalized);
                        }
                    }
                }
            }
        }

        // A small closed octahedron with flat faces, used for coconuts (the prototype's are 6 x 5 spheres).
        private static void Octahedron(MeshBuilder mesh, int submesh, float radius, Vector3 centre)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 x = new Vector3(sx * radius, 0f, 0f);
                        Vector3 y = new Vector3(0f, sy * radius, 0f);
                        Vector3 z = new Vector3(0f, 0f, sz * radius);
                        if (sx * sy * sz < 0)
                        {
                            (y, z) = (z, y); // keep the face pointing outward
                        }

                        Vector3 normal = new Vector3(sx, sy, sz).normalized;
                        int first = mesh.VertexCount;
                        mesh.Add(centre + x, normal, Vector2.zero);
                        mesh.Add(centre + y, normal, Vector2.zero);
                        mesh.Add(centre + z, normal, Vector2.zero);
                        mesh.Triangle(submesh, first, first + 1, first + 2);
                    }
                }
            }
        }

        // three.js rotateY / rotateZ use the same matrices as these Unity rotations.
        private static Quaternion RotationY(float radians)
        {
            return Quaternion.AngleAxis(radians * Mathf.Rad2Deg, Vector3.up);
        }

        private static Quaternion RotationZ(float radians)
        {
            return Quaternion.AngleAxis(radians * Mathf.Rad2Deg, Vector3.forward);
        }
    }
}
