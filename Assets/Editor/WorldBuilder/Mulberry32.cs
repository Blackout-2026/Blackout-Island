using System;

namespace Blackout.Editor
{
    /// <summary>
    /// The prototype's seeded random number generator (mulberry32, U.rng in prototype/src/core/namespace.js), ported
    /// bit for bit: the same seed gives the same sequence as the prototype, so a scatter that draws numbers in the
    /// prototype's order places the same forest. Not UnityEngine.Random, which is global state and not seeded per tool.
    /// </summary>
    public sealed class Mulberry32
    {
        private uint state;

        public Mulberry32(uint seed)
        {
            state = seed;
        }

        /// <summary>The next number in [0, 1).</summary>
        public double Next()
        {
            // uint arithmetic wraps like JavaScript's Math.imul and ">>> 0", so the bits match the prototype.
            unchecked
            {
                state += 0x6D2B79F5u;
                uint t = state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>A number in [min, max), like the prototype's rng.range.</summary>
        public double Range(double min, double max)
        {
            return min + (max - min) * Next();
        }

        /// <summary>An integer in [min, max] (both included), like the prototype's rng.int.</summary>
        public int Int(int min, int max)
        {
            return (int)Math.Floor(min + (max - min + 1) * Next());
        }
    }
}
