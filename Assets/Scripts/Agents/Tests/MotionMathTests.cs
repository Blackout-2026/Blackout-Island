using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class MotionMathTests
    {
        private const float Accel = 7f;
        private const float DecelMul = 1.6f;

        [Test]
        public void StepVelocity_AcceleratesByAtMostAccelerationTimesDt()
        {
            Vector2 v = MotionMath.StepVelocity(Vector2.zero, new Vector2(4f, 0f), Accel, DecelMul, 0.1f);

            Assert.AreEqual(0.7f, v.x, 0.0001f);
            Assert.AreEqual(0f, v.y, 0.0001f);
        }

        [Test]
        public void StepVelocity_ReachesTheDesiredVelocityExactlyWhenWithinOneStep()
        {
            Vector2 v = MotionMath.StepVelocity(new Vector2(1.95f, 0f), new Vector2(2f, 0f), Accel, DecelMul, 0.1f);

            Assert.AreEqual(new Vector2(2f, 0f), v);
        }

        [Test]
        public void StepVelocity_BrakesFasterThanItAccelerates()
        {
            float speedUp = MotionMath.StepVelocity(Vector2.zero, new Vector2(4f, 0f), Accel, DecelMul, 0.1f).x;
            float slowDown = 4f - MotionMath.StepVelocity(new Vector2(4f, 0f), Vector2.zero, Accel, DecelMul, 0.1f).x;

            Assert.AreEqual(speedUp * DecelMul, slowDown, 0.0001f);
        }

        [Test]
        public void StepVelocity_ChangeOfDirectionIsAlsoLimited()
        {
            // Turning 90 degrees at constant speed needs a velocity change of about 5.66, which takes several steps.
            Vector2 v = MotionMath.StepVelocity(new Vector2(4f, 0f), new Vector2(0f, 4f), Accel, DecelMul, 0.1f);

            Assert.AreEqual(0.7f, (v - new Vector2(4f, 0f)).magnitude, 0.0001f);
        }

        [Test]
        public void StepVelocity_ZeroDt_ChangesNothing()
        {
            Vector2 v = MotionMath.StepVelocity(new Vector2(1f, 1f), new Vector2(4f, 0f), Accel, DecelMul, 0f);

            Assert.AreEqual(new Vector2(1f, 1f), v);
        }

        [Test]
        public void YawOf_UsesTheUnityConvention()
        {
            Assert.AreEqual(0f, MotionMath.YawOf(new Vector2(0f, 1f)), 0.0001f, "+Z is yaw 0");
            Assert.AreEqual(90f, MotionMath.YawOf(new Vector2(1f, 0f)), 0.0001f, "+X is yaw 90");
            Assert.AreEqual(-90f, MotionMath.YawOf(new Vector2(-1f, 0f)), 0.0001f);
            Assert.AreEqual(180f, Mathf.Abs(MotionMath.YawOf(new Vector2(0f, -1f))), 0.0001f);
        }

        [Test]
        public void TurnToward_LimitsTheTurnPerStep()
        {
            float yaw = MotionMath.TurnToward(0f, 90f, 286.5f, 0.1f);

            Assert.AreEqual(28.65f, yaw, 0.001f);
        }

        [Test]
        public void TurnToward_TakesTheShortWayAcrossTheWrapAround()
        {
            float yaw = MotionMath.TurnToward(350f, 10f, 100f, 0.1f);

            Assert.AreEqual(360f, yaw, 0.001f, "350 -> 10 is 20 degrees clockwise, not 340 anticlockwise");
        }

        [Test]
        public void TurnToward_StopsExactlyOnTheTarget()
        {
            Assert.AreEqual(45f, MotionMath.TurnToward(44f, 45f, 286.5f, 0.1f), 0.0001f);
        }

        [Test]
        public void SeparationPush_PushesAwayFromTheOther_WhenCloserThanRange()
        {
            Vector2 push = MotionMath.SeparationPush(new Vector2(1f, 0f), Vector2.zero, range: 1.3f, strength: 2.4f);

            // distance 1: (1.3 - 1) * 2.4 = 0.72, directed along +X (away from the other agent at the origin).
            Assert.AreEqual(0.72f, push.magnitude, 0.0001f);
            Assert.Greater(push.x, 0f);
            Assert.AreEqual(0f, push.y, 0.0001f);
        }

        [Test]
        public void SeparationPush_IsZero_AtOrBeyondRange()
        {
            Assert.AreEqual(Vector2.zero, MotionMath.SeparationPush(new Vector2(1.3f, 0f), Vector2.zero, 1.3f, 2.4f));
            Assert.AreEqual(Vector2.zero, MotionMath.SeparationPush(new Vector2(5f, 0f), Vector2.zero, 1.3f, 2.4f));
        }

        [Test]
        public void SeparationPush_IsZero_NotNaN_OnExactOverlap()
        {
            Vector2 push = MotionMath.SeparationPush(Vector2.zero, Vector2.zero, 1.3f, 2.4f);

            Assert.AreEqual(Vector2.zero, push);
        }

        [Test]
        public void SeparationPush_IsStrongestAtZeroDistance_ApproachingTheLimit()
        {
            Vector2 closePush = MotionMath.SeparationPush(new Vector2(0.01f, 0f), Vector2.zero, 1.3f, 2.4f);

            Assert.AreEqual((1.3f - 0.01f) * 2.4f, closePush.magnitude, 0.001f);
        }

        [Test]
        public void SeparationPush_PlayerConstants_MatchTheReference()
        {
            // docs/reference/agent-controller.md 3.2: agent-player separation, range 1.0 m, strength 3.
            Vector2 push = MotionMath.SeparationPush(new Vector2(0.5f, 0f), Vector2.zero, range: 1.0f, strength: 3f);

            Assert.AreEqual((1.0f - 0.5f) * 3f, push.magnitude, 0.0001f);
        }

        [Test]
        public void Settings_DefaultsMatchThePrototypeReference()
        {
            var settings = new MovementSettings();

            Assert.AreEqual(1.5f, settings.SpeedFor(MoveSpeed.Walk));
            Assert.AreEqual(4.0f, settings.SpeedFor(MoveSpeed.Run));
            Assert.AreEqual(0f, settings.SpeedFor(MoveSpeed.Stop));
            Assert.AreEqual(7f, settings.acceleration);
            Assert.AreEqual(5f, settings.turnRateDegrees * Mathf.Deg2Rad, 0.001f, "5 rad/s");
            Assert.AreEqual(1.6f, settings.ReplanDistanceFor(MoveSpeed.Run));
            Assert.AreEqual(0.9f, settings.ReplanDistanceFor(MoveSpeed.Walk));
        }
    }
}
