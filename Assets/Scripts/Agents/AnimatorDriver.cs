// Owner: Agent Controller.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Drives a humanoid Animator Controller (Assets/Animations/Agent_Humanoid.controller, built by the
    /// AnimatorControllerBuilder Editor tool in commit 5) from the mover's realised velocity and the brain's
    /// intent. AgentRoot calls Apply/PlayHit/PlayDeath through IAgentAnimator; brains never reach this
    /// directly. Parameters on the controller: Speed (float, m/s, read every frame by the Locomotion blend
    /// tree), Aiming (bool), Hit (trigger), Dead (bool). The death collapse itself is the controller's own
    /// Death state (a clip); the fade-and-disable afterwards is the shared AgentVisualAnimatorBase.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimatorDriver : AgentVisualAnimatorBase
    {
        [Header("Optional weapon on a rigid character (Commander)")]
        [SerializeField] private Transform weapon;
        [SerializeField] private Transform weaponHand;
        [SerializeField] private Vector3 weaponGripPosition;
        [SerializeField] private Quaternion weaponGripRotation = Quaternion.identity;
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int AimingParam = Animator.StringToHash("Aiming");
        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DeadParam = Animator.StringToHash("Dead");
        private static readonly int ShootParam = Animator.StringToHash("Shoot");

        private Animator animator;
        private IAgentVelocitySource velocitySource;
        private AgentShooter shooter;
        private int weaponLayer = -1;
        private int lastShotsFired;
        private bool aiming;

        private void LateUpdate() => SyncWeaponGrip();

        /// <summary>Follow the animated hand. Also used after manual Editor animation sampling.</summary>
        public void SyncWeaponGrip()
        {
            // The rigid Commander's pistol is part of its imported hierarchy. Follow the animated
            // hand without unpacking/reparenting that FBX or changing its mesh and prefab references.
            if (weapon != null && weaponHand != null)
                weapon.SetPositionAndRotation(weaponHand.TransformPoint(weaponGripPosition), weaponHand.rotation * weaponGripRotation);
        }

        protected override void Awake()
        {
            base.Awake();
            Configure(GetComponent<Animator>(), GetComponentInParent<IAgentVelocitySource>());
        }

        /// <summary>Wires the Animator and the velocity source. Awake calls it for the real agent; tests call it directly since Awake does not run in EditMode.</summary>
        public void Configure(Animator newAnimator, IAgentVelocitySource newVelocitySource)
        {
            animator = newAnimator;
            velocitySource = newVelocitySource;
            shooter = GetComponentInParent<AgentShooter>();
            lastShotsFired = shooter != null ? shooter.ShotsFired : 0;
            weaponLayer = animator != null && animator.runtimeAnimatorController != null ? animator.GetLayerIndex("WeaponPose") : -1;
            if (velocitySource == null)
            {
                Debug.LogWarning($"{name}: AnimatorDriver found no IAgentVelocitySource on a parent (expected the agent's mover, e.g. PathFollower). Speed stays 0.", this);
            }
        }

        protected override void OnApply(in AgentIntent intent)
        {
            aiming = intent.aim;
            if (animator != null)
            {
                animator.SetBool(AimingParam, intent.aim);
            }
        }

        protected override void OnHit(in DamageInfo info)
        {
            if (animator != null)
            {
                animator.SetTrigger(HitParam);
            }
        }

        protected override void OnDeathBegin()
        {
            if (animator != null)
            {
                animator.SetBool(DeadParam, true);
                if (weaponLayer >= 0) animator.SetLayerWeight(weaponLayer, 0f);
            }
        }

        protected override void OnLiveTick(float dt)
        {
            if (animator != null && velocitySource != null)
            {
                animator.SetFloat(SpeedParam, velocitySource.Speed);
            }
            if (animator == null || weaponLayer < 0) return;
            bool reacting = animator.GetCurrentAnimatorStateInfo(0).IsName("HitReact");
            animator.SetLayerWeight(weaponLayer, aiming && !reacting ? 1f : 0f);
            // Follow actual shots, including the shooter's reaction delay and burst timing.
            if (shooter != null && shooter.ShotsFired != lastShotsFired)
            {
                lastShotsFired = shooter.ShotsFired;
                animator.SetTrigger(ShootParam);
            }
        }
    }
}
