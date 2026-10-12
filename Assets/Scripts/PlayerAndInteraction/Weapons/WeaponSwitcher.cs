// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Weapons
{
    public enum SwitchState
    {
        Ready,
        Lowering,
        Raising
    }

    /// <summary>
    /// Weapon switching as a small state machine: Ready → Lowering → (swap) → Raising → Ready.
    /// The prototype lowers for 0.18 s and raises for 0.22 s, so lowering takes 45 % of the switch time.
    /// Pure C#, so the timing is tested without a scene; WeaponController shows and hides the models.
    /// </summary>
    public sealed class WeaponSwitcher
    {
        public const float LowerFraction = 0.45f;

        private readonly int count;
        private float lowerTime;
        private float raiseTime;

        public WeaponSwitcher(int count, int startIndex)
        {
            this.count = count;
            ActiveIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, count - 1));
            PendingIndex = -1;
        }

        public int ActiveIndex { get; private set; }
        /// <summary>The weapon being switched to while lowering, otherwise -1.</summary>
        public int PendingIndex { get; private set; }
        public SwitchState State { get; private set; }
        /// <summary>Pose of the viewmodel: 0 = raised, 1 = fully lowered.</summary>
        public float Lowered { get; private set; }
        public bool IsReady => State == SwitchState.Ready;

        /// <summary>
        /// Starts switching to a weapon. Ignored for the active weapon or an invalid index. A request while
        /// lowering replaces the pending weapon; a request while raising lowers again from the current pose.
        /// </summary>
        public bool Request(int index, float switchTime)
        {
            if (index < 0 || index >= count || index == ActiveIndex)
            {
                return false;
            }

            PendingIndex = index;
            lowerTime = switchTime * LowerFraction;
            raiseTime = switchTime - lowerTime;
            State = SwitchState.Lowering;
            return true;
        }

        /// <summary>The weapon <paramref name="direction"/> steps away from the active one, wrapping around.</summary>
        public int Neighbour(int direction)
        {
            if (count == 0)
            {
                return -1;
            }

            return ((ActiveIndex + direction) % count + count) % count;
        }

        /// <summary>Advances the switch. Returns true on the frame the active weapon changes.</summary>
        public bool Tick(float dt)
        {
            if (State == SwitchState.Lowering)
            {
                Lowered += lowerTime > 0f ? dt / lowerTime : 1f;
                if (Lowered >= 1f)
                {
                    Lowered = 1f;
                    ActiveIndex = PendingIndex;
                    PendingIndex = -1;
                    State = SwitchState.Raising;
                    return true;
                }
            }
            else if (State == SwitchState.Raising)
            {
                Lowered -= raiseTime > 0f ? dt / raiseTime : 1f;
                if (Lowered <= 0f)
                {
                    Lowered = 0f;
                    State = SwitchState.Ready;
                }
            }

            return false;
        }
    }
}
