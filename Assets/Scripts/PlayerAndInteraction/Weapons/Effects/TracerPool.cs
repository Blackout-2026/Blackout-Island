// Owner: Systems Engineer.
using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.Weapons
{
    /// <summary>
    /// Bullet tracers: a fixed pool of LineRenderers made once in Awake and reused round-robin, so firing never
    /// instantiates or destroys anything. When all are busy the oldest one is reused (the prototype's 24-tracer
    /// pool, 0.018 m thick, fading out over 0.06 s).
    /// </summary>
    public class TracerPool : MonoBehaviour
    {
        [SerializeField, Min(1)] private int size = 24;
        [Tooltip("Additive particle material that uses vertex colour (LineRenderer colours fade the tracer).")]
        [SerializeField] private Material material;
        [SerializeField, Min(0.001f)] private float width = 0.018f;
        [Tooltip("Seconds to fade out.")]
        [SerializeField, Min(0.01f)] private float lifetime = 0.06f;
        [Tooltip("HDR so the tracer glows with bloom (prototype: 4.0, 3.2, 1.8).")]
        [SerializeField, ColorUsage(true, true)] private Color color = new Color(4f, 3.2f, 1.8f, 1f);

        private LineRenderer[] lines;
        private float[] timeLeft;
        private int next;

        private void Awake()
        {
            if (material == null)
            {
                Debug.LogError("[Blackout] TracerPool has no material assigned.", this);
                enabled = false;
                return;
            }

            lines = new LineRenderer[size];
            timeLeft = new float[size];
            for (int i = 0; i < size; i++)
            {
                lines[i] = CreateLine(i);
            }
        }

        private LineRenderer CreateLine(int index)
        {
            var child = new GameObject("Tracer " + index);
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);

            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        /// <summary>Shows a tracer from the muzzle to the hit point (or the end of the range).</summary>
        public void Show(Vector3 from, Vector3 to)
        {
            if (!enabled)
            {
                return;
            }

            LineRenderer line = lines[next];
            timeLeft[next] = lifetime;
            next = (next + 1) % size;

            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startColor = color;
            line.endColor = color;
            line.enabled = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < size; i++)
            {
                if (timeLeft[i] <= 0f)
                {
                    continue;
                }

                timeLeft[i] -= dt;
                LineRenderer line = lines[i];
                if (timeLeft[i] <= 0f)
                {
                    line.enabled = false;
                    continue;
                }

                Color faded = color;
                faded.a = timeLeft[i] / lifetime;
                line.startColor = faded;
                line.endColor = faded;
            }
        }
    }
}
