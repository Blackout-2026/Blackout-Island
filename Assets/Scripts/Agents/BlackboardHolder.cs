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
        [SerializeField] private VoidEventChannel playerDied;
        [SerializeField] private VoidEventChannel playerRespawned;

        private VoidEventChannel boundGenerator;
        private VoidEventChannel boundCure;
        private VoidEventChannel boundPlayerDied;
        private VoidEventChannel boundPlayerRespawned;

        public Blackboard Instance { get; } = new Blackboard();

        private void Awake()
        {
            Bind(generatorDisabled, cureTaken);
            BindPlayerLifecycle(playerDied, playerRespawned);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>Starts listening to the two channels (either may be null). Public so tests can bind fake channels.</summary>
        public void Bind(VoidEventChannel generator, VoidEventChannel cure)
        {
            UnbindWorldChannels();
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
            UnbindWorldChannels();
            BindPlayerLifecycle(null, null);
        }

        private void UnbindWorldChannels()
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
            Instance.ReportCureTaken();
        }

        public void BindPlayerLifecycle(VoidEventChannel died, VoidEventChannel respawned)
        {
            if (boundPlayerDied != null) boundPlayerDied.OnRaised -= OnPlayerDied;
            if (boundPlayerRespawned != null) boundPlayerRespawned.OnRaised -= OnPlayerRespawned;
            boundPlayerDied = died;
            boundPlayerRespawned = respawned;
            if (boundPlayerDied != null) boundPlayerDied.OnRaised += OnPlayerDied;
            if (boundPlayerRespawned != null) boundPlayerRespawned.OnRaised += OnPlayerRespawned;
        }

        private void OnPlayerDied() => Instance.playerNeutralized = true;

        private void OnPlayerRespawned()
        {
            Instance.playerNeutralized = false;
            Instance.lastKnownPlayerTime = float.NegativeInfinity;
            Instance.squadAssembled = false;
        }
    }
}
