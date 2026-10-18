// Owner: Agent Controller. Shared contract: extend it, never break it.
using System;

namespace Blackout.Shared
{
    /// <summary>Absolute alert multipliers; assigning them repeatedly must never stack the buff.</summary>
    public readonly struct AgentAlert
    {
        public readonly float speedMultiplier;
        public readonly float visionMultiplier;
        public readonly float aimErrorMultiplier;

        public AgentAlert(float speedMultiplier, float visionMultiplier, float aimErrorMultiplier)
        {
            if (!Valid(speedMultiplier) || !Valid(visionMultiplier) || !Valid(aimErrorMultiplier))
                throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "Alert multipliers must be finite and non-negative.");
            this.speedMultiplier = speedMultiplier;
            this.visionMultiplier = visionMultiplier;
            this.aimErrorMultiplier = aimErrorMultiplier;
        }

        private static bool Valid(float value) => value >= 0f && !float.IsInfinity(value) && !float.IsNaN(value);
    }

    /// <summary>Perception brains use visionMultiplier against their unmodified base sight range.</summary>
    public interface IAlertReceiver
    {
        void OnAlert(AgentAlert alert);
    }
}
