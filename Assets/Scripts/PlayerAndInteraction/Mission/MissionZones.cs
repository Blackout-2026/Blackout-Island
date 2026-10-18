// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Mission
{
    /// <summary>
    /// Where the player is, as boxes from the zone table in docs/level-layout.md. The mission is checked against
    /// these instead of trigger volumes, because the zones live in the World Builder's scene.
    /// </summary>
    public static class MissionZones
    {
        /// <summary>The beach's inland edge: north of it is the forest.</summary>
        public const float ForestEdgeZ = -92f;
        /// <summary>Escape: out of the compound and this far south of it, the escape forest checkpoint takes over.</summary>
        public const float EscapeForestZ = 26f;
        public const float BoatRadius = 6f;
        public const float BoatMinHeight = 0.5f;

        // Underground rooms have their floor at 2.5 and a 3 m ceiling; the generator room's ceiling is 6.5 m.
        private const float BunkerFloor = 2.5f;
        private const float FloorTolerance = 1f;
        private const float RoomTop = BunkerFloor + 3f;
        private const float GeneratorRoomTop = BunkerFloor + 6.5f;

        public static readonly Bounds Compound = Box(-22f, 22f, 7f, 40f, 28f, 62f);
        public static readonly Bounds SecurityArea = Box(-8f, 8f, BunkerFloor - FloorTolerance, RoomTop, 70f, 82f);
        public static readonly Bounds Corridor = Box(-1.5f, 1.5f, BunkerFloor - FloorTolerance, RoomTop, 82.4f, 123.6f);
        public static readonly Bounds GeneratorRoom =
            Box(-19.9f, -1.9f, BunkerFloor - FloorTolerance, GeneratorRoomTop, 96f, 116f);
        public static readonly Bounds Laboratory = Box(-10f, 10f, BunkerFloor - FloorTolerance, RoomTop, 124f, 142f);

        public static bool IsInForest(Vector3 position)
        {
            return position.z > ForestEdgeZ;
        }

        public static bool IsInCompound(Vector3 position)
        {
            return Compound.Contains(position);
        }

        public static bool IsUnderground(Vector3 position)
        {
            return SecurityArea.Contains(position) || Corridor.Contains(position)
                || GeneratorRoom.Contains(position) || Laboratory.Contains(position);
        }

        public static bool IsInGeneratorRoom(Vector3 position)
        {
            return GeneratorRoom.Contains(position);
        }

        /// <summary>Within 6 m of the BoatExit marker (ignoring height) and out of the water.</summary>
        public static bool IsAtBoat(Vector3 position, Vector3 boatExit)
        {
            float dx = position.x - boatExit.x;
            float dz = position.z - boatExit.z;
            return dx * dx + dz * dz < BoatRadius * BoatRadius && position.y > BoatMinHeight;
        }

        private static Bounds Box(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
            return bounds;
        }
    }
}
