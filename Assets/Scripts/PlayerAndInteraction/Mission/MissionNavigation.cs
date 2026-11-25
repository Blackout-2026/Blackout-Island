// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Mission
{
    /// <summary>Prototype-style objective guidance using the existing Unity marker IDs, not Inspector scene links.</summary>
    public static class MissionNavigation
    {
        public static string TargetMarker(MissionPhase phase, bool cureTaken, bool bunkerOpen,
            Vector3 player, out string label)
        {
            label = string.Empty;
            if (phase == MissionPhase.Complete) return null;
            if (cureTaken)
            {
                // Guide back through security and the ramp before pointing to the boat outside.
                if (player.y < 9f && player.z > 84f)
                {
                    label = "EXIT";
                    return "DoorSocket_D-Security";
                }
                if (player.z >= 48.2f)
                {
                    label = "EXIT";
                    return "DoorSocket_D-Bunker";
                }
                label = "BOAT";
                return "BoatExit";
            }

            switch (phase)
            {
                case MissionPhase.Beach:
                case MissionPhase.Forest:
                    label = "BUNKER";
                    return "DoorSocket_D-Bunker";
                case MissionPhase.BunkerEntrance:
                    label = bunkerOpen ? "RAMP" : "BUNKER";
                    return bunkerOpen ? "DoorSocket_D-Security" : "DoorSocket_D-Bunker";
                case MissionPhase.Corridor:
                    label = "GENERATOR";
                    return "DoorSocket_D-Generator";
                case MissionPhase.GeneratorRoom:
                    label = "GENERATOR";
                    return "GeneratorSocket";
                case MissionPhase.Lab:
                    label = "CURE";
                    return "CureSocket";
                default:
                    return null;
            }
        }
    }
}
