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
        /// <summary>A noise this Hunter was told about, kept for the debug gizmos (H4).</summary>
        public readonly struct HeardNoise
        {
            public readonly Vector3 Position;
            /// <summary>The noise's radius (its loudness in the event).</summary>
            public readonly float Radius;
            public readonly NoiseType Type;
            public readonly float Time;
            /// <summary>Perceived loudness here: after walls, or the open-space value if the raycast was skipped.</summary>
            public readonly float Perceived;
            /// <summary>Walls counted, or -1 if the raycast was skipped (the noise could not be heard or win anyway).</summary>
            public readonly int Walls;
            /// <summary>True if it became the remembered noise.</summary>
            public readonly bool Remembered;

            public HeardNoise(in NoiseEvent noise, float time, float perceived, int walls, bool remembered)
            {
                Position = noise.position;
                Radius = noise.loudness;
                Type = noise.type;
                Time = time;
                Perceived = perceived;
                Walls = walls;
                Remembered = remembered;
            }
        }

        /// <summary>How many recent noises are kept for the gizmos.</summary>
        public const int RecentCapacity = 8;

        [Tooltip("Every constant of the hearing formula. Empty falls back to the defaults (and logs a warning).")]
        [SerializeField] private HearingConfig config;
        [Tooltip("Noise.asset in Assets/ScriptableObjects/Events.")]
        [SerializeField] private NoiseEventChannel noiseChannel;

        private static readonly Func<float> DefaultClock = () => Time.time;

        private readonly HeardNoise[] recent = new HeardNoise[RecentCapacity];
        private int recentCount;
        private int recentNext;
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
        /// <summary>How loud the remembered noise was when heard (after walls, before fading).</summary>
        public float HeardLoudness => Memory.HeardLoudness;
        public Vector3 NoisePosition => Memory.Position;
        public NoiseType NoiseType => Memory.Type;

        /// <summary>Walls counted for the last noise that reached the raycast (for the H4 debug text). -1 before any.</summary>
        public int LastWallCount { get; private set; } = -1;

        /// <summary>Walls the remembered noise came through. -1 before any noise is remembered.</summary>
        public int NoiseWalls { get; private set; } = -1;

        /// <summary>How many recent noises are kept (up to <see cref="RecentCapacity"/>).</summary>
        public int RecentCount => recentCount;

        /// <summary>A recent noise, 0 being the newest. Kept in a fixed array, so recording one allocates nothing.</summary>
        public HeardNoise GetRecent(int index)
        {
            if (index < 0 || index >= recentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return recent[(recentNext - 1 - index + RecentCapacity) % RecentCapacity];
        }

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
                Record(noise, now, openSpace, -1, false);
                return false;
            }

            occlusion ??= new RaycastOcclusionCounter(values.occluders);
            int walls = occlusion.CountWalls(noise.position + Vector3.up * values.sourceHeight,
                listener + Vector3.up * values.listenerHeight, values.maxWalls);
            LastWallCount = walls;

            float perceived = openSpace * HearingMath.ThroughWalls(walls, values.wallFactor);
            bool remembered = HearingMath.IsAudible(perceived, values.threshold)
                              && Memory.Offer(noise.position, noise.type, perceived, now);
            Record(noise, now, perceived, walls, remembered);
            if (remembered)
            {
                NoiseWalls = walls;
            }

            return remembered;
        }

        private void Record(in NoiseEvent noise, float time, float perceived, int walls, bool remembered)
        {
            recent[recentNext] = new HeardNoise(noise, time, perceived, walls, remembered);
            recentNext = (recentNext + 1) % RecentCapacity;
            recentCount = Mathf.Min(recentCount + 1, RecentCapacity);
        }

        /// <summary>
        /// Selected Hunter only (docs/reference/systems-engineer.md section 16): every noise of the last 8 s as a ring
        /// of its radius in the prototype's colour, fading with age; a dimmer ring at the distance it could be heard
        /// through the walls counted; and a line to the Hunter's ears, green if it became the remembered noise.
        /// </summary>
        private void OnDrawGizmosSelected() => DrawGizmos(UnityGizmoPainter.Instance);

        /// <summary>The noise-radius gizmos, drawn with <paramref name="painter"/> (tests record them).</summary>
        public void DrawGizmos(IGizmoPainter painter)
        {
            // Drawing must not create the fallback config (and its warning) just because the object is selected.
            HearingConfig values = config != null ? config : fallbackConfig;
            if (values == null || recentCount == 0)
            {
                return;
            }

            float now = clock();
            Vector3 ears = transform.position + Vector3.up * values.listenerHeight;
            for (int i = 0; i < recentCount; i++)
            {
                HeardNoise noise = GetRecent(i);
                float fade = 1f - (now - noise.Time) / values.memorySeconds;
                if (fade <= 0f)
                {
                    continue;
                }

                Vector3 ground = noise.Position + Vector3.up * 0.1f;
                Color colour = HunterGizmos.ForNoise(noise.Type);
                painter.Color = HunterGizmos.Faded(colour, fade);
                HunterGizmos.Circle(painter, ground, noise.Radius);
                if (noise.Walls >= 0)
                {
                    painter.Color = HunterGizmos.Faded(colour, fade * 0.5f);
                    HunterGizmos.Circle(painter, ground, HearingMath.MaxHeardDistance(noise.Radius, noise.Walls,
                        values.wallFactor, values.hearing, values.threshold));
                }

                painter.Color = HunterGizmos.Faded(noise.Remembered ? HunterGizmos.Heard : HunterGizmos.NotHeard, fade);
                painter.Line(noise.Position + Vector3.up * values.sourceHeight, ears);
            }
        }

        private bool IsOwnNoise(GameObject source)
        {
            return source != null && (source == gameObject || source.transform.IsChildOf(transform));
        }
    }
}
