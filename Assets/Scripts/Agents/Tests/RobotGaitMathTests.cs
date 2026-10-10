using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class RobotGaitMathTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void WalkWeight_IsZeroAtStandstill_AndFullByWalkSpeed()
        {
            Assert.AreEqual(0f, RobotGaitMath.WalkWeight(0f), Tolerance);
            Assert.AreEqual(1f, RobotGaitMath.WalkWeight(1.5f), Tolerance);
            Assert.AreEqual(1f, RobotGaitMath.WalkWeight(10f), Tolerance, "clamped, never exceeds 1");
        }

        [Test]
        public void RunWeight_IsZeroBelowTheRunThreshold_AndRampsToOne()
        {
            Assert.AreEqual(0f, RobotGaitMath.RunWeight(2.3f), Tolerance);
            Assert.AreEqual(0f, RobotGaitMath.RunWeight(1f), Tolerance, "clamped, never negative");
            Assert.AreEqual(1f, RobotGaitMath.RunWeight(4.5f), Tolerance);
        }

        [Test]
        public void StrideLength_SwitchesAtThreeMetresPerSecond()
        {
            Assert.AreEqual(1.1f, RobotGaitMath.StrideLength(3f), Tolerance);
            Assert.AreEqual(1.5f, RobotGaitMath.StrideLength(3.01f), Tolerance);
        }

        [Test]
        public void AdvancePhase_OneStrideLength_AdvancesByPi()
        {
            float phase = RobotGaitMath.AdvancePhase(0f, 1.1f, 1.5f); // walking: stride 1.1

            Assert.AreEqual(Mathf.PI, phase, Tolerance);
        }

        [Test]
        public void AdvancePhase_NoMovement_DoesNotAdvance()
        {
            float phase = RobotGaitMath.AdvancePhase(1.2f, 0f, 0f);

            Assert.AreEqual(1.2f, phase, Tolerance);
        }

        [Test]
        public void ThighSwing_PeaksAtAHalfPiPhase_WithTheFullWalkAmplitude()
        {
            float swing = RobotGaitMath.ThighSwing(Mathf.PI / 2f, walkWeight: 1f, runWeight: 0f);

            Assert.AreEqual(0.32f, swing, Tolerance);
        }

        [Test]
        public void ThighSwing_AtFullRun_UsesTheRunAmplitude()
        {
            float swing = RobotGaitMath.ThighSwing(Mathf.PI / 2f, walkWeight: 0f, runWeight: 1f);

            Assert.AreEqual(0.22f, swing, Tolerance);
        }

        [Test]
        public void ThighSwing_IsZeroAtAZeroPhase()
        {
            Assert.AreEqual(0f, RobotGaitMath.ThighSwing(0f, 1f, 1f), Tolerance);
        }

        [Test]
        public void ShinBend_LiftsOnlyDuringTheForwardHalfOfTheStride()
        {
            float forwardHalf = RobotGaitMath.ShinBend(Mathf.PI / 2f, walkWeight: 1f, runWeight: 0f);
            float backwardHalf = RobotGaitMath.ShinBend(-Mathf.PI / 2f, walkWeight: 1f, runWeight: 0f);

            Assert.AreEqual(0.45f, forwardHalf, Tolerance);
            Assert.AreEqual(0f, backwardHalf, Tolerance, "the shin does not bend backwards");
        }

        [Test]
        public void PelvisStomp_HappensTwicePerStride()
        {
            // sin(phase*2) peaks at phase = PI/4 and again at phase = 3*PI/4 (once per footfall).
            float firstFootfall = RobotGaitMath.PelvisStomp(Mathf.PI / 4f, walkWeight: 1f, runWeight: 0f);
            float secondFootfall = RobotGaitMath.PelvisStomp(3f * Mathf.PI / 4f, walkWeight: 1f, runWeight: 0f);

            Assert.AreEqual(0.03f, firstFootfall, Tolerance);
            Assert.AreEqual(0.03f, secondFootfall, Tolerance);
        }

        [Test]
        public void FootCounter_OpposesTheThighAndShinBySeventyPercent()
        {
            float counter = RobotGaitMath.FootCounter(thighAngle: 0.3f, shinAngle: 0.4f);

            Assert.AreEqual(-0.7f * 0.7f, counter, Tolerance);
        }

        [Test]
        public void Smoothstep_IsZeroBelowAndOneAboveTheEdges()
        {
            Assert.AreEqual(0f, RobotGaitMath.Smoothstep(0.3f, 1.1f, 0f), Tolerance);
            Assert.AreEqual(0f, RobotGaitMath.Smoothstep(0.3f, 1.1f, 0.3f), Tolerance);
            Assert.AreEqual(1f, RobotGaitMath.Smoothstep(0.3f, 1.1f, 1.1f), Tolerance);
            Assert.AreEqual(1f, RobotGaitMath.Smoothstep(0.3f, 1.1f, 5f), Tolerance);
        }

        [Test]
        public void Smoothstep_IsHalfwayAtTheMidpoint()
        {
            Assert.AreEqual(0.5f, RobotGaitMath.Smoothstep(0f, 1f, 0.5f), Tolerance);
        }

        [Test]
        public void DeathPose_IsRestAtTheInstantOfDeath()
        {
            Assert.AreEqual(0f, RobotGaitMath.DeathPelvisDrop(0f), Tolerance);
            Assert.AreEqual(0f, RobotGaitMath.DeathPelvisPitch(0f), Tolerance);
            Assert.AreEqual(0f, RobotGaitMath.DeathHeadPitch(0f), Tolerance);
            Assert.AreEqual(0f, RobotGaitMath.DeathThighBuckle(0f), Tolerance);
            Assert.AreEqual(0f, RobotGaitMath.DeathShinBuckle(0f), Tolerance);
        }

        [Test]
        public void DeathPose_SettlesToItsFinalValues_LongAfterDeath()
        {
            float longAfter = 10f;

            Assert.AreEqual(0.45f + 0.35f, RobotGaitMath.DeathPelvisDrop(longAfter), Tolerance);
            Assert.AreEqual(1.2f, RobotGaitMath.DeathPelvisPitch(longAfter), Tolerance);
            Assert.AreEqual(0.5f, RobotGaitMath.DeathHeadPitch(longAfter), Tolerance);
            Assert.AreEqual(-0.9f, RobotGaitMath.DeathThighBuckle(longAfter), Tolerance);
            Assert.AreEqual(1.4f, RobotGaitMath.DeathShinBuckle(longAfter), Tolerance);
        }

        [Test]
        public void DeathPose_AtTheEdgeOfTheFallWindow_OnlyTheFastBuckleHasProgressed()
        {
            // t2's smoothstep window opens at 0.3 s (DeathT2 is exactly 0 at and before it), so right there
            // the pelvis has only dropped its t1 share (0.75 of 0.45 m) and has not pitched forward yet.
            Assert.AreEqual(0.75f * 0.45f, RobotGaitMath.DeathPelvisDrop(0.3f), Tolerance);
            Assert.AreEqual(0f, RobotGaitMath.DeathPelvisPitch(0.3f), Tolerance);
        }
    }
}
