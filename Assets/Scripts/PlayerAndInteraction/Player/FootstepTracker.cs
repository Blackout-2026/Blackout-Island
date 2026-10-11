// Owner: Systems Engineer.
namespace Blackout.Player
{
    /// <summary>
    /// Counts distance walked and reports a footstep every stride, so step rate follows real speed
    /// (and stops against a wall) instead of a timer. The leftover distance is kept, so cadence stays exact.
    /// </summary>
    public sealed class FootstepTracker
    {
        private readonly PlayerMovementSettings settings;
        private float distanceSinceStep;

        public FootstepTracker(PlayerMovementSettings settings)
        {
            this.settings = settings;
        }

        public int StepCount { get; private set; }

        public float Stride(bool sprinting)
        {
            return sprinting ? settings.sprintStride : settings.walkStride;
        }

        /// <summary>Adds this frame's horizontal travel. Returns true when a footstep lands this frame.</summary>
        public bool Advance(bool grounded, bool sprinting, float distance, float horizontalSpeed)
        {
            if (!grounded || horizontalSpeed <= settings.minFootstepSpeed)
            {
                return false;
            }

            distanceSinceStep += distance;
            float stride = Stride(sprinting);
            if (distanceSinceStep < stride)
            {
                return false;
            }

            distanceSinceStep -= stride;
            StepCount++;
            return true;
        }

        public void Reset()
        {
            distanceSinceStep = 0f;
        }
    }
}
