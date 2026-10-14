using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Checks a model's bounds against the pivot rule of docs/art-pipeline.md section 5 and against its catalog size.
    /// Pure functions: the prefab builder logs the result for an imported FBX, and the tests assert it for every placeholder.
    /// </summary>
    public static class PivotRules
    {
        public const float Tolerance = 0.05f; // metres

        /// <summary>Null when the bounds follow the rule, otherwise what is wrong. Bounds are in the prefab root's axes.</summary>
        public static string CheckPivot(Bounds bounds, PropPivot pivot)
        {
            switch (pivot)
            {
                case PropPivot.BaseCentre:
                    if (Mathf.Abs(bounds.min.y) > Tolerance)
                    {
                        return $"the bottom is at y {bounds.min.y:0.###}, not 0";
                    }

                    return CentredOn(bounds, true, false, true);
                case PropPivot.Centre:
                    return CentredOn(bounds, true, true, true);
                case PropPivot.WallFace:
                    if (Mathf.Abs(bounds.min.z) > Tolerance)
                    {
                        return $"the back face is at z {bounds.min.z:0.###}, not 0 (the wall side)";
                    }

                    return CentredOn(bounds, true, true, false);
                default:
                    return null; // Grip: the weapon system places it, nothing to check
            }
        }

        /// <summary>Null when the size matches the catalog within <see cref="Tolerance"/> on every axis.</summary>
        public static string CheckSize(Bounds bounds, Vector3 expected)
        {
            Vector3 size = bounds.size;
            if (Mathf.Abs(size.x - expected.x) > Tolerance || Mathf.Abs(size.y - expected.y) > Tolerance || Mathf.Abs(size.z - expected.z) > Tolerance)
            {
                return $"size is {size.x:0.##} x {size.y:0.##} x {size.z:0.##} m, expected {expected.x:0.##} x {expected.y:0.##} x {expected.z:0.##} m";
            }

            return null;
        }

        private static string CentredOn(Bounds bounds, bool x, bool y, bool z)
        {
            Vector3 c = bounds.center;
            if ((x && Mathf.Abs(c.x) > Tolerance) || (y && Mathf.Abs(c.y) > Tolerance) || (z && Mathf.Abs(c.z) > Tolerance))
            {
                return $"the centre is at ({c.x:0.###}, {c.y:0.###}, {c.z:0.###}), not on the pivot";
            }

            return null;
        }
    }
}
