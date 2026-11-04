// GN1: right mouse button with the SMG looks through its red-dot sight. A transient camera/HUD effect; weapon simulation stays on.
using Blackout.Hud;
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// While the SMG's own aim pose is fully raised (<see cref="SmgAimPose.Blend"/>), the camera narrows a little, the normal crosshair goes out, the gun and
    /// arms are hidden (you look through the optic, not at it) and <see cref="RedDotOverlay"/> draws the window with the red dot. Every condition that already
    /// cancels the aim (reload, sprint, pause, focus or cursor loss, death, switching, cinematics) therefore also ends this view, because it reads that blend.
    /// It runs after <c>SmgAimPose</c> (order 30) so it uses this frame's blend, and after SniperScope's per-frame idle reset so that cannot undo it; while the
    /// aim is down it touches nothing, so the rifle's scope keeps its own FOV, crosshair and hands. Hiding goes through <see cref="ViewmodelHands.SetSightHidden"/>, which does not end the aim that caused it.
    /// </summary>
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    public sealed class RedDotScope : MonoBehaviour
    {
        private const float OverlayThreshold = 0.85f;

        [SerializeField] private SmgAimPose pose;
        [SerializeField] private ViewmodelHands hands;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject overlay;
        [SerializeField] private CanvasGroup crosshair;
        [SerializeField, Range(1f, 110f)] private float scopedFieldOfView = 58f;

        private float normalFieldOfView;
        private bool initialized;
        private bool wasActive;

        public bool IsScoped { get; private set; }

        private void Awake()
        {
            if (pose == null || hands == null || playerCamera == null || overlay == null || crosshair == null)
            {
                Debug.LogError("[Blackout] RedDotScope needs the SMG aim pose, hands, camera and HUD references assigned.", this);
                enabled = false;
                return;
            }
            normalFieldOfView = playerCamera.fieldOfView;
            initialized = true;
            Apply(0f);
        }

        private void LateUpdate() => Tick();

        /// <summary>One frame: follows the SMG aim blend (also called by the tests).</summary>
        public void Tick()
        {
            if (!initialized) return;
            Apply(pose != null && pose.isActiveAndEnabled ? pose.Blend : 0f);
        }

        private void OnDisable()
        {
            if (initialized) Apply(0f);
        }

        private void Apply(float blend)
        {
            bool active = blend > 0.001f;
            if (!active && !wasActive) return;   // idle: touch nothing, so the sniper scope keeps its own FOV, crosshair and hands
            wasActive = active;
            IsScoped = blend > OverlayThreshold;
            playerCamera.fieldOfView = Mathf.Lerp(normalFieldOfView, scopedFieldOfView, blend);
            if (overlay.activeSelf != IsScoped) overlay.SetActive(IsScoped);
            crosshair.alpha = IsScoped ? 0f : 1f;
            hands.SetSightHidden(IsScoped);
        }
    }
}
