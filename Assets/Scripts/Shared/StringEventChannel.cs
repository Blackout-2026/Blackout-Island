// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>Carries a text value, for ObjectiveChanged (MissionDirector to the HUD).</summary>
    [CreateAssetMenu(menuName = "Blackout/Events/String Event Channel", fileName = "StringEventChannel")]
    public class StringEventChannel : EventChannel<string>
    {
    }
}
