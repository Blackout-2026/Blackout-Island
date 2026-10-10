// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using System;
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>
    /// An event with no payload: GeneratorDisabled, CureTaken, PlayerDied, PlayerRespawned, MissionComplete.
    /// </summary>
    [CreateAssetMenu(menuName = "Blackout/Events/Void Event Channel", fileName = "VoidEventChannel")]
    public class VoidEventChannel : ScriptableObject
    {
        public event Action OnRaised;

        /// <summary>Also on the asset's context menu, so any owner can fire it by hand while testing.</summary>
        [ContextMenu("Raise")]
        public void Raise()
        {
            OnRaised?.Invoke();
        }
    }
}
