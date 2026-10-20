// Owner: Systems Engineer (Hunter AI).
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>Cover geometry for the Hunter (docs/reference/systems-engineer.md section 15).</summary>
    public static class CoverMath
    {
        /// <summary>
        /// The point <paramref name="offset"/> metres behind <paramref name="cover"/> as seen from
        /// <paramref name="threat"/> (flat), as the prototype's tree cover is 0.9 m behind the trunk.
        /// </summary>
        public static Vector3 Behind(Vector3 cover, Vector3 threat, float offset)
        {
            var away = new Vector3(cover.x - threat.x, 0f, cover.z - threat.z);
            if (away.sqrMagnitude < 1e-8f)
            {
                return cover;
            }

            return cover + away.normalized * offset;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }
    }
}
