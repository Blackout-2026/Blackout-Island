// Owner: Agent Controller.
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Door awareness for the mover (step F2; the prototype's "door awareness", agent-controller.md 3.2). The NavMesh runs through every door gap, so
    /// without this an agent walks straight through a closed door. A door in front of a moving agent is asked to open, and the agent waits until it
    /// is open. A locked door is never opened: the agent waits and the mover's routine re-plan finds the way round (the locked door carves the NavMesh).
    /// Pure logic over <see cref="IDoor"/>, so it is tested with fake doors.
    /// </summary>
    public static class DoorGate
    {
        /// <summary>Metres from the doorway's centre at which a door in front of the agent is asked to open (the door slides in under a second).</summary>
        public const float LookAhead = 1.9f;

        /// <summary>A door on another floor (the ramp, the catwalk) does not count.</summary>
        public const float MaxHeightDifference = 2.5f;

        private const float AheadDot = 0.2f;

        /// <summary>
        /// True when the agent must wait: a closed door is within <see cref="LookAhead"/> metres in the direction it is moving. The door is asked to open
        /// unless it is locked. A standing agent (no direction) and doors behind it are ignored.
        /// </summary>
        public static bool MustWait(Vector3 position, Vector2 direction, GameObject opener, IReadOnlyList<IDoor> doors)
        {
            if (doors == null || direction.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            direction.Normalize();
            bool wait = false;
            for (int i = 0; i < doors.Count; i++)
            {
                IDoor door = doors[i];
                if (DoorRegistry.IsDestroyed(door) || door.IsOpen)
                {
                    continue;
                }

                Vector3 toDoor = door.Position - position;
                if (Mathf.Abs(toDoor.y) > MaxHeightDifference)
                {
                    continue;
                }

                var flat = new Vector2(toDoor.x, toDoor.z);
                float distance = flat.magnitude;
                if (distance > LookAhead || (distance > 0.05f && Vector2.Dot(flat / distance, direction) < AheadDot))
                {
                    continue;
                }

                if (!door.IsLocked)
                {
                    door.RequestOpen(opener);
                }

                wait = true;
            }

            return wait;
        }
    }
}
