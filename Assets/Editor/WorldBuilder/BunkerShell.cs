using System.Collections.Generic;
using Blackout.World;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>One cube of a room's shell in world space. Pitch tilts it about X (only the ramp's floor and ceiling).</summary>
    public readonly struct ShellPart
    {
        public readonly string Name;
        public readonly Vector3 Centre;
        public readonly Vector3 Size;
        public readonly float Pitch;

        public ShellPart(string name, Vector3 centre, Vector3 size, float pitch = 0f)
        {
            Name = name;
            Centre = centre;
            Size = size;
            Pitch = pitch;
        }

        public Quaternion Rotation => Quaternion.Euler(Pitch, 0f, 0f);

        /// <summary>True if the world point is inside the box, or within <paramref name="tolerance"/> of it.</summary>
        public bool Contains(Vector3 point, float tolerance = 0.001f)
        {
            Vector3 local = Quaternion.Inverse(Rotation) * (point - Centre);
            Vector3 half = Size * 0.5f;
            return Mathf.Abs(local.x) <= half.x + tolerance
                && Mathf.Abs(local.y) <= half.y + tolerance
                && Mathf.Abs(local.z) <= half.z + tolerance;
        }
    }

    /// <summary>
    /// Works out the cubes that make up each room in BunkerLayout.asset. Pure geometry with no scene access, so
    /// EditMode tests can check it. The rules:
    /// - Floor and ceiling slabs cover the clear interior only. The ramp's are tilted to its slope, and its ceiling
    ///   stays the room height above the floor, measured vertically.
    /// - Each wall is a band one wall-thickness wide just outside the interior, from the bottom of the floor slab to
    ///   the top of the ceiling slab. North and south walls include the corners; east and west walls stop at them.
    /// - An opening is a gap from the floor up to its height. A lintel fills the wall above it and a sill fills the
    ///   slab band below it, so the floor runs straight through the doorway.
    /// - Rooms that touch share one wall band, and that stretch is built once: by the room whose wall is taller (it
    ///   then covers the other's full height), else by the longer wall, else by the room listed first.
    /// </summary>
    public static class BunkerShell
    {
        private const float Epsilon = 0.001f;

        private static readonly WallSide[] Sides = { WallSide.North, WallSide.South, WallSide.East, WallSide.West };

        /// <summary>A wall band of one room: where it is across the wall, where it runs along it, and its height.</summary>
        private struct Wall
        {
            public WallSide Side;
            public float Thickness;
            public float Across; // centre of the band: z for North/South walls, x for East/West walls
            public float AlongMin; // x for North/South walls (corners included), z for East/West walls
            public float AlongMax;
            public float OuterMin; // the room's whole extent along this wall, corners included
            public float OuterMax;
            public float Bottom; // bottom of the floor slab
            public float Top; // top of the ceiling slab
        }

        /// <summary>The cubes for one room: floor, ceiling, then each wall's pieces, lintels and sills.</summary>
        public static List<ShellPart> BuildRoom(BunkerLayout layout, Room room)
        {
            var parts = new List<ShellPart>();
            AddSlabs(room, layout.wallThickness, parts);
            foreach (WallSide side in Sides)
            {
                AddWall(layout, room, side, parts);
            }

            return parts;
        }

        /// <summary>Floor height at this z. A room with floorDropNorth slopes down toward the north.</summary>
        public static float FloorY(Room room, float z)
        {
            float minZ = room.position.z - room.size.z * 0.5f;
            float fraction = Mathf.Clamp01((z - minZ) / room.size.z);
            return room.position.y + room.floorDropNorth * (0.5f - fraction);
        }

        /// <summary>Centre of an opening at floor level, in the middle of the wall's thickness (docs/level-layout.md).</summary>
        public static Vector3 OpeningCentre(BunkerLayout layout, Room room, DoorOpening opening)
        {
            Wall wall = GetWall(room, opening.wall, layout.wallThickness);
            float along = AlongCentre(room, opening);
            float floor = FloorAtOpening(room, opening.wall, along);
            return IsNorthSouth(opening.wall)
                ? new Vector3(along, floor, wall.Across)
                : new Vector3(wall.Across, floor, along);
        }

        private static void AddSlabs(Room room, float thickness, List<ShellPart> parts)
        {
            Vector3 p = room.position;
            Vector3 size = room.size;
            if (Mathf.Abs(room.floorDropNorth) < Epsilon)
            {
                parts.Add(new ShellPart("Floor", new Vector3(p.x, p.y - thickness * 0.5f, p.z), new Vector3(size.x, thickness, size.z)));
                parts.Add(new ShellPart("Ceiling", new Vector3(p.x, p.y + size.y + thickness * 0.5f, p.z), new Vector3(size.x, thickness, size.z)));
                return;
            }

            // Tilted about X so local +Z runs north and down the slope; local +Y is then (0, cos, sin).
            float pitch = Mathf.Atan2(room.floorDropNorth, size.z);
            float length = Mathf.Sqrt(size.z * size.z + room.floorDropNorth * room.floorDropNorth);
            var slabUp = new Vector3(0f, Mathf.Cos(pitch), Mathf.Sin(pitch));
            var slabSize = new Vector3(size.x, thickness, length);
            parts.Add(new ShellPart("Floor", p - slabUp * (thickness * 0.5f), slabSize, pitch * Mathf.Rad2Deg));
            parts.Add(new ShellPart("Ceiling", p + Vector3.up * size.y + slabUp * (thickness * 0.5f), slabSize, pitch * Mathf.Rad2Deg));
        }

        private static void AddWall(BunkerLayout layout, Room room, WallSide side, List<ShellPart> parts)
        {
            Wall wall = GetWall(room, side, layout.wallThickness);
            var spans = new List<Vector2> { new Vector2(wall.AlongMin, wall.AlongMax) };

            // Leave out the stretches a neighbouring room builds (its walls and corners cover them).
            int ownIndex = layout.rooms.IndexOf(room);
            for (int i = 0; i < layout.rooms.Count; i++)
            {
                if (i == ownIndex)
                {
                    continue;
                }

                Wall facing = GetWall(layout.rooms[i], Opposite(side), layout.wallThickness);
                if (Mathf.Abs(facing.Across - wall.Across) < Epsilon && OtherBuildsShared(facing, i, wall, ownIndex))
                {
                    Subtract(spans, facing.OuterMin, facing.OuterMax);
                }
            }

            // Cut a gap for each opening on this wall that falls in a stretch this room builds.
            string code = side.ToString().Substring(0, 1); // N, S, E or W
            var gapParts = new List<ShellPart>();
            foreach (DoorOpening opening in room.openings)
            {
                if (opening.wall != side)
                {
                    continue;
                }

                float along = AlongCentre(room, opening);
                float floor = FloorAtOpening(room, side, along);
                foreach (Vector2 span in spans.ToArray())
                {
                    float gapMin = Mathf.Max(along - opening.width * 0.5f, span.x);
                    float gapMax = Mathf.Min(along + opening.width * 0.5f, span.y);
                    if (gapMax - gapMin < Epsilon)
                    {
                        continue;
                    }

                    Subtract(spans, gapMin, gapMax);
                    AddPiece(gapParts, $"Lintel_{code}_{opening.id}", wall, gapMin, gapMax, floor + opening.height, wall.Top);
                    AddPiece(gapParts, $"Sill_{code}_{opening.id}", wall, gapMin, gapMax, floor - wall.Thickness, floor);
                }
            }

            for (int i = 0; i < spans.Count; i++)
            {
                AddPiece(parts, $"Wall_{code}_{i}", wall, spans[i].x, spans[i].y, wall.Bottom, wall.Top);
            }

            parts.AddRange(gapParts);
        }

        // The other room builds the shared stretch if its wall covers this wall's full height and is taller, or is
        // as tall and longer, or is the same size and listed first. Exactly one of two facing walls wins.
        private static bool OtherBuildsShared(Wall other, int otherIndex, Wall own, int ownIndex)
        {
            bool covers = other.Bottom <= own.Bottom + Epsilon && other.Top >= own.Top - Epsilon;
            if (!covers)
            {
                return false;
            }

            if (other.Top - other.Bottom > own.Top - own.Bottom + Epsilon)
            {
                return true;
            }

            float otherLength = other.OuterMax - other.OuterMin;
            float ownLength = own.OuterMax - own.OuterMin;
            if (Mathf.Abs(otherLength - ownLength) > Epsilon)
            {
                return otherLength > ownLength;
            }

            return otherIndex < ownIndex;
        }

        private static Wall GetWall(Room room, WallSide side, float thickness)
        {
            float minX = room.position.x - room.size.x * 0.5f;
            float maxX = room.position.x + room.size.x * 0.5f;
            float minZ = room.position.z - room.size.z * 0.5f;
            float maxZ = room.position.z + room.size.z * 0.5f;
            float southFloor = FloorY(room, minZ);
            float northFloor = FloorY(room, maxZ);
            float half = thickness * 0.5f;

            var wall = new Wall { Side = side, Thickness = thickness };
            if (IsNorthSouth(side))
            {
                float floor = side == WallSide.North ? northFloor : southFloor;
                wall.Across = side == WallSide.North ? maxZ + half : minZ - half;
                wall.AlongMin = wall.OuterMin = minX - thickness;
                wall.AlongMax = wall.OuterMax = maxX + thickness;
                wall.Bottom = floor - thickness;
                wall.Top = floor + room.size.y + thickness;
            }
            else
            {
                // On a sloped room the side walls are upright rectangles reaching from the lowest floor slab to the
                // highest ceiling slab, as in the prototype; the part outside the room is buried in the hill.
                wall.Across = side == WallSide.East ? maxX + half : minX - half;
                wall.AlongMin = minZ;
                wall.AlongMax = maxZ;
                wall.OuterMin = minZ - thickness;
                wall.OuterMax = maxZ + thickness;
                wall.Bottom = Mathf.Min(southFloor, northFloor) - thickness;
                wall.Top = Mathf.Max(southFloor, northFloor) + room.size.y + thickness;
            }

            return wall;
        }

        private static void AddPiece(List<ShellPart> parts, string name, Wall wall, float alongMin, float alongMax, float bottom, float top)
        {
            if (alongMax - alongMin < Epsilon || top - bottom < Epsilon)
            {
                return;
            }

            float along = (alongMin + alongMax) * 0.5f;
            float y = (bottom + top) * 0.5f;
            parts.Add(IsNorthSouth(wall.Side)
                ? new ShellPart(name, new Vector3(along, y, wall.Across), new Vector3(alongMax - alongMin, top - bottom, wall.Thickness))
                : new ShellPart(name, new Vector3(wall.Across, y, along), new Vector3(wall.Thickness, top - bottom, alongMax - alongMin)));
        }

        // Removes [cutMin, cutMax] from a sorted list of spans, splitting a span if the cut falls inside it.
        private static void Subtract(List<Vector2> spans, float cutMin, float cutMax)
        {
            for (int i = spans.Count - 1; i >= 0; i--)
            {
                Vector2 span = spans[i];
                if (cutMax <= span.x + Epsilon || cutMin >= span.y - Epsilon)
                {
                    continue;
                }

                spans.RemoveAt(i);
                if (span.y - cutMax > Epsilon)
                {
                    spans.Insert(i, new Vector2(cutMax, span.y));
                }

                if (cutMin - span.x > Epsilon)
                {
                    spans.Insert(i, new Vector2(span.x, cutMin));
                }
            }
        }

        private static float AlongCentre(Room room, DoorOpening opening)
        {
            return (IsNorthSouth(opening.wall) ? room.position.x : room.position.z) + opening.offset;
        }

        private static float FloorAtOpening(Room room, WallSide side, float along)
        {
            switch (side)
            {
                case WallSide.North:
                    return FloorY(room, room.position.z + room.size.z * 0.5f);
                case WallSide.South:
                    return FloorY(room, room.position.z - room.size.z * 0.5f);
                default:
                    return FloorY(room, along);
            }
        }

        private static bool IsNorthSouth(WallSide side)
        {
            return side == WallSide.North || side == WallSide.South;
        }

        private static WallSide Opposite(WallSide side)
        {
            switch (side)
            {
                case WallSide.North:
                    return WallSide.South;
                case WallSide.South:
                    return WallSide.North;
                case WallSide.East:
                    return WallSide.West;
                default:
                    return WallSide.East;
            }
        }
    }
}
