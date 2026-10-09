// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using UnityEngine;

namespace Blackout.Shared
{
    public enum NoiseType
    {
        Footstep,
        Sprint,
        Gunshot,
        Melee,
        Door,
        Generator
    }

    /// <summary>
    /// A sound the AI can hear. A struct, so publishing one allocates nothing.
    /// </summary>
    public readonly struct NoiseEvent
    {
        public readonly Vector3 position;
        /// <summary>
        /// Radius in metres at which the noise is heard in open space (the prototype's noise radius).
        /// Listeners such as the Hunter's HearingSystem apply their own falloff and wall attenuation.
        /// </summary>
        public readonly float loudness;
        public readonly NoiseType type;
        /// <summary>Who made the noise, so agents can ignore their own sounds. May be null.</summary>
        public readonly GameObject source;

        public NoiseEvent(Vector3 position, float loudness, NoiseType type, GameObject source)
        {
            this.position = position;
            this.loudness = loudness;
            this.type = type;
            this.source = source;
        }
    }
}
