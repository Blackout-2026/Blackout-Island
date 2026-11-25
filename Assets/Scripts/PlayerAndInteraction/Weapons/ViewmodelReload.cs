// Owner: Systems Engineer. RL1: play the fitted pack clip at the existing ammo timer's progress.
using UnityEngine;

namespace Blackout.Weapons
{
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    public sealed class ViewmodelReload : MonoBehaviour
    {
        [SerializeField] private WeaponController weapons;
        [SerializeField] private WeaponData data;
        [SerializeField] private GameObject normalVisual;
        [SerializeField] private GameObject reloadVisual;
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip clip;
        [Tooltip("Original humanoid clip used to bake the fitted Generic animation; retained for provenance.")]
        [SerializeField] private AnimationClip sourceClip;

        private WeaponAmmo ammo;
        public bool ShowingReload => reloadVisual != null && reloadVisual.activeSelf;
        public AnimationClip Clip => clip;
        public AnimationClip SourceClip => sourceClip;

        private void LateUpdate()
        {
            if (weapons == null || data == null) return;
            if (ammo == null) ammo = weapons.AmmoOf(data);
            if (ammo != null && ammo.Reloading && weapons.ActiveWeapon == data)
                ApplyAt(1f - ammo.ReloadRemaining / Mathf.Max(0.0001f, data.reloadTime));
            else ResetVisual();
        }

        /// <summary>Samples the reload pose. Also used by static review tools; only visual transforms change.</summary>
        public void ApplyAt(float progress)
        {
            if (normalVisual == null || reloadVisual == null || animator == null || clip == null) return;
            if (!reloadVisual.activeSelf) reloadVisual.SetActive(true);
            // Explicit transform-curve sampling keeps the pose on the ammo clock, including pause/restores.
            // No controller, root motion, animation events or second clock drives this visual rig.
            if (animator.enabled) animator.enabled = false;
            if (normalVisual.activeSelf) normalVisual.SetActive(false);
            clip.SampleAnimation(reloadVisual, Mathf.Clamp01(progress) * clip.length);
        }

        public void ResetVisual()
        {
            if (normalVisual != null && !normalVisual.activeSelf) normalVisual.SetActive(true);
            if (reloadVisual != null && reloadVisual.activeSelf) reloadVisual.SetActive(false);
        }

        private void OnDisable() => ResetVisual();

    }
}
