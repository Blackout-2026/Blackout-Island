using Blackout.Diagnostics;
using Blackout.Mission;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/Debug/Stress Test: runs the scripted StressTest in Play mode (start from Bootstrap.unity). It jumps
    /// through every phase and fires the real weapons, then logs a report (docs/reference/systems-engineer.md
    /// section 12). It ends on the Mission Complete screen; press Restart or stop Play mode afterwards.
    /// Play mode only, so it never changes a scene file.
    /// </summary>
    public static class StressTestMenu
    {
        private const string Item = "Blackout/Debug/Stress Test";

        [MenuItem(Item)]
        private static void Run()
        {
            var director = Object.FindFirstObjectByType<MissionDirector>();
            if (director == null || !director.IsRunning)
            {
                Debug.LogWarning("[Blackout] No running mission: open Assets/Scenes/Bootstrap.unity and press Play.");
                return;
            }

            if (Object.FindFirstObjectByType<StressTest>() != null)
            {
                Debug.LogWarning("[Blackout] A Stress Test is already running.");
                return;
            }

            new GameObject("StressTest").AddComponent<StressTest>();
            Debug.Log("[Blackout] Stress Test started: about 15 s, the report follows in the Console.");
        }

        [MenuItem(Item, true)]
        private static bool InPlayMode() => EditorApplication.isPlaying;
    }
}
