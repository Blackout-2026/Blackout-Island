// Owner: Agent Controller.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Holds the one shared Blackboard in the Agents scene and keeps its world flags in step with the event
    /// channels, so no agent has to listen for GeneratorDisabled or CureTaken itself. The alert level is left to
    /// the Commander, which decides when to raise it.
    /// </summary>
    public class BlackboardHolder : MonoBehaviour
    {
        [SerializeField] private VoidEventChannel generatorDisabled;
        [SerializeField] private VoidEventChannel cureTaken;

        private VoidEventChannel boundGenerator;
        private VoidEventChannel boundCure;

        public Blackboard Instance { get; } = new Blackboard();

        private void Awake()
        {
            Bind(generatorDisabled, cureTaken);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>Starts listening to the two channels (either may be null). Public so tests can bind fake channels.</summary>
        public void Bind(VoidEventChannel generator, VoidEventChannel cure)
        {
            Unbind();
            boundGenerator = generator;
            boundCure = cure;
            if (boundGenerator != null)
            {
                boundGenerator.OnRaised += OnGeneratorDisabled;
            }
            if (boundCure != null)
            {
                boundCure.OnRaised += OnCureTaken;
            }
        }

        public void Unbind()
        {
            if (boundGenerator != null)
            {
                boundGenerator.OnRaised -= OnGeneratorDisabled;
            }
            if (boundCure != null)
            {
                boundCure.OnRaised -= OnCureTaken;
            }
            boundGenerator = null;
            boundCure = null;
        }

        private void OnGeneratorDisabled()
        {
            Instance.generatorDisabled = true;
        }

        private void OnCureTaken()
        {
            Instance.cureTaken = true;
        }
    }
}
