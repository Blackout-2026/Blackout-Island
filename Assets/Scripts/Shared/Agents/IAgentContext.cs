// Owner: Agent Controller. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
// Created by the Systems Engineer in Y1; only the Agent Controller edits it from now on.
using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>Everything a brain may read about its agent and the world, handed over once at Initialize.</summary>
    public interface IAgentContext
    {
        Transform Self { get; }
        Health Health { get; }
        Blackboard Blackboard { get; }
        /// <summary>The player's transform, or null if there is no player in the scene.</summary>
        Transform Player { get; }

        /// <summary>Markers of a type, optionally only those whose Owner matches (null = any owner). Do not modify the list.</summary>
        IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null);
    }
}
