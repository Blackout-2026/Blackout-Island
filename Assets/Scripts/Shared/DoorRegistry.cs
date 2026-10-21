// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>
    /// Every door in the loaded scenes (step F2). A door registers itself when it is enabled and leaves when it is disabled, so the Agent Controller's
    /// movement finds the doors at run time across the additive scenes without a scene search or a persistent reference (agents live in another scene
    /// from the doors). Read-only for the movement: it asks each <see cref="IDoor"/> to open.
    /// </summary>
    public static class DoorRegistry
    {
        private static readonly List<IDoor> doors = new List<IDoor>();

        public static IReadOnlyList<IDoor> Doors => doors;

        public static void Register(IDoor door)
        {
            if (door != null && !doors.Contains(door))
            {
                doors.Add(door);
            }
        }

        public static void Unregister(IDoor door)
        {
            doors.Remove(door);
        }

        /// <summary>True for a door whose Unity object has been destroyed (a scene was unloaded without the door disabling first).</summary>
        public static bool IsDestroyed(IDoor door)
        {
            return door == null || (door is Object unityObject && unityObject == null);
        }

        // With Enter Play Mode options (no domain reload) the list would otherwise keep last session's doors.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            doors.Clear();
        }
    }
}
