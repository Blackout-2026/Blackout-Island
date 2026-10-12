// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// A small fixed-size pool of LineRenderer tracers for agent gunfire (A4, master prompt: "use pooling for
    /// bullets, tracers and FX"), so firing never instantiates at runtime. Call Show(from, to); each tracer
    /// hides itself again after its lifetime. Optional on AgentShooter — a tracer is a visual nicety, not a
    /// dependency, and this safely no-ops until a tracerPrefab is assigned.
    /// </summary>
    public class AgentTracerPool : MonoBehaviour
    {
        [SerializeField] private LineRenderer tracerPrefab;
        [Min(1)] [SerializeField] private int poolSize = 8;
        [Min(0.01f)] [SerializeField] private float lifetimeSeconds = 0.05f;

        private LineRenderer[] pool;
        private float[] expireAt;
        private int nextIndex;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            if (pool != null || tracerPrefab == null)
            {
                return; // already built, or no prefab yet (a placeholder I still need to supply)
            }

            pool = new LineRenderer[poolSize];
            expireAt = new float[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                pool[i] = Instantiate(tracerPrefab, transform);
                pool[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Shows one tracer from one pooled line to the next, round-robin. A no-op if no prefab is assigned.</summary>
        public void Show(Vector3 from, Vector3 to)
        {
            if (pool == null)
            {
                Build();
            }

            if (pool == null || pool.Length == 0)
            {
                return;
            }

            LineRenderer tracer = pool[nextIndex];
            expireAt[nextIndex] = Time.time + lifetimeSeconds;
            nextIndex = (nextIndex + 1) % pool.Length;

            tracer.gameObject.SetActive(true);
            tracer.SetPosition(0, from);
            tracer.SetPosition(1, to);
        }

        private void Update()
        {
            if (pool == null)
            {
                return;
            }

            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].gameObject.activeSelf && Time.time >= expireAt[i])
                {
                    pool[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
