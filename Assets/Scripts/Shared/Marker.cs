// Owner: World Builder. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
// Created by the Systems Engineer in Y1; only the World Builder edits it from now on.
using UnityEngine;

namespace Blackout.Shared
{
    public enum MarkerType
    {
        PlayerStart,
        Checkpoint,
        AgentSpawn,
        Waypoint,
        CoverPoint,
        DoorSocket,
        GeneratorSocket,
        CureSocket,
        BoatExit,
        HealthPickup
    }

    /// <summary>
    /// A named point in the Environment scene. Other scenes cannot reference it through the Inspector
    /// (additive scenes), so code finds markers at runtime with FindObjectsByType&lt;Marker&gt; and filters by
    /// <see cref="Type"/>, <see cref="Id"/> and <see cref="Owner"/>.
    /// </summary>
    public class Marker : MonoBehaviour
    {
        [SerializeField] private MarkerType type;
        [Tooltip("Unique name, for example Checkpoint_Beach or Waypoint_Hunter_02.")]
        [SerializeField] private string id;
        [Tooltip("Who uses it, for example an agent name for Waypoint and AgentSpawn markers. Empty means anyone.")]
        [SerializeField] private string owner;

        public MarkerType Type => type;
        public string Id => id;
        public string Owner => owner;
    }
}
