using NUnit.Framework;
using UnityEngine;

namespace Blackout.Player.Tests
{
    public class PlayerMovementLogicTests
    {
        private const float Tolerance = 0.001f;
        private const float Frame = 1f / 60f;

        private PlayerMovementSettings settings;

        [SetUp]
        public void SetUp()
        {
            // Defaults are the prototype's numbers (walk 4.3, sprint 7, strides 0.78 / 1.05, noise 4.5 / 13).
            settings = ScriptableObject.CreateInstance<PlayerMovementSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        // ---- state ---------------------------------------------------------------

        [Test]
        public void ChooseState_InTheAir_IsAirborne()
        {
            Assert.AreEqual(MovementState.Airborne, PlayerMovementLogic.ChooseState(false, Vector2.up, true));
        }

        [Test]
        public void ChooseState_NoInput_IsIdle()
        {
            Assert.AreEqual(MovementState.Idle, PlayerMovementLogic.ChooseState(true, Vector2.zero, true));
        }

        [Test]
        public void ChooseState_ForwardWithSprint_IsSprint()
        {
            Assert.AreEqual(MovementState.Sprint, PlayerMovementLogic.ChooseState(true, Vector2.up, true));
        }

        [Test]
        public void ChooseState_SprintWhileStrafingOrBackingUp_OnlyWalks()
        {
            Assert.AreEqual(MovementState.Walk, PlayerMovementLogic.ChooseState(true, Vector2.right, true));
            Assert.AreEqual(MovementState.Walk, PlayerMovementLogic.ChooseState(true, Vector2.down, true));
        }

        [Test]
        public void TargetSpeed_SprintIsFaster_AirUsesWalkSpeed()
        {
            Assert.AreEqual(7f, PlayerMovementLogic.TargetSpeed(MovementState.Sprint, settings));
            Assert.AreEqual(4.3f, PlayerMovementLogic.TargetSpeed(MovementState.Walk, settings));
            Assert.AreEqual(4.3f, PlayerMovementLogic.TargetSpeed(MovementState.Airborne, settings));
        }

        [Test]
        public void Acceleration_GroundWithInput_GroundWithout_Air()
        {
            Assert.AreEqual(38f, PlayerMovementLogic.Acceleration(true, true, settings));
            Assert.AreEqual(30f, PlayerMovementLogic.Acceleration(true, false, settings));
            Assert.AreEqual(7f, PlayerMovementLogic.Acceleration(false, true, settings));
        }

        // ---- velocity ------------------------------------------------------------

        [Test]
        public void WishVelocity_FollowsYaw()
        {
            Vector2 north = PlayerMovementLogic.WishVelocity(Vector2.up, 0f, 4.3f);
            Assert.AreEqual(0f, north.x, Tolerance);
            Assert.AreEqual(4.3f, north.y, Tolerance);

            // Facing +X (yaw 90), forward input moves along +X and strafing right moves along -Z.
            Vector2 east = PlayerMovementLogic.WishVelocity(Vector2.up, 90f, 4.3f);
            Assert.AreEqual(4.3f, east.x, Tolerance);
            Assert.AreEqual(0f, east.y, Tolerance);
            Vector2 strafe = PlayerMovementLogic.WishVelocity(Vector2.right, 90f, 4.3f);
            Assert.AreEqual(0f, strafe.x, Tolerance);
            Assert.AreEqual(-4.3f, strafe.y, Tolerance);
        }

        [Test]
        public void WishVelocity_DiagonalIsNotFaster()
        {
            Vector2 diagonal = PlayerMovementLogic.WishVelocity(new Vector2(1f, 1f), 0f, 4.3f);
            Assert.AreEqual(4.3f, diagonal.magnitude, Tolerance);
        }

        [Test]
        public void Accelerate_IsLimitedByAccelerationTimesDt()
        {
            Vector2 v = PlayerMovementLogic.Accelerate(Vector2.zero, new Vector2(0f, 4.3f), 38f, 0.1f);
            Assert.AreEqual(3.8f, v.magnitude, Tolerance);

            v = PlayerMovementLogic.Accelerate(v, new Vector2(0f, 4.3f), 38f, 0.1f);
            Assert.AreEqual(4.3f, v.y, Tolerance, "does not overshoot the target");
        }

        [Test]
        public void Accelerate_FromWalkToStop_TakesAboutAFifthOfASecond()
        {
            Vector2 v = new Vector2(0f, 4.3f);
            float time = 0f;
            while (v.sqrMagnitude > 0f && time < 1f)
            {
                v = PlayerMovementLogic.Accelerate(v, Vector2.zero, settings.groundDeceleration, Frame);
                time += Frame;
            }

            // 4.3 m/s at 30 m/s² = 0.143 s, rounded up to whole frames.
            Assert.AreEqual(0.15f, time, 0.01f);
        }

        // ---- noise ---------------------------------------------------------------

        [Test]
        public void FootstepLoudness_MatchesPrototypeAtWalkAndSprint()
        {
            Assert.AreEqual(0f, PlayerMovementLogic.FootstepLoudness(0f, settings), Tolerance);
            Assert.AreEqual(4.5f, PlayerMovementLogic.FootstepLoudness(4.3f, settings), Tolerance);
            Assert.AreEqual(13f, PlayerMovementLogic.FootstepLoudness(7f, settings), Tolerance);
        }

        [Test]
        public void FootstepLoudness_ScalesWithSpeedAndClampsAboveSprint()
        {
            Assert.AreEqual(2.25f, PlayerMovementLogic.FootstepLoudness(2.15f, settings), Tolerance);
            Assert.AreEqual(8.75f, PlayerMovementLogic.FootstepLoudness(5.65f, settings), Tolerance);
            Assert.AreEqual(13f, PlayerMovementLogic.FootstepLoudness(20f, settings), Tolerance);
        }

        // ---- footsteps -----------------------------------------------------------

        [Test]
        public void Footsteps_OneStepPerStride_KeepingTheRemainder()
        {
            var steps = new FootstepTracker(settings);
            Assert.IsFalse(steps.Advance(true, false, 0.5f, 4.3f));
            Assert.IsTrue(steps.Advance(true, false, 0.5f, 4.3f), "1.0 m > 0.78 m walk stride");
            // 0.22 m carried over, so 0.6 m more is enough for the next step (0.82 m).
            Assert.IsTrue(steps.Advance(true, false, 0.6f, 4.3f));
            Assert.AreEqual(2, steps.StepCount);
        }

        [Test]
        public void Footsteps_SprintStrideIsLonger()
        {
            var steps = new FootstepTracker(settings);
            Assert.IsFalse(steps.Advance(true, true, 1f, 7f), "1.0 m < 1.05 m sprint stride");
            Assert.IsTrue(steps.Advance(true, true, 0.1f, 7f));
        }

        [Test]
        public void Footsteps_NoneInTheAirOrWhenCreeping()
        {
            var steps = new FootstepTracker(settings);
            Assert.IsFalse(steps.Advance(false, false, 5f, 4.3f));
            Assert.IsFalse(steps.Advance(true, false, 5f, 0.5f), "below the 0.6 m/s minimum");
            Assert.AreEqual(0, steps.StepCount);
        }

        // ---- camera feel ---------------------------------------------------------

        [Test]
        public void HeadBob_GrowsWithSpeedAndFadesInTheAir()
        {
            var bob = new HeadBob(settings);
            for (int i = 0; i < 120; i++)
            {
                bob.Update(true, 7f * Frame, settings.sprintStride, 7f, Frame);
            }

            Assert.AreEqual(1.6f, bob.Amplitude, 0.01f, "sprint: 7 / 4.3 = 1.63, capped at 1.6");

            for (int i = 0; i < 120; i++)
            {
                bob.Update(false, 7f * Frame, settings.sprintStride, 7f, Frame);
            }

            Assert.AreEqual(0f, bob.Amplitude, 0.01f);
            Assert.AreEqual(0f, bob.Offset.magnitude, 0.001f);
        }

        [Test]
        public void LandingSpring_SoftLandingIsIgnored()
        {
            var spring = new LandingSpring(settings);
            Assert.IsFalse(spring.Impact(2f));
            spring.Update(Frame);
            Assert.AreEqual(0f, spring.Offset);
        }

        [Test]
        public void LandingSpring_HardLandingDipsThenSettles()
        {
            var spring = new LandingSpring(settings);
            Assert.IsTrue(spring.Impact(8f));

            float lowest = 0f;
            for (int i = 0; i < 120; i++)
            {
                spring.Update(Frame);
                lowest = Mathf.Min(lowest, spring.Offset);
            }

            Assert.Less(lowest, -0.05f, "the camera dips");
            Assert.AreEqual(0f, spring.Offset, 0.005f, "and is back after 2 s");
        }

        [Test]
        public void LandingSpring_KickIsCapped()
        {
            var hard = new LandingSpring(settings);
            var extreme = new LandingSpring(settings);
            hard.Impact(8.75f); // 8.75 × 0.32 = 2.8, the cap
            extreme.Impact(30f);
            hard.Update(Frame);
            extreme.Update(Frame);
            Assert.AreEqual(hard.Offset, extreme.Offset, Tolerance);
        }

        [Test]
        public void Damp_IsFrameRateIndependent()
        {
            float at30 = 0f;
            float at120 = 0f;
            for (int i = 0; i < 30; i++)
            {
                at30 = PlayerMovementLogic.Damp(at30, 1f, 8f, 1f / 30f);
            }

            for (int i = 0; i < 120; i++)
            {
                at120 = PlayerMovementLogic.Damp(at120, 1f, 8f, 1f / 120f);
            }

            Assert.AreEqual(at30, at120, Tolerance);
        }
    }
}
