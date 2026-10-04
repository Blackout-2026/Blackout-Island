using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blackout.World
{
    /// <summary>Which wall of a room an opening is cut into. North is +Z, East is +X.</summary>
    public enum WallSide
    {
        North,
        South,
        East,
        West,
    }

    /// <summary>What fills an opening. Doors are built by the Systems Engineer; Open is a bare passage.</summary>
    public enum OpeningKind
    {
        Open,
        SlideDoor,
        HingeDoor,
        MaglockDoor, // sliding door that stays locked until the generator is disabled
    }

    /// <summary>A hole in one wall of a room (a door or an open passage).</summary>
    [Serializable]
    public class DoorOpening
    {
        [Tooltip("Shared by both rooms when the opening is in a wall they share, e.g. D-Lab.")]
        public string id;

        public WallSide wall;

        [Tooltip("Metres from the middle of the wall: along +X on North/South walls, along +Z on East/West walls.")]
        public float offset;

        [Tooltip("Clear width in metres.")]
        public float width = 1.4f;

        [Tooltip("Clear height in metres, measured from the floor.")]
        public float height = 2.3f;

        public OpeningKind kind;

        [Tooltip("Name of the room (or zone) on the other side.")]
        public string connectsTo;
    }

    /// <summary>One box-shaped room of the bunker. Walls sit outside the interior box.</summary>
    [Serializable]
    public class Room
    {
        public string name;

        [Tooltip("Centre of the floor: x and z are the room centre, y is the floor height there.")]
        public Vector3 position;

        [Tooltip("Clear interior size: width (X), ceiling height above the floor (Y), depth (Z).")]
        public Vector3 size;

        [Tooltip("How far the floor drops from the south edge to the north edge. 0 for flat rooms; the ramp uses 7.5.")]
        public float floorDropNorth;

        public List<DoorOpening> openings = new List<DoorOpening>();
    }

    /// <summary>
    /// Data-only description of the bunker rooms, read by the World Builder's editor tools.
    /// The numbers come from docs/level-layout.md; it holds no logic so it is easy to check against that table.
    /// </summary>
    [CreateAssetMenu(fileName = "BunkerLayout", menuName = "Blackout/World/Bunker Layout")]
    public class BunkerLayout : ScriptableObject
    {
        [Tooltip("Thickness of every wall, floor and ceiling slab in metres.")]
        public float wallThickness = 0.4f;

        public List<Room> rooms = new List<Room>();

        /// <summary>Returns the room with this exact name, or null.</summary>
        public Room FindRoom(string roomName)
        {
            foreach (Room room in rooms)
            {
                if (string.Equals(room.name, roomName, StringComparison.Ordinal))
                {
                    return room;
                }
            }

            return null;
        }
    }
}
