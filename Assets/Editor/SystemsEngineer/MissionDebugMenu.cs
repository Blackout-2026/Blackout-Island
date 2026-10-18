using Blackout.Mission;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/Debug/Mission: jump to any mission phase in Play mode (start from Bootstrap.unity). The player is
    /// moved to the phase's start and the generator and cure are handled as needed, see MissionDirector.DebugJump.
    /// Play mode only, so it never changes a scene file.
    /// </summary>
    public static class MissionDebugMenu
    {
        private const string Root = "Blackout/Debug/Mission/";

        [MenuItem(Root + "Jump to Beach")]
        private static void Beach() => Jump(MissionPhase.Beach);

        [MenuItem(Root + "Jump to Forest")]
        private static void Forest() => Jump(MissionPhase.Forest);

        [MenuItem(Root + "Jump to Bunker Entrance")]
        private static void BunkerEntrance() => Jump(MissionPhase.BunkerEntrance);

        [MenuItem(Root + "Jump to Corridor")]
        private static void Corridor() => Jump(MissionPhase.Corridor);

        [MenuItem(Root + "Jump to Generator Room")]
        private static void GeneratorRoom() => Jump(MissionPhase.GeneratorRoom);

        [MenuItem(Root + "Jump to Lab (generator off)")]
        private static void Lab() => Jump(MissionPhase.Lab);

        [MenuItem(Root + "Jump to Escape (cure taken)")]
        private static void Escape() => Jump(MissionPhase.Escape);

        [MenuItem(Root + "Jump to Complete (at the boat)")]
        private static void Complete() => Jump(MissionPhase.Complete);

        [MenuItem(Root + "Jump to Beach", true)]
        [MenuItem(Root + "Jump to Forest", true)]
        [MenuItem(Root + "Jump to Bunker Entrance", true)]
        [MenuItem(Root + "Jump to Corridor", true)]
        [MenuItem(Root + "Jump to Generator Room", true)]
        [MenuItem(Root + "Jump to Lab (generator off)", true)]
        [MenuItem(Root + "Jump to Escape (cure taken)", true)]
        [MenuItem(Root + "Jump to Complete (at the boat)", true)]
        private static bool InPlayMode() => EditorApplication.isPlaying;

        private static void Jump(MissionPhase phase)
        {
            var director = Object.FindFirstObjectByType<MissionDirector>();
            if (director == null)
            {
                Debug.LogWarning("[Blackout] No MissionDirector: open Assets/Scenes/Bootstrap.unity and press Play.");
                return;
            }

            director.DebugJump(phase);
        }
    }
}
