// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// Muzzle flash of one gun: shows its flash quads for 0.05 s with a random size and roll, and lights the
    /// shared flash light at the muzzle for 0.06 s. Both guns share one Light (as in the prototype), so firing
    /// never adds lights to the scene. Sits on the gun's muzzle point.
    /// </summary>
    public class MuzzleFlash : MonoBehaviour
    {
        private const float VisibleTime = 0.05f;
        private const float LightTime = 0.06f;
        // Prototype: light intensity 3.2 × the weapon's flash scale.
        private const float LightIntensityPerScale = 3.2f;

        [Tooltip("Child with the crossed flash quads. Hidden except while flashing.")]
        [SerializeField] private Transform visual;
        [Tooltip("The point light shared by every gun. Optional.")]
        [SerializeField] private Light flashLight;

        private Vector3 baseScale = Vector3.one;
        private float visualTimer;
        private float lightTimer;

        private void Awake()
        {
            if (visual != null)
            {
                baseScale = visual.localScale;
                visual.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // The gun was switched away mid-flash.
            visualTimer = 0f;
            lightTimer = 0f;
            Hide();
        }

        /// <summary>Flashes at the weapon's flash scale (0 = no flash).</summary>
        public void Flash(float scale)
        {
            if (scale <= 0f)
            {
                return;
            }

            if (visual != null)
            {
                visual.localScale = baseScale * (scale * Random.Range(0.8f, 1.2f));
                visual.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                visual.gameObject.SetActive(true);
                visualTimer = VisibleTime;
            }

            if (flashLight != null)
            {
                flashLight.transform.position = transform.position;
                flashLight.intensity = LightIntensityPerScale * scale;
                flashLight.enabled = true;
                lightTimer = LightTime;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (visualTimer > 0f)
            {
                visualTimer -= dt;
                if (visualTimer <= 0f && visual != null)
                {
                    visual.gameObject.SetActive(false);
                }
            }

            if (lightTimer > 0f)
            {
                lightTimer -= dt;
                if (lightTimer <= 0f && flashLight != null)
                {
                    flashLight.enabled = false;
                }
            }
        }

        private void Hide()
        {
            if (visual != null)
            {
                visual.gameObject.SetActive(false);
            }

            if (flashLight != null)
            {
                flashLight.enabled = false;
            }
        }
    }
}
