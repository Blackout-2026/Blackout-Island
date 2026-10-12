// Owner: Systems Engineer. Add it to any agent or prop with a Health to make it flash when hit.
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>
    /// The hit-flash hook: weapons only call IDamageable.TakeDamage, Health raises OnDamaged, and this tints
    /// every renderer under the object for a moment. Weapons never need to know what they hit.
    /// The tint uses a MaterialPropertyBlock, so the shared materials are never copied or changed.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        // URP Lit / Simple Lit colour property.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Leave empty to use the Health on this object or a parent.")]
        [SerializeField] private Health health;
        [SerializeField] private Color flashColor = new Color(1f, 0.35f, 0.3f, 1f);
        [SerializeField, Min(0.01f)] private float duration = 0.08f;

        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float timeLeft;

        private void Awake()
        {
            if (health == null)
            {
                health = GetComponentInParent<Health>();
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDamaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDamaged -= OnDamaged;
            }

            timeLeft = 0f;
            Apply(false);
        }

        private void OnDamaged(DamageInfo info)
        {
            timeLeft = duration;
            Apply(true);
        }

        private void Update()
        {
            if (timeLeft <= 0f)
            {
                return;
            }

            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
            {
                Apply(false);
            }
        }

        private void Apply(bool flashing)
        {
            if (renderers == null)
            {
                return;
            }

            block.Clear();
            if (flashing)
            {
                block.SetColor(BaseColorId, flashColor);
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].SetPropertyBlock(block);
                }
            }
        }
    }
}
