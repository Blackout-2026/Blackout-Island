// Owner: Agent Controller.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Shared timing between the two <see cref="IAgentAnimator"/> implementations (A3): the humanoid
    /// <see cref="AnimatorDriver"/> and the robot <see cref="ProceduralRobotAnimator"/>. Each decides what its
    /// own "collapse" looks like (a clip versus procedural joint rotation), but once dead both fade and
    /// disable on the same clock, taken from the prototype (docs/reference/agent-controller.md section 5.2):
    /// the material starts fading 3.2 s after death and takes 1.6 s. Scaling the visual to zero rather than
    /// touching a material keeps the fade independent of whatever surface type the Core Developer's materials
    /// end up using, which this role does not own.
    /// Mirrors <see cref="PathFollower"/>'s Update/Tick split so the timing is testable without real time.
    /// </summary>
    public abstract class AgentVisualAnimatorBase : MonoBehaviour, IAgentAnimator
    {
        public const float FadeStartDelay = 3.2f;
        public const float FadeDuration = 1.6f;

        [Tooltip("Scaled to zero and disabled once dead. Empty means this object.")]
        [SerializeField] private Transform visualRoot;

        private bool isDead;
        private float deathElapsed;
        private Vector3 restScale = Vector3.one;

        /// <summary>The transform the death fade scales down. Defaults to this component's own transform.</summary>
        protected Transform VisualRoot => visualRoot != null ? visualRoot : transform;

        protected virtual void Awake()
        {
            restScale = VisualRoot.localScale;
        }

        /// <inheritdoc />
        public void Apply(in AgentIntent intent)
        {
            if (isDead)
            {
                return;
            }

            OnApply(intent);
        }

        /// <inheritdoc />
        public void PlayHit(in DamageInfo info)
        {
            if (isDead)
            {
                return;
            }

            OnHit(info);
        }

        /// <inheritdoc />
        public void PlayDeath()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            deathElapsed = 0f;
            OnDeathBegin();
        }

        protected virtual void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>One frame of behaviour. Update calls it every frame; public so tests can drive time.</summary>
        public void Tick(float dt)
        {
            if (isDead)
            {
                deathElapsed += dt;
                OnDeathTick(deathElapsed);
                ApplyFade(deathElapsed);
            }
            else
            {
                OnLiveTick(dt);
            }
        }

        private void ApplyFade(float secondsSinceDeath)
        {
            VisualRoot.localScale = restScale * FadeScale(secondsSinceDeath);

            // Checked on elapsed time, not the computed scale: a lerp is not guaranteed to land on exactly
            // zero, and a visual stuck active at a near-invisible scale would be a silent, hard-to-spot bug.
            if (secondsSinceDeath >= FadeStartDelay + FadeDuration && VisualRoot.gameObject.activeSelf)
            {
                VisualRoot.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 1 before the fade starts, lerping to 0 over <see cref="FadeDuration"/>, 0 after. A pure function so
        /// the timing is testable without a running game loop.
        /// </summary>
        public static float FadeScale(float secondsSinceDeath)
        {
            if (secondsSinceDeath <= FadeStartDelay)
            {
                return 1f;
            }

            float t = (secondsSinceDeath - FadeStartDelay) / FadeDuration;
            return 1f - Mathf.Clamp01(t);
        }

        /// <summary>A new intent from the brain (aim, look target, speed class). Not called once dead.</summary>
        protected abstract void OnApply(in AgentIntent intent);
        /// <summary>A hit was taken. Not called once dead (a dead agent cannot be hit again).</summary>
        protected abstract void OnHit(in DamageInfo info);
        /// <summary>Death just started: play whatever makes the collapse begin (a bool/trigger, or nothing if the pose is purely a function of elapsed time).</summary>
        protected abstract void OnDeathBegin();
        /// <summary>Called every frame while alive, before the next <see cref="OnApply"/>. Default does nothing (the humanoid driver needs it for Speed; the robot needs it for the gait).</summary>
        protected virtual void OnLiveTick(float dt) { }
        /// <summary>Called every frame after death with the total elapsed time, for a subclass's own collapse pose.</summary>
        protected virtual void OnDeathTick(float secondsSinceDeath) { }
    }
}
