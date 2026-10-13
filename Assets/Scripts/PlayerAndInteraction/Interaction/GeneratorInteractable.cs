// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace Blackout.Interaction
{
    /// <summary>
    /// The generator's control console: hold E for 2 s to disable it. That raises GeneratorDisabled (lighting,
    /// the lab maglock, the Core Developer's GeneratorVisuals and the agents all react to the event) and publishes a
    /// 60 m Generator noise. It sits on the console beside the nested Generator_Visual, which it never edits.
    /// Audio hooks: random sparks while holding, and one call when it shuts down.
    /// </summary>
    public class GeneratorInteractable : Interactable
    {
        // Prototype: a tick at random, about 6 per second, while E is held.
        private const float SparksPerSecond = 6f;
        private const string PromptText = "Disable generator";
        private const string HoldKey = "Hold E";

        [SerializeField] private VoidEventChannel generatorDisabled;
        [SerializeField] private NoiseEventChannel noiseChannel;
        [SerializeField, Min(0f)] private float holdTime = 2f;
        [SerializeField, Min(0f)] private float noiseRadius = 60f;
        [Tooltip("Where the noise comes from (the generator body). Defaults to this object.")]
        [SerializeField] private Transform noiseOrigin;

        [Header("Audio hooks")]
        [Tooltip("Random sparks while E is held (spark or tick sound).")]
        public UnityEvent onHoldSpark = new UnityEvent();
        [Tooltip("Once, when the generator shuts down (power-down sound; the hum stop is in GeneratorVisuals).")]
        public UnityEvent onDisabled = new UnityEvent();

        public bool IsRunning { get; private set; } = true;

        public override string Prompt => PromptText;
        public override string Key => HoldKey;
        public override float HoldTime => holdTime;
        public override bool CanInteract => IsRunning;

        private void OnEnable()
        {
            if (generatorDisabled != null)
            {
                generatorDisabled.OnRaised += OnGeneratorDisabled; // also covers a debug Raise from the asset
            }
        }

        private void OnDisable()
        {
            if (generatorDisabled != null)
            {
                generatorDisabled.OnRaised -= OnGeneratorDisabled;
            }
        }

        public override void WhileHeld(float progress, float dt)
        {
            if (Random.value < dt * SparksPerSecond)
            {
                onHoldSpark.Invoke();
            }
        }

        public override void Interact(GameObject by)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            Vector3 origin = noiseOrigin != null ? noiseOrigin.position : transform.position;
            if (noiseChannel != null)
            {
                noiseChannel.Raise(new NoiseEvent(origin, noiseRadius, NoiseType.Generator, by));
            }

            if (generatorDisabled != null)
            {
                generatorDisabled.Raise();
            }

            onDisabled.Invoke();
        }

        private void OnGeneratorDisabled()
        {
            IsRunning = false;
        }
    }
}
