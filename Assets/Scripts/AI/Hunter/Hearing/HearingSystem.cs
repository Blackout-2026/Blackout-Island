// Owner: Systems Engineer (Hunter AI).
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// The Hunter's ears (docs/reference/systems-engineer.md section 14). It listens to the Noise channel, works out
    /// how loud each noise is here with <see cref="HearingMath"/> (distance falloff, walls counted with a raycast,
    /// the Hunter's hearing), and remembers the strongest recent one in a <see cref="NoiseMemory"/> that fades over
    /// 8 s. The brain (H3) reads <see cref="PerceivedLoudness"/>, <see cref="NoiseDirection"/> and
    /// <see cref="TimeSinceNoise"/>; this component never moves the agent. Put it on the agent's root, whose position
    /// is at the feet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HearingSystem : MonoBehaviour
    {
        [Tooltip("Every constant of the hearing formula. Empty falls back to the defaults (and logs a warning).")]
        [SerializeField] private HearingConfig config;
        [Tooltip("Noise.asset in Assets/ScriptableObjects/Events.")]
        [SerializeField] private NoiseEventChannel noiseChannel;

        private static readonly Func<float> DefaultClock = () => Time.time;

        private Func<float> clock = DefaultClock;
        private IOcclusionCounter occlusion;
        private NoiseMemory memory;
        private HearingConfig fallbackConfig;
        private NoiseEventChannel subscribedChannel;
        private Action<NoiseEvent> onNoise;

        /// <summary>How loud the remembered noise is now, after fading. 0 if nothing is remembered.</summary>
        public float PerceivedLoudness => Memory.LoudnessAt(clock());

        /// <summary>Flat unit vector from the Hunter to the remembered noise, or zero if there is none.</summary>
        public Vector3 NoiseDirection
        {
            get
            {
                if (!HasNoise)
                {
                    return Vector3.zero;
                }

                Vector3 direction = Memory.Position - transform.position;
                direction.y = 0f;
                return direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;
            }
        }

        /// <summary>Seconds since the remembered noise was heard, or infinity if nothing has been heard.</summary>
        public float TimeSinceNoise => Memory.TimeSince(clock());

        public bool HasNoise => Memory.HasNoise(clock());
        public Vector3 NoisePosition => Memory.Position;
        public NoiseType NoiseType => Memory.Type;

        /// <summary>Walls counted for the last noise that reached the raycast (for the H4 debug text). -1 before any.</summary>
        public int LastWallCount { get; private set; } = -1;

        public HearingConfig Config
        {
            get
            {
                if (config != null)
                {
                    return config;
                }

                if (fallbackConfig == null)
                {
                    Debug.LogWarning("[Blackout] HearingSystem has no HearingConfig; using the defaults.", this);
                    fallbackConfig = ScriptableObject.CreateInstance<HearingConfig>();
                    fallbackConfig.hideFlags = HideFlags.HideAndDontSave;
                }

                return fallbackConfig;
            }
        }

        private NoiseMemory Memory => memory ?? (memory = new NoiseMemory(Config.memorySeconds));

        // Tests (and the H3 brain) plug these in before listening.
        public void SetConfig(HearingConfig newConfig)
        {
            config = newConfig;
            memory = null;
            occlusion = null;
        }

        public void SetClock(Func<float> newClock) => clock = newClock ?? DefaultClock;
        public void SetOcclusion(IOcclusionCounter counter) => occlusion = counter;

        public void SetChannel(NoiseEventChannel channel)
        {
            bool listening = subscribedChannel != null;
            StopListening();
            noiseChannel = channel;
            if (listening)
            {
                StartListening();
            }
        }

        private void OnEnable() => StartListening();
        private void OnDisable() => StopListening();

        private void OnDestroy()
        {
            if (fallbackConfig == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(fallbackConfig);
            }
            else
            {
                DestroyImmediate(fallbackConfig);
            }
        }

        /// <summary>Subscribes to the Noise channel (OnEnable does this; EditMode tests call it directly).</summary>
        public void StartListening()
        {
            if (noiseChannel == null || subscribedChannel != null)
            {
                return;
            }

            onNoise ??= noise => Hear(noise);
            noiseChannel.OnRaised += onNoise;
            subscribedChannel = noiseChannel;
        }

        public void StopListening()
        {
            if (subscribedChannel != null)
            {
                subscribedChannel.OnRaised -= onNoise;
                subscribedChannel = null;
            }
        }

        /// <summary>Forgets the remembered noise (after a respawn, for example).</summary>
        public void Forget() => Memory.Clear();

        /// <summary>
        /// Works out how loud <paramref name="noise"/> is here and remembers it if it is heard and at least as loud as
        /// the faded memory. Returns true if it was remembered. The Hunter's own noises are ignored. The raycast is
        /// skipped when even open space could not make the noise heard or beat the memory.
        /// </summary>
        public bool Hear(NoiseEvent noise)
        {
            if (IsOwnNoise(noise.source))
            {
                return false;
            }

            HearingConfig values = Config;
            float now = clock();
            Vector3 listener = transform.position;
            float distance = Vector3.Distance(noise.position, listener);
            float openSpace = HearingMath.Perceived(values, noise.loudness, distance, 0);
            if (!HearingMath.IsAudible(openSpace, values.threshold) || openSpace < Memory.LoudnessAt(now))
            {
                return false;
            }

            occlusion ??= new RaycastOcclusionCounter(values.occluders);
            int walls = occlusion.CountWalls(noise.position + Vector3.up * values.sourceHeight,
                listener + Vector3.up * values.listenerHeight, values.maxWalls);
            LastWallCount = walls;

            float perceived = openSpace * HearingMath.ThroughWalls(walls, values.wallFactor);
            return HearingMath.IsAudible(perceived, values.threshold)
                   && Memory.Offer(noise.position, noise.type, perceived, now);
        }

        private bool IsOwnNoise(GameObject source)
        {
            return source != null && (source == gameObject || source.transform.IsChildOf(transform));
        }
    }
}
