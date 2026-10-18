// Owner: Agent Controller. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
// Created by the Systems Engineer in Y1; only the Agent Controller edits it from now on.
using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>
    /// Squad knowledge shared by all agents (prototype agents/blackboard.js). Agents never reference each other;
    /// they read and write here. A plain class: one MonoBehaviour in the Agents scene holds the single instance.
    /// </summary>
    public class Blackboard
    {
        public Vector3 lastKnownPlayerPos;
        /// <summary>Time the player was last seen or heard; negative infinity means never.</summary>
        public float lastKnownPlayerTime = float.NegativeInfinity;
        /// <summary>0 = calm, 1 = alert (the prototype only uses on/off). Higher values are free for the owner to add.</summary>
        public int alertLevel;
        public bool generatorDisabled;
        public bool cureTaken;
        /// <summary>Set by the player lifecycle integration, never by a predicted GOAP effect.</summary>
        public bool playerNeutralized;
        /// <summary>Cleared when the roster changes; set after the Commander completes a regroup.</summary>
        public bool squadAssembled;

        public event System.Action CureTakenReported;
        public event System.Action<AgentAlert> AlertChanged;
        public bool HasAlertBuff { get; private set; }
        public AgentAlert Alert { get; private set; } = new AgentAlert(1f, 1f, 1f);

        /// <summary>Called by the event-channel holder. Living Commanders react synchronously.</summary>
        public void ReportCureTaken()
        {
            cureTaken = true;
            CureTakenReported?.Invoke();
        }

        public void BroadcastAlert(AgentAlert alert)
        {
            alertLevel = System.Math.Max(1, alertLevel);
            if (HasAlertBuff && Alert.speedMultiplier == alert.speedMultiplier &&
                Alert.visionMultiplier == alert.visionMultiplier && Alert.aimErrorMultiplier == alert.aimErrorMultiplier)
                return;
            Alert = alert;
            HasAlertBuff = true;
            AlertChanged?.Invoke(alert);
        }

        private readonly Dictionary<string, string> agentStates = new Dictionary<string, string>();
        private readonly List<BlackboardAgent> agents = new List<BlackboardAgent>();

        /// <summary>Agent positions, health and optional directive receivers. Registration allocates; reading does not.</summary>
        public IReadOnlyList<BlackboardAgent> Agents => agents;

        /// <summary>Registers or updates an agent. Updating a brain preserves its last directive.</summary>
        public BlackboardAgent RegisterAgent(string agentName, Transform transform, Health health,
            IDirectiveReceiver receiver)
        {
            if (string.IsNullOrWhiteSpace(agentName))
                throw new System.ArgumentException("An agent needs a unique name.", nameof(agentName));
            for (int i = 0; i < agents.Count; i++)
            {
                if (agents[i].Name != agentName)
                    continue;
                agents[i].Update(transform, health, receiver);
                return agents[i];
            }

            var agent = new BlackboardAgent(agentName, transform, health, receiver);
            agents.Add(agent);
            squadAssembled = false;
            return agent;
        }

        /// <summary>Registered agents (by name) and their current state text, for the debug overlay and the Commander.</summary>
        public IReadOnlyDictionary<string, string> AgentStates => agentStates;

        /// <summary>Registers the agent if needed and records its state, for example "Patrol".</summary>
        public void SetAgentState(string agentName, string state)
        {
            agentStates[agentName] = state;
        }

        public void RemoveAgent(string agentName)
        {
            agentStates.Remove(agentName);
            for (int i = agents.Count - 1; i >= 0; i--)
            {
                if (agents[i].Name == agentName)
                {
                    agents.RemoveAt(i);
                    squadAssembled = false;
                }
            }
        }
    }
}
