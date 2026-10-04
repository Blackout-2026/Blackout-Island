using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Outdoor layout numbers shared by the terrain and beach tools, in Unity coordinates (metres, +Z north,
    /// water at y 0). They come from docs/level-layout.md and docs/reference/world-builder.md: if you change a
    /// number here, change it there too. The bunker rooms live in BunkerLayout.asset instead.
    /// </summary>
    public static class IslandLayout
    {
        // Terrain: 400 x 400 m from y -8 to y 24 (reference section 6). 513 samples gives 0.78 m spacing.
        public static readonly Vector3 TerrainOrigin = new Vector3(-200f, -8f, -180f);
        public static readonly Vector3 TerrainSize = new Vector3(400f, 32f, 400f);
        public const int HeightmapResolution = 513;

        public const float WaterY = 0f;
        public const float BeachTopY = 2f; // where the beach slope meets the forest
        public const float PlateauY = 10f; // compound pad, lobby floor

        // Island ellipse (the prototype's centre z -28, negated).
        public const float IslandCentreZ = 28f;
        public const float IslandRadiusX = 118f;
        public const float IslandRadiusZ = 142f;

        // Compound pad: flat at PlateauY. The cliff and hill over the bunker start at its north edge.
        public const float CompoundMinX = -22f;
        public const float CompoundMaxX = 22f;
        public const float CompoundMinZ = 28f;
        public const float CompoundMaxZ = 62f;

        // Beach: the sand slopes this many metres per metre down to the shoreline.
        public const float BeachSlope = 0.115f;

        // Trail from the dock to the compound gate: the prototype's 12 control points with z negated.
        public static readonly Vector2[] TrailControlPoints =
        {
            new Vector2(1f, -104f), new Vector2(0f, -96f), new Vector2(-6f, -85f), new Vector2(4f, -72f),
            new Vector2(12f, -58f), new Vector2(4f, -44f), new Vector2(-9f, -32f), new Vector2(-10f, -16f),
            new Vector2(2f, -3f), new Vector2(9f, 10f), new Vector2(3f, 22f), new Vector2(0f, 30f),
        };

        // Dock: deck x -1.4 .. 1.4, z -128 .. -97, top at y 1.3.
        public const float DockHalfWidth = 1.4f;
        public const float DockMinZ = -128f;
        public const float DockMaxZ = -97f;
        public const float DockDeckY = 1.3f;

        // Boat: prototype (3.7, 0.15, 122.5) with rotY 0.05 rad, so Unity yaw is -2.9 degrees.
        public static readonly Vector3 BoatPosition = new Vector3(3.7f, 0.15f, -122.5f);
        public const float BoatYaw = -2.865f;

        // Boat / exit zone used by the win check.
        public static readonly Vector2 BoatZoneCentre = new Vector2(0f, -124f);
        public const float BoatZoneRadius = 6f;

        // Unity additions (not in the prototype):
        // the sea bed under the boat is kept at least BerthDepth below the water,
        public static readonly Rect Berth = Rect.MinMaxRect(1.6f, -129f, 6.2f, -116f);
        public const float BerthDepth = 1.5f;

        // and the sand under the shore end of the dock is raised to just below the deck, so stepping off the
        // dock is a 5 cm step instead of 33 cm.
        public static readonly Rect DockLanding = Rect.MinMaxRect(-2.4f, -101f, 2.4f, -97f);
        public const float DockLandingBelowDeck = 0.05f;
    }
}
