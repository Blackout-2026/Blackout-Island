// Owner: Agent Controller.
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// The list of living agents in the Agents scene, plus the services every agent shares: the Blackboard, the
    /// player and the marker index. Agents register themselves, so nothing else needs a reference to any agent.
    /// Markers live in the Environment scene, which cannot be referenced from here, so they are found at runtime.
    /// </summary>
    [RequireComponent(typeof(BlackboardHolder))]
    public class AgentRegistry : MonoBehaviour
    {
        private const string InitialState = "Idle";

        [Tooltip("Leave empty to use the object tagged Player.")]
        [SerializeField] private Transform player;

        private readonly List<IAgentInfo> agents = new List<IAgentInfo>();
        private BlackboardHolder blackboardHolder;
        private MarkerIndex markers;

        /// <summary>The registry of the loaded Agents scene, or null before it has woken up.</summary>
        public static AgentRegistry Current { get; private set; }

        public IReadOnlyList<IAgentInfo> Agents => agents;
        public Blackboard Blackboard => Holder.Instance;

        private BlackboardHolder Holder
        {
            get
            {
                if (blackboardHolder == null)
                {
                    blackboardHolder = GetComponent<BlackboardHolder>();
                }
                return blackboardHolder;
            }
        }

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        public void Register(IAgentInfo agent)
        {
            if (agent == null || agents.Contains(agent))
            {
                return;
            }
            agents.Add(agent);
            Blackboard.SetAgentState(agent.AgentName, InitialState);
        }

        public void Unregister(IAgentInfo agent)
        {
            if (agent == null || !agents.Remove(agent))
            {
                return;
            }
            Blackboard.RemoveAgent(agent.AgentName);
        }

        /// <summary>Publishes the agent's state text (for example "Patrol") on the Blackboard for the overlay and the Commander.</summary>
        public void SetState(IAgentInfo agent, string state)
        {
            Blackboard.SetAgentState(agent.AgentName, state);
        }

        /// <summary>Builds the context handed to one agent's brain at Initialize.</summary>
        public IAgentContext CreateContext(Transform self, Health health)
        {
            return new AgentContext(self, health, Blackboard, GetPlayer, GetMarkers);
        }

        /// <summary>Forget the cached markers so the next query rescans the scenes, for example after Environment loads.</summary>
        public void RefreshMarkers()
        {
            markers = null;
        }

        private Transform GetPlayer()
        {
            if (player == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag("Player");
                if (found != null)
                {
                    player = found.transform;
                }
            }
            return player;
        }

        private MarkerIndex GetMarkers()
        {
            if (markers == null)
            {
                markers = new MarkerIndex(FindObjectsByType<Marker>(FindObjectsSortMode.None));
            }
            return markers;
        }
    }
}
