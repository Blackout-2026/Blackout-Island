// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>Carries every <see cref="NoiseEvent"/> (footsteps, gunshots, doors, generator) to the agents' hearing.</summary>
    [CreateAssetMenu(menuName = "Blackout/Events/Noise Event Channel", fileName = "NoiseEventChannel")]
    public class NoiseEventChannel : EventChannel<NoiseEvent>
    {
    }
}
