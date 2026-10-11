// Owner: Core Developer.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Art
{
    /// <summary>
    /// The generator's look when the power goes out: listens to GeneratorDisabled, swaps the energy core to its
    /// "off" material, stops the hum and plays the sparks. Visuals only, no interaction: the Systems Engineer's
    /// interactable raises the event, this script just reacts to it.
    /// </summary>
    public class GeneratorVisuals : MonoBehaviour
    {
        [Header("Event")]
        [SerializeField] private VoidEventChannel generatorDisabled;

        [Header("Energy core")]
        [SerializeField] private Renderer coreRenderer;
        [Tooltip("Index of the core material on the renderer (0 when the core is its own object).")]
        [SerializeField] private int coreMaterialSlot;
        [Tooltip("The running look. Saved in the prefab (the builder fills it) so Restore Power works after a reopen.")]
        [SerializeField] private Material coreOnMaterial;
        [SerializeField] private Material coreOffMaterial;

        [Header("Sockets")]
        [SerializeField] private AudioSource humSource;
        [SerializeField] private ParticleSystem sparks;

        private VoidEventChannel boundChannel;

        public bool IsPoweredDown { get; private set; }

        private void OnEnable()
        {
            Bind(generatorDisabled);
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>Starts listening to a channel (may be null). Public so tests can bind a fake channel.</summary>
        public void Bind(VoidEventChannel channel)
        {
            Unbind();
            boundChannel = channel;
            if (boundChannel != null)
            {
                boundChannel.OnRaised += PowerDown;
            }
        }

        public void Unbind()
        {
            if (boundChannel != null)
            {
                boundChannel.OnRaised -= PowerDown;
            }

            boundChannel = null;
        }

        /// <summary>Also on the component's context menu, so the blackout look can be tested without the event.</summary>
        [ContextMenu("Test: Power Down")]
        public void PowerDown()
        {
            if (IsPoweredDown)
            {
                return;
            }

            IsPoweredDown = true;

            // Normally the builder has filled the running material. If not, remember what the core shows now, but
            // never the off material itself (that would make Restore Power a no-op).
            if (coreOnMaterial == null)
            {
                Material current = GetCoreMaterial();
                if (current != coreOffMaterial)
                {
                    coreOnMaterial = current;
                }
            }

            SetCoreMaterial(coreOffMaterial);

            if (humSource != null)
            {
                humSource.Stop();
            }

            if (sparks != null)
            {
                sparks.Play();
            }
        }

        /// <summary>Undoes PowerDown. For testing in the Editor; the game never turns the generator back on.</summary>
        [ContextMenu("Test: Restore Power")]
        public void RestorePower()
        {
            // No "already running" early exit: after a recompile or a reopen IsPoweredDown is false while the saved
            // core can still be showing the off material, and Restore Power must fix that.
            IsPoweredDown = false;
            SetCoreMaterial(coreOnMaterial);

            if (humSource != null && humSource.clip != null)
            {
                humSource.Play();
            }

            if (sparks != null)
            {
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private Material GetCoreMaterial()
        {
            if (coreRenderer == null)
            {
                return null;
            }

            Material[] materials = coreRenderer.sharedMaterials;
            return coreMaterialSlot >= 0 && coreMaterialSlot < materials.Length ? materials[coreMaterialSlot] : null;
        }

        // Runs once per blackout, so the array copy that sharedMaterials makes is not a hot-path allocation.
        private void SetCoreMaterial(Material material)
        {
            if (coreRenderer == null || material == null)
            {
                Debug.LogWarning($"{name}: GeneratorVisuals has no core renderer or core material to switch.", this);
                return;
            }

            Material[] materials = coreRenderer.sharedMaterials;
            if (coreMaterialSlot < 0 || coreMaterialSlot >= materials.Length)
            {
                Debug.LogWarning($"{name}: core material slot {coreMaterialSlot} does not exist on {coreRenderer.name}.", this);
                return;
            }

            materials[coreMaterialSlot] = material;
            coreRenderer.sharedMaterials = materials;
        }
    }
}
