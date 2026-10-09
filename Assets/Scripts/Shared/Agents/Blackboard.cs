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

        private readonly Dictionary<string, string> agentStates = new Dictionary<string, string>();

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
        }
    }
}
