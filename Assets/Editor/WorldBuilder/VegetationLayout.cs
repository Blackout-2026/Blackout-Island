namespace Blackout.Editor
{
    /// <summary>
    /// Vegetation numbers for the island: seeds, scatter areas, spacing rules and caps from
    /// docs/reference/world-builder.md section 5 (the prototype's forest.js), plus the Unity clearings. If you change a
    /// number here, change it in docs/level-layout.md too.
    /// Scatter areas are given in the prototype's coordinates (Pz = prototype z = -Unity z), because the scatter loops
    /// over them in the prototype's order: that keeps the random sequence, and so the forest, the same as the prototype.
    /// </summary>
    public static class VegetationLayout
    {
        // Pines and palms: one candidate per cell of a jittered 4.6 m grid (seed 1234).
        public const uint TreeSeed = 1234;
        public const double TreeGridStep = 4.6;
        public const double TreeGridMinX = -118;
        public const double TreeGridMaxX = 118;
        public const double TreeGridMinPz = -175;
        public const double TreeGridMaxPz = 100;
        public const double TreeJitter = 0.9; // of a cell
        public const double TreeMinGround = 1.0; // metres above the water
        public const double TreeTrailClearance = 3.4; // from the trail's centre line
        public const double TreeMinNormalY = 0.8; // slopes up to about 37 degrees
        public const double TreeMinScale = 0.78;
        public const double TreeScaleRange = 0.5; // so 0.78 .. 1.28
        public const double PalmBeachMaxGround = 3.2; // low ground near the beach: palms only, 45 % kept
        public const double PalmBeachMaxZ = -60; // Unity z (prototype z > 60)
        public const double PalmBeachKeep = 0.55;
        public const double PalmInlandMaxZ = -35; // Unity z (prototype z > 35)
        public const double PalmInlandMaxGround = 5.5;
        public const double PalmInlandChance = 0.18;

        // Bushes (seed 4321): up to 420 from 2600 tries.
        public const uint BushSeed = 4321;
        public const int BushTries = 2600;
        public const int BushCap = 420;
        public const double BushMaxX = 90;
        public const double BushMinPz = -60;
        public const double BushMaxPz = 95;
        public const double BushTrailMin = 3.8;
        public const double BushTrailMax = 45;
        public const double BushMinGround = 1.6;

        // Grass tufts (seed 999): up to 6000 from 30000 tries, in noise clumps or lining the trail.
        public const uint GrassSeed = 999;
        public const int GrassTries = 30000;
        public const int GrassCap = 6000;
        public const double GrassMaxX = 80;
        public const double GrassMinPz = -62;
        public const double GrassMaxPz = 98;
        public const double GrassTrailMin = 1.4;
        public const double GrassTrailMax = 38;
        public const double GrassMinGround = 1.3;
        public const double GrassClumpMin = 0.45; // noise threshold for a clump
        public const double GrassTrailLining = 4; // within this of the trail, grass grows outside clumps too

        // Rocks (seed 777): up to 170 from 1500 tries. Boulders along the cliff, small rocks elsewhere (45 % kept).
        public const uint RockSeed = 777;
        public const int RockTries = 1500;
        public const int RockCap = 170;
        public const double RockMaxX = 110;
        public const double RockMinPz = -120;
        public const double RockMaxPz = 104;
        public const double RockMinGround = -0.5;
        public const double RockCliffBand = 6; // metres either side of the cliff line
        public const double RockTrailClearance = 3;
        public const double RockSkipChance = 0.55; // away from the cliff
        public const float RockColliderMinScale = 0.7f; // smaller rocks have no collider (prototype)

        // Unity addition: no tree, rock or bush within this distance of a Hunter's waypoints and spawn. A pine's
        // lowest branches reach 2.4 m x 1.28 = 3.1 m from its trunk, so 6 m leaves about 3 m of open ground around the
        // point: room for the Hunter to turn and for the player to see it there. Grass is kept.
        public const float ClearingRadius = 6f;
    }
}
