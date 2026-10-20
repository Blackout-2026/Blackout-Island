// Owner: Systems Engineer (Hunter AI).
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// The Hunter's eyes and awareness meter, ported from the prototype's perception.js (docs/reference/
    /// systems-engineer.md section 15). Each think tick: is the player in range and in the cone, can a ray reach the
    /// head or the chest, and then the meter fills or empties. At 1 the player is detected, the sighting is kept,
    /// and 6 s without seeing them the detection is lost. Plain C# with time passed in, so it is tested without a
    /// scene.
    /// </summary>
    public sealed class HunterVision
    {
        private readonly HunterConfig config;

        public HunterVision(HunterConfig config)
        {
            this.config = config;
        }

        public float Awareness { get; private set; }
        public bool CanSee { get; private set; }
        public bool Detected { get; private set; }
        /// <summary>True once the player has been detected at least once (and not forgotten).</summary>
        public bool HasSighting { get; private set; }
        /// <summary>The player's feet when last seen while detected.</summary>
        public Vector3 LastSeenPosition { get; private set; }
        public float LastSeenTime { get; private set; } = float.NegativeInfinity;
        /// <summary>The distance from eye to head on the last tick the player was in range.</summary>
        public float Distance { get; private set; } = float.PositiveInfinity;
        /// <summary>The Commander's alert sets this (x1.4); the base range stays in the config.</summary>
        public float RangeMultiplier { get; set; } = 1f;

        public float Range => config.sightRange * RangeMultiplier;

        /// <summary>
        /// In range and inside the view cone (flat), or within the close-sense range in any direction.
        /// </summary>
        public static bool InView(Vector3 eye, Vector3 forward, Vector3 target, float range, float fieldOfViewDegrees,
            float closeSenseRange, out float distance)
        {
            Vector3 delta = target - eye;
            distance = delta.magnitude;
            if (distance >= range)
            {
                return false;
            }

            if (distance < closeSenseRange)
            {
                return true;
            }

            var flat = new Vector3(delta.x, 0f, delta.z);
            var flatForward = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 1e-8f || flatForward.sqrMagnitude < 1e-8f)
            {
                return true;
            }

            float cos = Vector3.Dot(flat.normalized, flatForward.normalized);
            return cos > Mathf.Cos(fieldOfViewDegrees * 0.5f * Mathf.Deg2Rad);
        }

        /// <summary>How easy the player is to see, from their speed: still = camouflage, sprinting = x1.25.</summary>
        public static float Visibility(float playerSpeed, HunterConfig config)
        {
            if (playerSpeed < config.stillSpeed)
            {
                return config.camouflageMultiplier;
            }

            return playerSpeed > config.sprintSpeed ? config.sprintVisibility : 1f;
        }

        /// <summary>Awareness per second while seeing the player.</summary>
        public static float FillRate(float distance, float range, float visibility, bool detected, HunterConfig config)
        {
            float near = Mathf.Max(config.minNearFactor, config.nearOffset - distance / range);
            return config.awarenessRate * near * visibility * (detected ? config.detectedRateMultiplier : 1f);
        }

        /// <summary>
        /// One think tick. <paramref name="playerFeet"/> is the player's position; the rays go to the head and
        /// chest above it. <paramref name="forward"/> is where the Hunter faces.
        /// </summary>
        public void Update(Vector3 hunterFeet, Vector3 forward, Vector3 playerFeet, bool playerAlive,
            float playerSpeed, ISightCheck sight, float now, float deltaTime)
        {
            CanSee = false;
            if (!playerAlive)
            {
                Awareness = Mathf.Max(0f, Awareness - config.awarenessDecayPlayerDead * deltaTime);
                Detected = false;
                return;
            }

            Vector3 eye = hunterFeet + Vector3.up * config.eyeHeight;
            Vector3 head = playerFeet + Vector3.up * config.playerHeadHeight;
            float range = Range;
            if (InView(eye, forward, head, range, config.fieldOfViewDegrees, config.closeSenseRange, out float distance))
            {
                Distance = distance;
                CanSee = sight.IsClear(eye, head)
                         || sight.IsClear(eye, playerFeet + Vector3.up * config.playerChestHeight);
            }

            if (CanSee)
            {
                float rate = FillRate(distance, range, Visibility(playerSpeed, config), Detected, config);
                Awareness = Mathf.Min(config.awarenessCap, Awareness + rate * deltaTime);
                if (Awareness >= config.detectThreshold)
                {
                    Detected = true;
                    HasSighting = true;
                    LastSeenPosition = playerFeet;
                    LastSeenTime = now;
                }

                return;
            }

            Awareness = Mathf.Max(0f,
                Awareness - deltaTime * (Detected ? config.awarenessDecayDetected : config.awarenessDecay));
            if (Detected && now - LastSeenTime > config.loseDetectionSeconds)
            {
                Detected = false;
            }
        }
    }
}
