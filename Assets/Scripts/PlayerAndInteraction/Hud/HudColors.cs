// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Hud
{
    /// <summary>The prototype's HUD palette (ui/style.css), in one place so every HUD element matches.</summary>
    public static class HudColors
    {
        public static readonly Color Text = new Color(226f / 255f, 232f / 255f, 226f / 255f, 0.92f);
        public static readonly Color Dim = new Color(200f / 255f, 210f / 255f, 205f / 255f, 0.55f);
        public static readonly Color Accent = new Color(0xd9 / 255f, 0xa5 / 255f, 0x21 / 255f, 1f);
        public static readonly Color Danger = new Color(1f, 0x3b / 255f, 0x2a / 255f, 1f);
        public static readonly Color Good = new Color(0x47 / 255f, 0xd1 / 255f, 0xd6 / 255f, 1f);
        public static readonly Color Panel = new Color(8f / 255f, 11f / 255f, 14f / 255f, 0.62f);
        /// <summary>Health bar fill when healthy.</summary>
        public static readonly Color HealthFill = new Color(0.86f, 0.9f, 0.86f, 1f);
        /// <summary>The red ghost bar that trails damage.</summary>
        public static readonly Color Ghost = new Color(1f, 0.31f, 0.24f, 0.7f);
    }
}
