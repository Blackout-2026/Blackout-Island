using System.Collections.Generic;
using Blackout.Shared;
using Blackout.World;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>One gameplay marker: its type, name (also its Marker id), owner, position and facing.</summary>
    public readonly struct MarkerSpec
    {
        public readonly MarkerType Type;
        public readonly string Name;
        public readonly string Owner; // the agent's name for AgentSpawn and Waypoint markers, empty otherwise
        public readonly Vector3 Position; // on the walking surface
        public readonly float Yaw; // degrees, 0 = north (+Z), 90 = east

        public MarkerSpec(MarkerType type, string name, string owner, Vector3 position, float yaw)
        {
            Type = type;
            Name = name;
            Owner = owner;
            Position = position;
            Yaw = yaw;
        }
    }

    /// <summary>
    /// Every gameplay marker in Environment.unity, in Unity coordinates, from docs/level-layout.md and
    /// docs/reference/world-builder.md section 2. Data plus one rule for heights, with no scene access, so tests can
    /// check it.
    /// - A marker's id is its GameObject name, for example Checkpoint_Beach or Waypoint_Patrol-1_02.
    /// - Agents use the prototype's names (Hunter-1, Patrol-1, TG-01, Commander), as the Agent Controller's AgentRoot
    ///   does; the name is the Owner of the agent's AgentSpawn and Waypoint markers. Waypoints are numbered in route
    ///   order, and every route is a loop.
    /// - y is the walking surface (<see cref="GroundY"/>): the dock deck, a bunker room's floor, or the terrain.
    /// - Corridor points at x +-1.2 in the reference move to +-0.9, clear of the 0.3 m pillars (W1).
    /// - Nothing goes in the barricade crate's space in the corridor.
    /// </summary>
    public static class MarkerLayout
    {
        /// <summary>Kept clear for the Systems Engineer's barricade crate: a 1.5 m cube at x 0.7 (moved from 0.95, W1).</summary>
        public static readonly Vector3 BarricadeCrateCentre = new Vector3(0.7f, 2.5f, 95f);

        public const float BarricadeCrateSize = 1.5f;

        // Agent name, spawn (x, z), spawn yaw (null = face the second waypoint), route or post (x, z) in order.
        // Reference section 2, "Agent spawns and routes".
        private static readonly (string name, Vector2 spawn, float? yaw, Vector2[] route)[] Agents =
        {
            ("Hunter-1", P(-24f, -54f), null, new[] { P(-24f, -54f), P(-30f, -30f), P(-18f, -12f), P(-26f, 8f), P(-14f, -70f), P(-36f, -44f) }),
            ("Hunter-2", P(26f, -20f), null, new[] { P(26f, -20f), P(30f, -44f), P(20f, -64f), P(32f, -2f), P(18f, 14f), P(36f, -30f) }),
            ("Patrol-1", P(-14f, 36f), null, new[] { P(-14f, 36f), P(14f, 36f), P(16f, 46f), P(10f, 55f), P(-10f, 55f), P(-16f, 46f) }),
            ("Patrol-2", P(3f, 74f), null, new[] { P(3f, 74f), P(-4f, 74f), P(-4f, 79.5f), P(3f, 79.5f) }),

            // A ping-pong route in the reference: its return through (0, 102) is stored as a 4th point.
            ("Patrol-3", P(0f, 88f), null, new[] { P(0f, 88f), P(0f, 102f), P(-0.5f, 120f), P(0f, 102f) }),

            // Guards on a post face the door of their room; the Commander faces south as in the reference.
            ("TG-01", P(-8f, 100f), 45f, new[] { P(-8f, 100f) }), // toward D-Generator
            ("TG-02", P(3f, 129f), 210f, new[] { P(2.5f, 129f) }), // toward D-Lab
            ("Commander", P(6f, 138f), 180f, new[] { P(6f, 138f) }),
        };

        // Reference section 2, "Cover points": 1-4 generator corners, 5-12 generator room (tanks, cabinets, stairs),
        // 13-16 lab benches, 17-19 corridor pillars.
        private static readonly Vector2[] CoverPoints =
        {
            P(-14.8f, 101.2f), P(-7.2f, 101.2f), P(-14.8f, 110.8f), P(-7.2f, 110.8f),
            P(-17.5f, 99f), P(-4.5f, 113.5f), P(-18f, 110f), P(-11f, 109.4f),
            P(-15.5f, 108.6f), P(-16.8f, 106f), P(-9f, 97.5f), P(-13f, 97.5f),
            P(-6.5f, 130.5f), P(6.5f, 130.5f), P(-6.5f, 137f), P(6.5f, 137f),
            P(-0.9f, 92.5f), P(0.9f, 110f), P(-0.9f, 117f),
        };

        private const float CatwalkDeckY = 5.5f; // generator room catwalk, built by GeneratorCatwalkBuilder (W13)

        public static List<MarkerSpec> Build(BunkerLayout layout)
        {
            var markers = new List<MarkerSpec>();

            // Player start and checkpoints (reference "Player start and checkpoints"). The beach checkpoint is the
            // start on the dock; the two escape checkpoints keep a death near the boat from sending the player back
            // underground.
            Add(markers, layout, MarkerType.PlayerStart, "PlayerStart", "", 0f, -121f, 0f);
            Add(markers, layout, MarkerType.Checkpoint, "Checkpoint_Beach", "", 0f, -121f, 0f);
            Add(markers, layout, MarkerType.Checkpoint, "Checkpoint_Forest", "", 0f, -90f, 0f);
            Add(markers, layout, MarkerType.Checkpoint, "Checkpoint_BunkerEntrance", "", 0f, 33f, 0f);
            Add(markers, layout, MarkerType.Checkpoint, "Checkpoint_GeneratorRoom", "", -4.5f, 106f, 270f);
            Add(markers, layout, MarkerType.Checkpoint, "Checkpoint_EscapeEntrance", "", 0f, 40f, 180f);
            Add(markers, layout, MarkerType.Checkpoint, "Checkpoint_EscapeForest", "", 0f, 24f, 180f);

            foreach ((string name, Vector2 spawn, float? yaw, Vector2[] route) in Agents)
            {
                float spawnYaw = yaw ?? YawTowards(spawn, route[1]);
                Add(markers, layout, MarkerType.AgentSpawn, $"AgentSpawn_{name}", name, spawn.x, spawn.y, spawnYaw);
                for (int i = 0; i < route.Length; i++)
                {
                    Add(markers, layout, MarkerType.Waypoint, $"Waypoint_{name}_{i + 1:00}", name, route[i].x, route[i].y, 0f);
                }
            }

            for (int i = 0; i < CoverPoints.Length; i++)
            {
                Add(markers, layout, MarkerType.CoverPoint, $"CoverPoint_{i + 1:00}", "", CoverPoints[i].x, CoverPoints[i].y, 0f);
            }

            AddDoorSockets(layout, markers);

            // Objectives, pickups and the exit (reference "Objectives and exit", "Pickups and interactables").
            Add(markers, layout, MarkerType.GeneratorSocket, "GeneratorSocket", "", -11f, 106f, 0f); // long side along X
            Add(markers, layout, MarkerType.CureSocket, "CureSocket", "", 0f, 135f, 180f); // chamber opening faces D-Lab
            Add(markers, layout, MarkerType.HealthPickup, "HealthPickup_Security", "", 6.5f, 80.5f, 0f);
            Add(markers, layout, MarkerType.HealthPickup, "HealthPickup_Corridor", "", 0.9f, 119f, 0f);
            markers.Add(new MarkerSpec(MarkerType.HealthPickup, "HealthPickup_Catwalk", "", new Vector3(-18.5f, CatwalkDeckY, 114.5f), 0f));
            Add(markers, layout, MarkerType.BoatExit, "BoatExit", "", IslandLayout.BoatZoneCentre.x, IslandLayout.BoatZoneCentre.y, 0f);
            return markers;
        }

        /// <summary>The walking surface at (x, z): the dock deck, a bunker room's floor, or the island terrain.</summary>
        public static float GroundY(BunkerLayout layout, float x, float z)
        {
            if (Mathf.Abs(x) <= IslandLayout.DockHalfWidth && z >= IslandLayout.DockMinZ && z <= IslandLayout.DockMaxZ)
            {
                return IslandLayout.DockDeckY;
            }

            foreach (Room room in layout.rooms)
            {
                if (Mathf.Abs(x - room.position.x) <= room.size.x * 0.5f && Mathf.Abs(z - room.position.z) <= room.size.z * 0.5f)
                {
                    return BunkerShell.FloorY(room, z);
                }
            }

            return IslandHeightField.Sample(x, z);
        }

        // One DoorSocket in each door gap (open passages get none). It faces into the room you enter on the way in:
        // the later of the two rooms in the layout, or the room itself for the door from outside.
        private static void AddDoorSockets(BunkerLayout layout, List<MarkerSpec> markers)
        {
            var order = new List<string>();
            var sockets = new Dictionary<string, MarkerSpec>();
            foreach (Room room in layout.rooms)
            {
                foreach (DoorOpening opening in room.openings)
                {
                    if (opening.kind == OpeningKind.Open)
                    {
                        continue;
                    }

                    if (!sockets.ContainsKey(opening.id))
                    {
                        order.Add(opening.id);
                    }

                    Vector3 centre = BunkerShell.OpeningCentre(layout, room, opening);
                    sockets[opening.id] = new MarkerSpec(MarkerType.DoorSocket, $"DoorSocket_{opening.id}", "", centre, InwardYaw(opening.wall));
                }
            }

            foreach (string id in order)
            {
                markers.Add(sockets[id]);
            }
        }

        private static void Add(List<MarkerSpec> markers, BunkerLayout layout, MarkerType type, string name, string owner,
            float x, float z, float yaw)
        {
            markers.Add(new MarkerSpec(type, name, owner, new Vector3(x, GroundY(layout, x, z), z), yaw));
        }

        // Facing from a room's wall into the room.
        private static float InwardYaw(WallSide wall)
        {
            switch (wall)
            {
                case WallSide.South:
                    return 0f;
                case WallSide.West:
                    return 90f;
                case WallSide.North:
                    return 180f;
                default:
                    return 270f;
            }
        }

        private static float YawTowards(Vector2 from, Vector2 to)
        {
            float yaw = Mathf.Atan2(to.x - from.x, to.y - from.y) * Mathf.Rad2Deg;
            return Mathf.Round(yaw < 0f ? yaw + 360f : yaw);
        }

        private static Vector2 P(float x, float z)
        {
            return new Vector2(x, z);
        }
    }
}
