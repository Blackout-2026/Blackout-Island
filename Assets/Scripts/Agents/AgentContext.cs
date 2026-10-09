// Owner: Agent Controller.
using System;
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// What a brain may read about its agent and the world (see IAgentContext). The player and the markers are
    /// looked up through delegates so they can appear after the agent does, which happens because Environment and
    /// the player are loaded additively.
    /// </summary>
    public sealed class AgentContext : IAgentContext
    {
        private readonly Func<Transform> playerSource;
        private readonly Func<MarkerIndex> markerSource;

        public AgentContext(Transform self, Health health, Blackboard blackboard,
            Func<Transform> playerSource, Func<MarkerIndex> markerSource)
        {
            Self = self;
            Health = health;
            Blackboard = blackboard;
            this.playerSource = playerSource;
            this.markerSource = markerSource;
        }

        public Transform Self { get; }
        public Health Health { get; }
        public Blackboard Blackboard { get; }

        public Transform Player => playerSource?.Invoke();

        public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null)
        {
            return markerSource().Get(type, owner);
        }
    }
}
