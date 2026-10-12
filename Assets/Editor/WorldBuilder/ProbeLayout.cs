using System;
using System.Collections.Generic;
using Blackout.World;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>A baked reflection probe for one bunker room, in world coordinates.</summary>
    public class ReflectionProbeSpec
    {
        public string Room;
        public Vector3 Position; // where the cubemap is captured: the middle of the room at eye height
        public Vector3 BoxCentre; // the room's interior, used for box projection
        public Vector3 BoxSize;
    }

    /// <summary>
    /// Where Setup Lighting puts its probes (docs/level-layout.md, "Powered lighting").
    /// - Reflection probes: one per bunker room, captured at eye height (1.65 m) in the middle of the room, with a box
    ///   the size of the room's interior so box projection lines the reflections up with the walls.
    /// - Light probes: they give moving objects (the player, the agents, the doors) the baked bounce light. In each room a
    ///   grid at most 3 m apart, 0.5 m in from the walls, in layers at most 2 m apart from 0.5 m over the floor to 0.5 m
    ///   under the ceiling, plus two in every door gap so light does not blend through a wall. None inside the generator.
    ///   Outside along the player's path: the dock, the trail (every 6 m, on its centre line and 3 m to each side) and
    ///   the compound yard (every 5 m), 0.5 m and 2.5 m over the ground.
    /// </summary>
    public static class ProbeLayout
    {
        public const float EyeHeight = 1.65f;
        public const float RoomProbeSpacing = 3f;
        public const float LayerSpacing = 2f;
        public const float WallInset = 0.5f;
        public const float LowestProbe = 0.5f;
        public static readonly float[] OutdoorHeights = { 0.5f, 2.5f };
        public static readonly float[] DoorHeights = { 0.5f, 1.8f }; // the doors are 2.3 m tall

        public const float TrailSpacing = 6f;
        public const float TrailSideOffset = 3f; // just past the 3.4 m trail, where bushes start (3.8 m)
        public const float CompoundSpacing = 5f;
        public const float DockSpacing = 4f;

        /// <summary>The generator's collider (reference section 2): probes inside it would read black.</summary>
        public static readonly Bounds Generator = Bounds(new Vector3(-14.2f, 2.5f, 104.4f), new Vector3(-7.8f, 6.1f, 107.6f));

        // ---- Reflection probes ------------------------------------------------------------------------------------

        public static List<ReflectionProbeSpec> ReflectionProbes(BunkerLayout layout)
        {
            var specs = new List<ReflectionProbeSpec>();
            foreach (Room room in layout.rooms)
            {
                // The box runs from the lowest floor to the highest ceiling (they differ only on the ramp).
                float halfDepth = room.size.z * 0.5f;
                float bottom = Mathf.Min(BunkerShell.FloorY(room, room.position.z - halfDepth), BunkerShell.FloorY(room, room.position.z + halfDepth));
                float top = Mathf.Max(BunkerShell.FloorY(room, room.position.z - halfDepth), BunkerShell.FloorY(room, room.position.z + halfDepth))
                    + room.size.y;
                specs.Add(new ReflectionProbeSpec
                {
                    Room = room.name,
                    Position = new Vector3(room.position.x, BunkerShell.FloorY(room, room.position.z) + EyeHeight, room.position.z),
                    BoxCentre = new Vector3(room.position.x, (bottom + top) * 0.5f, room.position.z),
                    BoxSize = new Vector3(room.size.x, top - bottom, room.size.z),
                });
            }

            return specs;
        }

        // ---- Light probes: bunker ---------------------------------------------------------------------------------

        public static List<Vector3> BunkerProbes(BunkerLayout layout)
        {
            var probes = new List<Vector3>();
            foreach (Room room in layout.rooms)
            {
                float[] xs = Steps(room.position.x - room.size.x * 0.5f + WallInset, room.position.x + room.size.x * 0.5f - WallInset, RoomProbeSpacing);
                float[] zs = Steps(room.position.z - room.size.z * 0.5f + WallInset, room.position.z + room.size.z * 0.5f - WallInset, RoomProbeSpacing);
                float[] heights = Steps(LowestProbe, room.size.y - LowestProbe, LayerSpacing);
                foreach (float z in zs)
                {
                    float floor = BunkerShell.FloorY(room, z);
                    foreach (float x in xs)
                    {
                        foreach (float h in heights)
                        {
                            var p = new Vector3(x, floor + h, z);
                            if (!Generator.Contains(p))
                            {
                                probes.Add(p);
                            }
                        }
                    }
                }
            }

            // Door gaps and open passages: each opening once, although both rooms list it.
            var done = new HashSet<string>();
            foreach (Room room in layout.rooms)
            {
                foreach (DoorOpening opening in room.openings)
                {
                    if (!done.Add(opening.id))
                    {
                        continue;
                    }

                    Vector3 centre = BunkerShell.OpeningCentre(layout, room, opening);
                    foreach (float h in DoorHeights)
                    {
                        probes.Add(centre + Vector3.up * h);
                    }
                }
            }

            return probes;
        }

        // ---- Light probes: outdoors -------------------------------------------------------------------------------

        /// <summary>The dock deck, then the trail from the dock to the compound gate.</summary>
        public static List<Vector3> TrailProbes(Func<float, float, float> groundY)
        {
            var probes = new List<Vector3>();
            foreach (float z in Steps(IslandLayout.DockMinZ + 1f, IslandLayout.DockMaxZ - 1f, DockSpacing))
            {
                foreach (float h in OutdoorHeights)
                {
                    probes.Add(new Vector3(0f, IslandLayout.DockDeckY + h, z));
                }
            }

            Vector2[] line = IslandHeightField.TrailPolyline();
            foreach (Vector2[] station in TrailStations(line, TrailSpacing))
            {
                Vector2 point = station[0];
                Vector2 side = new Vector2(-station[1].y, station[1].x); // perpendicular to the trail
                foreach (float offset in new[] { -TrailSideOffset, 0f, TrailSideOffset })
                {
                    Vector2 xz = point + side * offset;
                    float ground = groundY(xz.x, xz.y);
                    foreach (float h in OutdoorHeights)
                    {
                        probes.Add(new Vector3(xz.x, ground + h, xz.y));
                    }
                }
            }

            return probes;
        }

        /// <summary>The compound yard, keeping clear of the lobby and the ramp that cross it.</summary>
        public static List<Vector3> CompoundProbes(BunkerLayout layout, Func<float, float, float> groundY)
        {
            var keepOut = new List<Rect>();
            foreach (string name in new[] { "BunkerEntranceLobby", "BunkerEntranceRamp" })
            {
                Room room = layout.FindRoom(name);
                float margin = layout.wallThickness + WallInset;
                keepOut.Add(Rect.MinMaxRect(room.position.x - room.size.x * 0.5f - margin, room.position.z - room.size.z * 0.5f - margin,
                    room.position.x + room.size.x * 0.5f + margin, room.position.z + room.size.z * 0.5f + margin));
            }

            var probes = new List<Vector3>();
            float[] xs = Steps(IslandLayout.CompoundMinX + 2f, IslandLayout.CompoundMaxX - 2f, CompoundSpacing);
            float[] zs = Steps(IslandLayout.CompoundMinZ + 2f, IslandLayout.CompoundMaxZ - 2f, CompoundSpacing);
            foreach (float z in zs)
            {
                foreach (float x in xs)
                {
                    if (keepOut.Exists(r => r.Contains(new Vector2(x, z))))
                    {
                        continue;
                    }

                    foreach (float h in OutdoorHeights)
                    {
                        probes.Add(new Vector3(x, groundY(x, z) + h, z));
                    }
                }
            }

            return probes;
        }

        // ---- Helpers ----------------------------------------------------------------------------------------------

        /// <summary>
        /// Evenly spaced values from <paramref name="min"/> to <paramref name="max"/>, both included, at most
        /// <paramref name="maxSpacing"/> apart. A range shorter than a centimetre gives its middle.
        /// </summary>
        public static float[] Steps(float min, float max, float maxSpacing)
        {
            if (max - min < 0.01f)
            {
                return new[] { (min + max) * 0.5f };
            }

            int intervals = Mathf.CeilToInt((max - min) / maxSpacing - 1e-4f);
            var values = new float[intervals + 1];
            for (int i = 0; i <= intervals; i++)
            {
                values[i] = Mathf.Lerp(min, max, (float)i / intervals);
            }

            return values;
        }

        /// <summary>Points along a polyline at most <paramref name="spacing"/> apart, both ends included, each with its unit direction.</summary>
        public static List<Vector2[]> TrailStations(Vector2[] line, float spacing)
        {
            float length = 0f;
            for (int i = 0; i < line.Length - 1; i++)
            {
                length += Vector2.Distance(line[i], line[i + 1]);
            }

            int intervals = Mathf.CeilToInt(length / spacing);
            var stations = new List<Vector2[]>();
            int segment = 0;
            float segmentStart = 0f;
            for (int s = 0; s <= intervals; s++)
            {
                float target = length * s / intervals;
                while (segment < line.Length - 2 && segmentStart + Vector2.Distance(line[segment], line[segment + 1]) < target)
                {
                    segmentStart += Vector2.Distance(line[segment], line[segment + 1]);
                    segment++;
                }

                Vector2 a = line[segment];
                Vector2 b = line[segment + 1];
                float t = Mathf.Clamp01((target - segmentStart) / Vector2.Distance(a, b));
                stations.Add(new[] { Vector2.Lerp(a, b, t), (b - a).normalized });
            }

            return stations;
        }

        private static Bounds Bounds(Vector3 min, Vector3 max)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }
}
