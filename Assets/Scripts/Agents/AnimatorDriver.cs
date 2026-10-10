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
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int AimingParam = Animator.StringToHash("Aiming");
        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DeadParam = Animator.StringToHash("Dead");

        private Animator animator;
        private IAgentVelocitySource velocitySource;

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
            if (velocitySource == null)
            {
                Debug.LogWarning($"{name}: AnimatorDriver found no IAgentVelocitySource on a parent (expected the agent's mover, e.g. PathFollower). Speed stays 0.", this);
            }
        }

        protected override void OnApply(in AgentIntent intent)
        {
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
            }
        }

        protected override void OnLiveTick(float dt)
        {
            if (animator != null && velocitySource != null)
            {
                animator.SetFloat(SpeedParam, velocitySource.Speed);
            }
        }
    }
}
