using System;

namespace Blackout.Editor
{
    /// <summary>
    /// Triangle and material limits for a custom model (docs/art-pipeline.md section 6).
    /// The hard maximum is always 1.5 x the target, so there is one rule to remember.
    /// </summary>
    public readonly struct ModelBudget
    {
        public const float HardMaxFactor = 1.5f;

        public readonly int TargetTriangles;
        public readonly int MaxTriangles;
        public readonly int MaxMaterials;

        public ModelBudget(int targetTriangles, int maxMaterials)
        {
            TargetTriangles = targetTriangles;
            MaxTriangles = (int)(targetTriangles * HardMaxFactor);
            MaxMaterials = maxMaterials;
        }

        // The importer cannot tell a mug from a fuel tank by name, so every SM_Prop_ uses the large-prop limit.
        private static readonly ModelBudget Generator = new ModelBudget(10000, 6);
        private static readonly ModelBudget Robot = new ModelBudget(5000, 4);
        private static readonly ModelBudget Character = new ModelBudget(3000, 3);
        private static readonly ModelBudget Weapon = new ModelBudget(2000, 3);
        private static readonly ModelBudget Prop = new ModelBudget(1500, 2);

        /// <summary>Budget for a model asset name such as "SM_Generator" (extension optional).</summary>
        /// <returns>False when the name has no known budget, so the caller skips the check.</returns>
        public static bool TryGet(string assetName, out ModelBudget budget)
        {
            budget = default;
            if (string.IsNullOrEmpty(assetName))
            {
                return false;
            }

            if (Matches(assetName, "SM_Generator"))
            {
                budget = Generator;
            }
            else if (Matches(assetName, "SM_SecurityRobot"))
            {
                budget = Robot;
            }
            else if (assetName.StartsWith("SM_Char_", StringComparison.Ordinal))
            {
                budget = Character;
            }
            else if (assetName.StartsWith("SM_Weapon_", StringComparison.Ordinal))
            {
                budget = Weapon;
            }
            else if (assetName.StartsWith("SM_Prop_", StringComparison.Ordinal))
            {
                budget = Prop;
            }
            else
            {
                return false;
            }

            return true;
        }

        // "SM_Generator" matches itself and "SM_Generator_Lod1", not "SM_GeneratorCable".
        private static bool Matches(string assetName, string baseName)
        {
            return assetName == baseName || assetName.StartsWith(baseName + "_", StringComparison.Ordinal);
        }
    }
}
