// Owner: Agent Controller.
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// The procedural gait formulas <see cref="ProceduralRobotAnimator"/> plays back, kept free of components
    /// so they are testable directly (same idea as <see cref="MotionMath"/>). Every constant cites its source:
    /// most come from docs/reference/agent-controller.md sections 9.1 (shared blend/aim timings) and 9.3
    /// (robot-specific amplitudes), which were themselves read from the prototype's source, not guessed. A few
    /// death-pose numbers are not in either reference doc and are marked [GUESS].
    /// </summary>
    public static class RobotGaitMath
    {
        /// <summary>agent-controller.md 9.1: robot walk blend = clamp01(speed / 1.5).</summary>
        public static float WalkWeight(float speed)
        {
            return Mathf.Clamp01(speed / 1.5f);
        }

        /// <summary>agent-controller.md 9.1: robot run blend = clamp01((speed - 2.3) / 2.2).</summary>
        public static float RunWeight(float speed)
        {
            return Mathf.Clamp01((speed - 2.3f) / 2.2f);
        }

        /// <summary>agent-controller.md 9.1: robot stride length, 1.1 m, or 1.5 m once speed exceeds 3 m/s.</summary>
        public static float StrideLength(float speed)
        {
            return speed > 3f ? 1.5f : 1.1f;
        }

        /// <summary>agent-controller.md 9.1: gait phase advances by distance moved / stride length * PI.</summary>
        public static float AdvancePhase(float phase, float distanceMoved, float speed)
        {
            float stride = StrideLength(speed);
            return stride > 0f ? phase + distanceMoved / stride * Mathf.PI : phase;
        }

        /// <summary>agent-controller.md 9.3 (robot specifics): leg swing amplitude = 0.32*walk + 0.22*run, radians.</summary>
        public static float ThighSwing(float phase, float walkWeight, float runWeight)
        {
            return Mathf.Sin(phase) * (0.32f * walkWeight + 0.22f * runWeight);
        }

        /// <summary>agent-controller.md 9.3: shin lift = 0.45*(walk+run), only while the leg swings forward (the lift half of the stride).</summary>
        public static float ShinBend(float phase, float walkWeight, float runWeight)
        {
            return Mathf.Max(0f, Mathf.Sin(phase)) * (0.45f * (walkWeight + runWeight));
        }

        /// <summary>agent-controller.md 9.3: pelvis stomp = 0.03*walk + 0.05*run, twice per stride (once per footfall).</summary>
        public static float PelvisStomp(float phase, float walkWeight, float runWeight)
        {
            return Mathf.Abs(Mathf.Sin(phase * 2f)) * (0.03f * walkWeight + 0.05f * runWeight);
        }

        /// <summary>core-developer.md 3.5: the foot counter-rotates 0.7x the thigh+shin angle so the sole stays level.</summary>
        public static float FootCounter(float thighAngle, float shinAngle)
        {
            return -0.7f * (thighAngle + shinAngle);
        }

        /// <summary>agent-controller.md 9.1: the aim blend damps toward the intent at this rate (shared with the humanoid driver's aimBlend).</summary>
        public const float AimDampRate = 7f;

        /// <summary>Classic GLSL-style smoothstep, used by the death pose below.</summary>
        public static float Smoothstep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>agent-controller.md 9.2 (robot death): the fast buckle timer, 0 to 1 over 0.4 s.</summary>
        public static float DeathT1(float secondsSinceDeath)
        {
            return Mathf.Clamp01(secondsSinceDeath / 0.4f);
        }

        /// <summary>agent-controller.md 9.2: the slower fall-forward timer, smoothstep(0.3, 1.1, t).</summary>
        public static float DeathT2(float secondsSinceDeath)
        {
            return Smoothstep(0.3f, 1.1f, secondsSinceDeath);
        }

        /// <summary>agent-controller.md 9.2: pelvis drops 0.45 m on t1 plus a further 0.35 m on t2.</summary>
        public static float DeathPelvisDrop(float secondsSinceDeath)
        {
            return DeathT1(secondsSinceDeath) * 0.45f + DeathT2(secondsSinceDeath) * 0.35f;
        }

        /// <summary>agent-controller.md 9.2: pelvis pitches forward up to 1.2 rad (the robot falls forward, unlike the humanoid).</summary>
        public static float DeathPelvisPitch(float secondsSinceDeath)
        {
            return DeathT2(secondsSinceDeath) * 1.2f;
        }

        /// <summary>agent-controller.md 9.2: head pitches a further 0.5 rad.</summary>
        public static float DeathHeadPitch(float secondsSinceDeath)
        {
            return DeathT2(secondsSinceDeath) * 0.5f;
        }

        /// <summary>
        /// [GUESS] Neither reference doc gives a robot thigh-buckle angle, only "thighs/shins buckle"
        /// qualitatively (core-developer.md section 3.5, moving parts table). Reuses DeathT1's timing shape
        /// with a modest angle so the pose does not look frozen; replace with a measured value once C3 lands.
        /// </summary>
        public static float DeathThighBuckle(float secondsSinceDeath)
        {
            return -DeathT1(secondsSinceDeath) * 0.9f;
        }

        /// <summary>[GUESS] see <see cref="DeathThighBuckle"/>.</summary>
        public static float DeathShinBuckle(float secondsSinceDeath)
        {
            return DeathT1(secondsSinceDeath) * 1.4f;
        }
    }
}
