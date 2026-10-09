// Owner: Agent Controller.
using Blackout.Shared;

namespace Blackout.Agents
{
    /// <summary>
    /// Fires the agent's weapon: reaction delay, aim error, bursts, damage and the Gunshot noise (A4).
    /// The brain only sets <see cref="AgentIntent.fire"/>; the shooter decides whether a shot is actually allowed.
    /// </summary>
    public interface IAgentShooter
    {
        /// <summary>Called when the brain produces a new intent. The shooter keeps its trigger state until the next one.</summary>
        void Apply(in AgentIntent intent);
        /// <summary>Releases the trigger and drops any pending burst, for example when the agent dies.</summary>
        void CeaseFire();
    }
}
