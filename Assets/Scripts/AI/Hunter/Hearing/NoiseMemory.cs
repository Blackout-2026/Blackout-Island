// Owner: Systems Engineer (Hunter AI).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// The one noise the Hunter remembers: the strongest recent one, with the time it was heard, fading to 0 over
    /// <see cref="MemorySeconds"/> (docs/reference/systems-engineer.md section 14). A new noise replaces it if it is at
    /// least as loud as what is left of the old one, so a running player's footsteps keep the position up to date.
    /// Plain C#, with time passed in, so it is tested without a scene.
    /// </summary>
    public sealed class NoiseMemory
    {
        private bool remembered;

        public NoiseMemory(float memorySeconds)
        {
            MemorySeconds = memorySeconds;
        }

        public float MemorySeconds { get; }

        /// <summary>Where the remembered noise was made. Only meaningful while <see cref="HasNoise"/> is true.</summary>
        public Vector3 Position { get; private set; }
        public NoiseType Type { get; private set; }
        /// <summary>The perceived loudness when it was heard, before fading.</summary>
        public float HeardLoudness { get; private set; }
        /// <summary>The time it was heard, on the clock passed to <see cref="Offer"/>.</summary>
        public float HeardAt { get; private set; }

        /// <summary>The remembered loudness at <paramref name="time"/>, after fading; 0 if nothing is remembered.</summary>
        public float LoudnessAt(float time)
        {
            return remembered ? HearingMath.Faded(HeardLoudness, time - HeardAt, MemorySeconds) : 0f;
        }

        /// <summary>True while a remembered noise has not faded to 0.</summary>
        public bool HasNoise(float time)
        {
            return LoudnessAt(time) > 0f;
        }

        /// <summary>Seconds since the remembered noise was heard, or infinity if nothing has been heard.</summary>
        public float TimeSince(float time)
        {
            return remembered ? Mathf.Max(0f, time - HeardAt) : float.PositiveInfinity;
        }

        /// <summary>Keeps the noise if it is at least as loud as the faded memory. Returns true if it was kept.</summary>
        public bool Offer(Vector3 position, NoiseType type, float perceived, float time)
        {
            if (!(perceived > 0f) || perceived < LoudnessAt(time))
            {
                return false;
            }

            remembered = true;
            Position = position;
            Type = type;
            HeardLoudness = perceived;
            HeardAt = time;
            return true;
        }

        public void Clear()
        {
            remembered = false;
        }
    }
}
