// Owner: Systems Engineer. TF1: visual-only index-finger pull on accepted shots.
using UnityEngine;
using Blackout.Player;

namespace Blackout.Weapons
{
    [DisallowMultipleComponent]
    public sealed class TriggerFinger : MonoBehaviour
    {
        public const string ShapeName = "TriggerPull";
        [SerializeField] private WeaponController weapons;
        [SerializeField] private WeaponData data;
        [SerializeField] private ViewmodelHands hands;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private SkinnedMeshRenderer fingerVisual;
        [SerializeField, Min(.01f)] private float returnTime = .14f;

        private int shape = -1;
        private float remaining;
        private int shotFrame = -1;
        public float Pull => shape < 0 ? 0 : fingerVisual.GetBlendShapeWeight(shape) / 100f;

        private void Awake()
        {
            if (fingerVisual != null && fingerVisual.sharedMesh != null)
                shape = fingerVisual.sharedMesh.GetBlendShapeIndex(ShapeName);
            if (shape < 0 || weapons == null || data == null || hands == null || input == null)
            {
                Debug.LogError("[TF1] Trigger finger needs its weapon, hands and baked holding shape.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (weapons == null) return;
            weapons.ShotFired += OnShot;
            weapons.WeaponChanged += OnWeaponChanged;
            weapons.SwitchStarted += ResetPull;
            weapons.AmmoChanged += OnAmmoChanged;
        }

        private void OnDisable()
        {
            if (weapons != null)
            {
                weapons.ShotFired -= OnShot;
                weapons.WeaponChanged -= OnWeaponChanged;
                weapons.SwitchStarted -= ResetPull;
                weapons.AmmoChanged -= OnAmmoChanged;
            }
            ResetPull();
        }

        private void OnShot(WeaponData fired)
        {
            if (fired != data || !weapons.isActiveAndEnabled || !hands.Shown || Time.timeScale <= 0) return;
            remaining = returnTime;
            shotFrame = Time.frameCount;
            Apply(1);
        }

        private void LateUpdate() => Tick(Time.deltaTime);

        /// <summary>Advance presentation; a fresh shot must survive even a long firing frame.</summary>
        public void Tick(float dt)
        {
            if (!isActiveAndEnabled) return;
            if (!weapons.isActiveAndEnabled || weapons.ActiveWeapon != data || weapons.IsSwitching
                || !hands.Shown || Time.timeScale <= 0) { ResetPull(); return; }
            if (remaining <= 0) return;
            // Update fires before LateUpdate. Do not spend this entire frame's dt on a shot
            // that just happened: at low FPS that could erase the pull before rendering it.
            if (shotFrame == Time.frameCount || (input.isActiveAndEnabled && input.FireHeld))
            {
                Apply(1);
                return;
            }
            remaining = Mathf.Max(0, remaining - dt);
            Apply(Mathf.SmoothStep(0, 1, remaining / returnTime));
        }

        private void OnAmmoChanged()
        {
            if (weapons.AmmoOf(data)?.Reloading ?? false) ResetPull();
        }

        private void OnWeaponChanged(WeaponData _) => ResetPull();
        private void OnApplicationFocus(bool focused) { if (!focused) ResetPull(); }

        private void ResetPull()
        {
            remaining = 0;
            shotFrame = -1;
            Apply(0);
        }

        private void Apply(float amount)
        {
            if (shape >= 0) fingerVisual.SetBlendShapeWeight(shape, amount * 100);
        }
    }
}
