// Owner: Systems Engineer.
namespace Blackout.Mission
{
    /// <summary>The mission in order (docs/reference/systems-engineer.md section 11). Phases only move forward.</summary>
    public enum MissionPhase
    {
        Beach,
        Forest,
        BunkerEntrance,
        /// <summary>Underground, before the generator room: the prototype's "bunker" phase.</summary>
        Corridor,
        GeneratorRoom,
        /// <summary>After the blackout: take the cure from the laboratory.</summary>
        Lab,
        Escape,
        Complete,
    }
}
