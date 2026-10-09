// Owner: Agent Controller.
using Blackout.Shared;

namespace Blackout.Agents
{
    /// <summary>
    /// The visual layer of an agent (Animator controller or procedural robot rig). It reads the intent and the
    /// mover's velocity and plays the matching pose; brains never reach it. Implemented in A3.
    /// </summary>
    public interface IAgentAnimator
    {
        /// <summary>Called when the brain produces a new intent (aim on/off, look target, speed).</summary>
        void Apply(in AgentIntent intent);
        void PlayHit(in DamageInfo info);
        void PlayDeath();
    }
}
