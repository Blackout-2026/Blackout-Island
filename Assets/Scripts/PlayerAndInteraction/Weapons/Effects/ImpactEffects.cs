// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// Impact sparks from one shared ParticleSystem. Each hit moves the emitter to the hit point, turns it to face
    /// out of the surface and calls Emit, so a hit costs no GameObject and no allocation. The particles simulate
    /// in world space, so they stay where they were emitted when the emitter moves to the next hit.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ImpactEffects : MonoBehaviour
    {
        // Lifts the emitter off the surface so sparks do not start inside it.
        private const float SurfaceOffset = 0.02f;

        [Tooltip("Sparks per bullet impact (the prototype emits 3-9 by surface).")]
        [SerializeField, Min(1)] private int sparksPerHit = 5;

        private ParticleSystem particles;

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false; // particles only come from Emit
            particles.Play();
        }

        public void Play(Vector3 point, Vector3 normal)
        {
            Play(point, normal, sparksPerHit);
        }

        public void Play(Vector3 point, Vector3 normal, int count)
        {
            if (particles == null || count <= 0)
            {
                return;
            }

            if (normal.sqrMagnitude < 1e-6f)
            {
                normal = Vector3.up;
            }

            transform.SetPositionAndRotation(point + normal * SurfaceOffset, Quaternion.LookRotation(normal));
            particles.Emit(count);
        }
    }
}
