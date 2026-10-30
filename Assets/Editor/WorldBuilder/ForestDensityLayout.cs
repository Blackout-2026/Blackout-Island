using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>FD1 supplements the prototype forest; does not replace its scatter or existing authored plants.</summary>
    public static class ForestDensityLayout
    {
        public const int TreeCandidates = 900;
        public const int BushCount = 1100;
        public const int RockCandidates = 240;
        public const float TreeSpacing = 2.6f;

        public static VegetationSet Generate(IReadOnlyList<Vector2> clearings)
        {
            var result = new VegetationSet();
            Place(result.Trees, VegetationKind.Pine, TreeCandidates, 50123, clearings, 3.2f, 4.5f, 7);
            Place(result.Bushes, VegetationKind.Bush, BushCount, 50432, clearings, 1.4f, 3.8f, 2);
            Place(result.Rocks, VegetationKind.Rock, RockCandidates, 50777, clearings, 3f, 4f, 2);
            return result;
        }

        private static void Place(List<VegetationInstance> result, VegetationKind kind, int cap, uint seed,
            IReadOnlyList<Vector2> clearings, float spacing, float trailClearance, int variants)
        {
            var random = new Mulberry32(seed);
            // Generate the same finite plan independently of saved vegetation. A rejected candidate is never
            // replaced by an extra one, so running the append tool again cannot progressively fill every gap.
            for (int i = 0; i < 30000 && result.Count < cap; i++)
            {
                float x = (float)random.Range(-105, 105);
                float z = (float)random.Range(-88, 65);
                double scale = random.Range(kind == VegetationKind.Rock ? 0.65 : 0.78,
                    kind == VegetationKind.Bush ? 1.25 : 1.08);
                double yaw = random.Range(0, 6.283185307);
                int variant = random.Int(0, variants - 1);
                if (IslandHeightField.Sample(x, z) < 2.6f || VegetationScatter.Blocked(x, z)
                    || z > IslandHeightField.CliffLineZ(x) - 6
                    || IslandHeightField.TrailDistance(x, z) < trailClearance
                    || VegetationScatter.GroundNormalY(x, z) < 0.85
                    || VegetationScatter.InClearing(x, z, clearings) || Near(result, x, z, spacing)) continue;
                result.Add(new VegetationInstance(kind, x, z, scale, yaw,
                    kind == VegetationKind.Rock ? 0.05 : 0.12, variant));
            }
        }

        private static bool Near(List<VegetationInstance> instances, float x, float z, float radius)
        {
            foreach (VegetationInstance instance in instances)
                if ((instance.X - x) * (instance.X - x) + (instance.Z - z) * (instance.Z - z) < radius * radius)
                    return true;
            return false;
        }
    }
}
