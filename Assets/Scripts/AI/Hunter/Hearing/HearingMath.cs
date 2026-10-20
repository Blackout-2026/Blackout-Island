// Owner: Systems Engineer (Hunter AI).
namespace Blackout.AI.Hunter
{
    /// <summary>
    /// The hearing formula as pure functions, so it is tested without a scene (docs/reference/systems-engineer.md
    /// section 14). <see cref="HearingConfig"/> documents each constant.
    /// </summary>
    public static class HearingMath
    {
        /// <summary>1 at the noise, falling linearly to 0 at its radius: <c>max(0, 1 − d / r)</c>.</summary>
        public static float OpenSpace(float radius, float distance)
        {
            if (!(radius > 0f))
            {
                return 0f;
            }

            float value = 1f - distance / radius;
            return value > 0f ? value : 0f;
        }

        /// <summary>What gets through <paramref name="walls"/> walls: <c>wallFactor ^ walls</c>.</summary>
        public static float ThroughWalls(int walls, float wallFactor)
        {
            float result = 1f;
            for (int i = 0; i < walls; i++)
            {
                result *= wallFactor;
            }

            return result;
        }

        /// <summary><c>max(0, 1 − d / r) × wallFactor ^ walls × hearing</c>.</summary>
        public static float Perceived(float radius, float distance, int walls, float wallFactor, float hearing)
        {
            return OpenSpace(radius, distance) * ThroughWalls(walls, wallFactor) * hearing;
        }

        public static float Perceived(HearingConfig config, float radius, float distance, int walls)
        {
            return Perceived(radius, distance, walls, config.wallFactor, config.hearing);
        }

        /// <summary>Heard only if strictly above the threshold, as in the prototype (<c>loud &gt; 0.04</c>).</summary>
        public static bool IsAudible(float perceived, float threshold)
        {
            return perceived > threshold;
        }

        /// <summary>
        /// The farthest distance at which a noise of this radius is still heard through this many walls. For the
        /// H4 gizmos and the table in the reference.
        /// </summary>
        public static float MaxHeardDistance(float radius, int walls, float wallFactor, float hearing, float threshold)
        {
            float peak = ThroughWalls(walls, wallFactor) * hearing;
            if (!(radius > 0f) || peak <= threshold)
            {
                return 0f;
            }

            return radius * (1f - threshold / peak);
        }

        /// <summary>
        /// A remembered loudness after <paramref name="age"/> seconds, fading linearly to 0 over
        /// <paramref name="memorySeconds"/>. A negative age counts as 0.
        /// </summary>
        public static float Faded(float loudness, float age, float memorySeconds)
        {
            if (age <= 0f)
            {
                return loudness;
            }

            if (!(memorySeconds > 0f) || age >= memorySeconds)
            {
                return 0f;
            }

            return loudness * (1f - age / memorySeconds);
        }
    }
}
