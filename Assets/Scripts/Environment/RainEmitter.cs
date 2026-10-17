using UnityEngine;

namespace Blackout.Environment
{
    /// <summary>
    /// Keeps the optional rain (W9) over the player: every frame the emitter moves to a point above the main camera, and
    /// it stops emitting while something is overhead (a ceiling, the lobby roof), so no rain falls inside the bunker.
    /// Drops already falling keep going: the system simulates in world space.
    /// - The camera can live in another scene, so it is found by its MainCamera tag (Camera.main) and cached.
    /// - One upward raycast per frame, against every layer except triggers. It starts inside the player's own capsule,
    ///   which a raycast ignores.
    /// The rain is built switched off (Volumes/Atmosphere/Atmosphere_Rain). Turn the GameObject on to use it. Nothing is
    /// allocated per frame.
    /// Owner: World Builder.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public class RainEmitter : MonoBehaviour
    {
        [Tooltip("How far above the camera the drops start.")]
        [SerializeField] private float height = 12f;

        [Tooltip("Rain stops while anything is within this distance straight above the camera.")]
        [SerializeField] private float roofCheckDistance = 30f;

        private ParticleSystem rain;
        private Transform viewer;
        private bool covered;

        /// <summary>True while something above the camera stops the rain.</summary>
        public bool IsCovered => covered;

        /// <summary>Sets the heights. Used by the Editor tool that builds the rain.</summary>
        public void Configure(float newHeight, float newRoofCheckDistance)
        {
            height = newHeight;
            roofCheckDistance = newRoofCheckDistance;
        }

        private void Awake()
        {
            rain = GetComponent<ParticleSystem>();
        }

        private void LateUpdate()
        {
            if (viewer == null)
            {
                Camera main = Camera.main;
                if (main == null)
                {
                    return;
                }

                viewer = main.transform;
            }

            Vector3 eye = viewer.position;
            transform.position = eye + Vector3.up * height;

            bool roof = Physics.Raycast(eye, Vector3.up, roofCheckDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            ParticleSystem.EmissionModule emission = rain.emission;
            if (emission.enabled == roof)
            {
                emission.enabled = !roof;
            }

            covered = roof;
        }
    }
}
