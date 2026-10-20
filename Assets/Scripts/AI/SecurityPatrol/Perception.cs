// Owner: World Builder (Security Patrol AI).
using System;
using Blackout.Shared;
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
        private readonly float hearingRange;
        private readonly float hearingThreshold;

        public bool CanSeePlayer { get; private set; }
        public bool HasLastKnownPosition { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public bool HeardNoise { get; private set; }
        public Vector3 NoisePosition { get; private set; }
        public float NoiseLoudness { get; private set; }

        /// <param name="visionRange">Maximum eye-to-target distance in metres; zero disables vision.</param>
        /// <param name="visionAngle">Full horizontal cone angle in degrees, from 0 to 360.</param>
        /// <param name="hearingRange">Independent listener distance cap in metres; zero disables hearing.</param>
        /// <param name="hearingThreshold">Normalized loudness must be strictly above this value.</param>
        public Perception(float visionRange, float visionAngle, IPerceptionRaycast sightRaycast,
            float hearingRange = 65f, float hearingThreshold = 0.04f)
        {
            if (float.IsNaN(visionRange) || float.IsInfinity(visionRange) || visionRange < 0f)
                throw new ArgumentOutOfRangeException(nameof(visionRange));
            if (float.IsNaN(visionAngle) || float.IsInfinity(visionAngle) ||
                visionAngle < 0f || visionAngle > 360f)
                throw new ArgumentOutOfRangeException(nameof(visionAngle));
            if (float.IsNaN(hearingRange) || float.IsInfinity(hearingRange) || hearingRange < 0f)
                throw new ArgumentOutOfRangeException(nameof(hearingRange));
            if (float.IsNaN(hearingThreshold) || float.IsInfinity(hearingThreshold) ||
                hearingThreshold < 0f || hearingThreshold > 1f)
                throw new ArgumentOutOfRangeException(nameof(hearingThreshold));

            this.sightRaycast = sightRaycast ?? throw new ArgumentNullException(nameof(sightRaycast));
            visionRangeSquared = visionRange * visionRange;
            minimumVisionDot = Mathf.Cos(visionAngle * 0.5f * Mathf.Deg2Rad);
            this.hearingRange = hearingRange;
            this.hearingThreshold = hearingThreshold;
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

        /// <summary>
        /// NoiseEvent.loudness is a radius in metres: perceived = max(0, 1 - distance / radius).
        /// Keeps the strongest audible noise since ClearHeardNoise (newest wins equal strength).
        /// Returns true when remembered. Hearing uses distance only; no wall attenuation in S2.
        /// </summary>
        public bool HearNoise(Vector3 listenerPosition, NoiseEvent noise)
        {
            if (hearingRange <= 0f || !(noise.loudness > 0f) || float.IsInfinity(noise.loudness))
                return false;

            float distance = Vector3.Distance(listenerPosition, noise.position);
            if (distance > hearingRange)
                return false;

            float perceived = Mathf.Max(0f, 1f - distance / noise.loudness);
            if (!(perceived > hearingThreshold) || (HeardNoise && perceived < NoiseLoudness))
                return false;

            HeardNoise = true;
            NoisePosition = noise.position;
            NoiseLoudness = perceived;
            // A sound can guide investigation, but should not replace a currently visible player.
            if (!CanSeePlayer)
            {
                HasLastKnownPosition = true;
                LastKnownPosition = noise.position;
            }
            return true;
        }

        /// <summary>Call after the host reads this tick's noises. Last-known position survives.</summary>
        public void ClearHeardNoise()
        {
            HeardNoise = false;
            NoisePosition = default;
            NoiseLoudness = 0f;
        }

        /// <summary>Clear all sensing memory when resetting or respawning the listener.</summary>
        public void Forget()
        {
            CanSeePlayer = false;
            HasLastKnownPosition = false;
            LastKnownPosition = default;
            ClearHeardNoise();
        }
    }
}
