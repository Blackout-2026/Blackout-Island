// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Hud
{
    /// <summary>Metres, +Z north, +X east. Headings increase clockwise: N=0, E=90, S=180, W=270.</summary>
    public static class CompassMath
    {
        public const float PixelsPerDegree = 2f;
        public const float MarkerLimit = 112f;

        public static float Bearing(Vector3 direction) =>
            Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360f);

        public static float MarkerOffset(float heading, Vector3 direction) =>
            Mathf.Clamp(Mathf.DeltaAngle(heading, Bearing(direction)), -MarkerLimit, MarkerLimit) * PixelsPerDegree;
    }
}
