// Owner: World Builder (Security Patrol AI).
using System;
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>
    /// Plain C# patrol senses; no Transform, clock, movement or animation dependencies.
    /// The host supplies world positions at its think rate (normally 10 Hz).
    /// </summary>
    public sealed class Perception
    {
        private readonly float visionRangeSquared;
        private readonly float minimumVisionDot;
        private readonly IPerceptionRaycast sightRaycast;

        public bool CanSeePlayer { get; private set; }
        public bool HasLastKnownPosition { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }

        /// <param name="visionRange">Maximum eye-to-target distance in metres; zero disables vision.</param>
        /// <param name="visionAngle">Full horizontal cone angle in degrees, from 0 to 360.</param>
        public Perception(float visionRange, float visionAngle, IPerceptionRaycast sightRaycast)
        {
            if (float.IsNaN(visionRange) || float.IsInfinity(visionRange) || visionRange < 0f)
                throw new ArgumentOutOfRangeException(nameof(visionRange));
            if (float.IsNaN(visionAngle) || float.IsInfinity(visionAngle) ||
                visionAngle < 0f || visionAngle > 360f)
                throw new ArgumentOutOfRangeException(nameof(visionAngle));

            this.sightRaycast = sightRaycast ?? throw new ArgumentNullException(nameof(sightRaycast));
            visionRangeSquared = visionRange * visionRange;
            minimumVisionDot = Mathf.Cos(visionAngle * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>
        /// Tests 3D range, horizontal facing, then line of sight to one supplied player sight point.
        /// Range and cone boundaries are inclusive. A failed check keeps the last confirmed position.
        /// Forward need not be normalized; a zero horizontal forward cannot see.
        /// </summary>
        public void UpdateVision(Vector3 eyePosition, Vector3 forward, Vector3 playerPosition)
        {
            CanSeePlayer = false;
            Vector3 offset = playerPosition - eyePosition;
            if (visionRangeSquared <= 0f || offset.sqrMagnitude > visionRangeSquared)
                return;

            forward.y = 0f;
            float facingSquared = forward.sqrMagnitude;
            if (facingSquared <= 0f)
                return;

            Vector3 horizontalOffset = offset;
            horizontalOffset.y = 0f;
            float horizontalSquared = horizontalOffset.sqrMagnitude;
            if (horizontalSquared > 0f)
            {
                float dot = Vector3.Dot(forward, horizontalOffset) /
                    Mathf.Sqrt(facingSquared * horizontalSquared);
                if (Mathf.Clamp(dot, -1f, 1f) < minimumVisionDot)
                    return;
            }

            if (sightRaycast.IsBlocked(eyePosition, playerPosition))
                return;

            CanSeePlayer = true;
            HasLastKnownPosition = true;
            LastKnownPosition = playerPosition;
        }
    }
}
