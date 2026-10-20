using UnityEngine;

namespace Blackout.Editor
{
    public enum PropSize
    {
        Small,
        Medium,
        Large,
    }

    /// <summary>
    /// The three prop size classes of docs/art-pipeline.md section 6: small under 0.5 m, medium up to 1.5 m, large above.
    /// The class is read from the longest side, and it picks the triangle budget the Props palette compares against.
    /// </summary>
    public static class PropSizes
    {
        public const float SmallMax = 0.5f;
        public const float MediumMax = 1.5f;

        public static PropSize Classify(Vector3 size)
        {
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (longest < SmallMax)
            {
                return PropSize.Small;
            }

            return longest <= MediumMax ? PropSize.Medium : PropSize.Large;
        }

        // Targets are 1,200 / 2,000 / 3,000 triangles; the hard maximum is 1.5 x that (ModelBudget). Four materials each (step C7: the
        // props are modelled like the real objects, with drawers, louvres, hoops and fittings, so they need more than the stand-ins did).
        public static ModelBudget BudgetOf(PropSize size)
        {
            switch (size)
            {
                case PropSize.Small: return new ModelBudget(1200, 4);
                case PropSize.Medium: return new ModelBudget(2000, 4);
                default: return new ModelBudget(3000, 4);
            }
        }
    }
}
