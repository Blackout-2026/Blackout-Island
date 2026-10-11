// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// Camera bob driven by distance walked, not time: half a sine period per stride, so each dip lines up with
    /// a footstep. Amplitude follows speed (1 at walk speed) and fades out in the air or when standing still.
    /// </summary>
    public sealed class HeadBob
    {
        private const float TwoPi = Mathf.PI * 2f;

        private readonly PlayerMovementSettings settings;
        private float phase;

        public HeadBob(PlayerMovementSettings settings)
        {
            this.settings = settings;
        }

        public float Amplitude { get; private set; }

        /// <summary>Camera offset in metres: x sideways, y up.</summary>
        public Vector2 Offset { get; private set; }

        public void Update(bool grounded, float distance, float stride, float horizontalSpeed, float dt)
        {
            if (grounded && horizontalSpeed > settings.minFootstepSpeed && stride > 0f)
            {
                // Wrapped so the phase never grows large enough to lose float precision.
                phase = Mathf.Repeat(phase + distance / stride * Mathf.PI, TwoPi);
            }

            float target = grounded && settings.walkSpeed > 0f
                ? Mathf.Clamp(horizontalSpeed / settings.walkSpeed, 0f, settings.bobMaxAmplitude)
                : 0f;
            Amplitude = PlayerMovementLogic.Damp(Amplitude, target, settings.bobAmplitudeDamping, dt);

            float sway = Mathf.Cos(phase) * settings.bobSway * Amplitude;
            // |sin| bounces once per step; subtracting half the height centres it on the eye.
            float bounce = (Mathf.Abs(Mathf.Sin(phase)) - 0.5f) * settings.bobHeight * Amplitude;
            Offset = new Vector2(sway, bounce);
        }
    }
}
