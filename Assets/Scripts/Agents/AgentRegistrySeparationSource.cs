// Owner: Agent Controller.
using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Reads <see cref="AgentRegistry.Current"/> to answer <see cref="ISeparationSource"/>: the real wiring
    /// PathFollower.Awake uses. Iterates the registry's list by index (never foreach over the interface-typed
    /// list, and never allocates a collection), since this runs every frame for every agent.
    /// </summary>
    public sealed class AgentRegistrySeparationSource : ISeparationSource
    {
        // docs/reference/agent-controller.md section 3.2.
        public const float AgentRange = 1.3f;
        public const float AgentStrength = 2.4f;
        public const float PlayerRange = 1.0f;
        public const float PlayerStrength = 3f;

        public Vector2 ComputeSeparation(Vector3 position, Transform self)
        {
            Vector2 push = Vector2.zero;
            AgentRegistry registry = AgentRegistry.Current;
            if (registry == null)
            {
                return push;
            }

            Vector2 flatSelf = new Vector2(position.x, position.z);
            IReadOnlyList<IAgentInfo> agents = registry.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                IAgentInfo agent = agents[i];
                if (!agent.IsAlive || agent.Transform == self)
                {
                    continue;
                }

                Vector3 otherPosition = agent.Transform.position;
                push += MotionMath.SeparationPush(flatSelf, new Vector2(otherPosition.x, otherPosition.z), AgentRange, AgentStrength);
            }

            Transform player = registry.Player;
            if (player != null && player != self)
            {
                Vector3 playerPosition = player.position;
                push += MotionMath.SeparationPush(flatSelf, new Vector2(playerPosition.x, playerPosition.z), PlayerRange, PlayerStrength);
            }

            return push;
        }
    }
}
