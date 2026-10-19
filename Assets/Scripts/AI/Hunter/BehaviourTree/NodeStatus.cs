// Owner: Systems Engineer (Hunter AI).
namespace Blackout.AI.Hunter
{
    /// <summary>What a behaviour tree node reports after a tick (docs/reference/systems-engineer.md section 13).</summary>
    public enum NodeStatus
    {
        /// <summary>Not finished; tick it again next time.</summary>
        Running,
        Success,
        Failure
    }
}
