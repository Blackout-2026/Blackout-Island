using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class PathFollowerTests
    {
        private const float Dt = 0.02f;

        /// <summary>A flat, endless floor: the path is the straight line (or the given corners), and every position is on the floor.</summary>
        private sealed class FakePathProvider : IPathProvider
        {
            public int planCalls;
            public bool pathExists = true;
            public bool onFloor = true;
            public bool blocked;
            public bool clearBlockOnPlan = true;
            public float floorHeight;
            public Vector3[] fixedCorners;
            public Vector3 lastDestination;

            public bool TryCalculatePath(Vector3 from, Vector3 to, out Vector3[] corners)
            {
                planCalls++;
                lastDestination = to;
                if (clearBlockOnPlan)
                {
                    blocked = false;
                }
                if (!pathExists)
                {
                    corners = null;
                    return false;
                }
                corners = fixedCorners ?? new[] { from, to };
                return true;
            }

            public bool TrySnap(Vector3 position, float maxDistance, out Vector3 snapped)
            {
                snapped = new Vector3(position.x, floorHeight, position.z);
                return onFloor;
            }

            public bool IsBlocked(Vector3 from, Vector3 to)
            {
                return blocked;
            }
        }

        private GameObject gameObject;
        private PathFollower follower;
        private FakePathProvider provider;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("FollowerTest");
            follower = gameObject.AddComponent<PathFollower>();
            provider = new FakePathProvider();
            follower.Configure(provider);
            follower.Settings.repathJitter = 0f; // deterministic timing
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        private void Run(float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                follower.Tick(Dt);
            }
        }

        private float RunUntilArrived(float timeoutSeconds, out float maxSpeed)
        {
            maxSpeed = 0f;
            float t = 0f;
            while (!follower.HasArrived && t < timeoutSeconds)
            {
                follower.Tick(Dt);
                maxSpeed = Mathf.Max(maxSpeed, follower.Speed);
                t += Dt;
            }
            return t;
        }

        [Test]
        public void WalksToTheDestinationAndArrives()
        {
            follower.SetDestination(new Vector3(10f, 0f, 0f), MoveSpeed.Walk);

            RunUntilArrived(30f, out _);

            Assert.IsTrue(follower.HasArrived);
            Assert.Less(Vector3.Distance(gameObject.transform.position, new Vector3(10f, 0f, 0f)), 0.5f);
        }

        [Test]
        public void IsAnAgentVelocitySource_ForTheAnimatorLayer()
        {
            // A3: AnimatorDriver and ProceduralRobotAnimator find the mover via GetComponentInParent<IAgentVelocitySource>.
            Assert.IsInstanceOf<IAgentVelocitySource>(follower);

            follower.SetDestination(new Vector3(10f, 0f, 0f), MoveSpeed.Walk);
            RunUntilArrived(30f, out float maxSpeed);

            IAgentVelocitySource velocitySource = follower;
            Assert.Greater(maxSpeed, 0f);
            Assert.AreEqual(follower.Speed, velocitySource.Speed, 0.0001f);
            Assert.AreEqual(follower.Velocity, velocitySource.Velocity);
        }

        [Test]
        public void NeverExceedsTheWalkSpeed_AndReachesIt()
        {
            follower.SetDestination(new Vector3(20f, 0f, 0f), MoveSpeed.Walk);

            RunUntilArrived(60f, out float maxSpeed);

            Assert.LessOrEqual(maxSpeed, 1.5f + 0.001f);
            Assert.Greater(maxSpeed, 1.45f, "a 20 m walk is long enough to reach top speed");
        }

        [Test]
        public void Running_UsesTheRunSpeed()
        {
            follower.SetDestination(new Vector3(30f, 0f, 0f), MoveSpeed.Run);

            RunUntilArrived(60f, out float maxSpeed);

            Assert.Greater(maxSpeed, 3.9f);
            Assert.LessOrEqual(maxSpeed, 4f + 0.001f);
        }

        [Test]
        public void SpeedMultiplier_RaisesTheTopSpeed()
        {
            follower.SpeedMultiplier = 1.25f; // the alert buff
            follower.SetDestination(new Vector3(30f, 0f, 0f), MoveSpeed.Walk);

            RunUntilArrived(60f, out float maxSpeed);

            Assert.AreEqual(1.5f * 1.25f, maxSpeed, 0.01f);
        }

        [Test]
        public void Accelerates_InsteadOfJumpingToTopSpeed()
        {
            follower.SetDestination(new Vector3(20f, 0f, 0f), MoveSpeed.Run);

            follower.Tick(Dt);

            Assert.AreEqual(7f * Dt, follower.Speed, 0.0001f, "one step of acceleration 7 m/s^2");
        }

        [Test]
        public void SlowsDownOnApproach_InsteadOfStoppingAbruptly()
        {
            follower.SetDestination(new Vector3(10f, 0f, 0f), MoveSpeed.Walk);
            while (gameObject.transform.position.x < 9.3f) // 0.7 m short of the destination
            {
                follower.Tick(Dt);
            }

            Assert.Less(follower.Speed, 1.5f * 0.8f, "arrival slow-down is in effect");
            Assert.Greater(follower.Speed, 0f);
        }

        [Test]
        public void FollowsAllCornersOfAnLShapedPath()
        {
            provider.fixedCorners = new[] { Vector3.zero, new Vector3(6f, 0f, 0f), new Vector3(6f, 0f, 6f) };
            // The fake returns the same corners on every plan (a real provider plans from where the agent stands),
            // so keep the routine re-plan out of this test: it is about following corners, not re-planning.
            follower.Settings.repathInterval = 999f;
            follower.SetDestination(new Vector3(6f, 0f, 6f), MoveSpeed.Walk);
            float closestToCorner = float.MaxValue;

            for (int i = 0; i < 3000 && !follower.HasArrived; i++)
            {
                follower.Tick(Dt);
                closestToCorner = Mathf.Min(closestToCorner, Vector3.Distance(gameObject.transform.position, new Vector3(6f, 0f, 0f)));
            }

            Assert.IsTrue(follower.HasArrived);
            Assert.Less(closestToCorner, 0.7f, "it went round the corner instead of cutting straight to the end");
            Assert.Less(Vector3.Distance(gameObject.transform.position, new Vector3(6f, 0f, 6f)), 0.5f);
        }

        [Test]
        public void SameDestinationEveryTick_PlansOnlyOnce()
        {
            for (int i = 0; i < 100; i++)
            {
                follower.SetDestination(new Vector3(10f, 0f, 0f), MoveSpeed.Walk);
                follower.Tick(Dt);
            }

            Assert.AreEqual(1, provider.planCalls);
        }

        [Test]
        public void SmallDestinationDrift_DoesNotReplan_ButALargeOneDoes()
        {
            follower.SetDestination(new Vector3(10f, 0f, 0f), MoveSpeed.Walk);
            Run(0.5f);

            follower.SetDestination(new Vector3(10.5f, 0f, 0f), MoveSpeed.Walk); // 0.5 m, below the 0.9 m walk threshold
            Run(0.5f);
            Assert.AreEqual(1, provider.planCalls);

            follower.SetDestination(new Vector3(12f, 0f, 0f), MoveSpeed.Walk); // 2 m from the planned one
            Run(0.5f);
            Assert.AreEqual(2, provider.planCalls);
            Assert.AreEqual(new Vector3(12f, 0f, 0f), provider.lastDestination);
        }

        [Test]
        public void Running_ToleratesMoreDriftThanWalking()
        {
            follower.SetDestination(new Vector3(20f, 0f, 0f), MoveSpeed.Run);
            Run(0.5f);

            follower.SetDestination(new Vector3(21.2f, 0f, 0f), MoveSpeed.Run); // 1.2 m: over the walk threshold, under the 1.6 m run one
            Run(0.5f);

            Assert.AreEqual(1, provider.planCalls);
        }

        [Test]
        public void ReplansOnATimerEvenWithTheSameDestination()
        {
            follower.SetDestination(new Vector3(40f, 0f, 0f), MoveSpeed.Walk);

            Run(2.0f);
            Assert.AreEqual(1, provider.planCalls);

            Run(1.0f); // past the 2.5 s repath interval
            Assert.AreEqual(2, provider.planCalls);
        }

        [Test]
        public void ReplansAtOnce_WhenTheWayToTheNextCornerIsCut()
        {
            follower.SetDestination(new Vector3(40f, 0f, 0f), MoveSpeed.Walk);
            Run(0.5f);
            Assert.AreEqual(1, provider.planCalls);

            provider.blocked = true; // a barricade was pushed into the corridor

            Run(0.6f); // well under the 2.5 s timer: a block check (every 0.25 s) found it
            Assert.AreEqual(2, provider.planCalls);
        }

        [Test]
        public void Stop_BringsTheAgentToAHalt_AndClearsThePath()
        {
            follower.SetDestination(new Vector3(40f, 0f, 0f), MoveSpeed.Run);
            Run(2f);
            Assert.Greater(follower.Speed, 1f);

            follower.Stop();
            Run(1.5f);

            Assert.AreEqual(0f, follower.Speed, 0.0001f);
            Assert.IsFalse(follower.HasArrived);
            Assert.IsNull(follower.CurrentPath);
        }

        [Test]
        public void Braking_TakesLessTimeThanAccelerating()
        {
            follower.SetDestination(new Vector3(100f, 0f, 0f), MoveSpeed.Run);
            int stepsUp = 0;
            while (follower.Speed < 3.99f)
            {
                follower.Tick(Dt);
                stepsUp++;
            }

            follower.Stop();
            int stepsDown = 0;
            while (follower.Speed > 0f)
            {
                follower.Tick(Dt);
                stepsDown++;
            }

            Assert.Less(stepsDown, stepsUp);
        }

        [Test]
        public void SetDestinationWithStopSpeed_ActsLikeStop()
        {
            follower.SetDestination(new Vector3(40f, 0f, 0f), MoveSpeed.Walk);
            Run(0.5f);

            follower.SetDestination(new Vector3(40f, 0f, 0f), MoveSpeed.Stop);
            Run(1f);

            Assert.AreEqual(0f, follower.Speed, 0.0001f);
            Assert.IsNull(follower.CurrentPath);
        }

        [Test]
        public void NoPathFound_DoesNotMove_ReportsFailure_AndRetriesLater()
        {
            provider.pathExists = false;
            follower.SetDestination(new Vector3(10f, 0f, 0f), MoveSpeed.Walk);

            Run(1f);
            Assert.IsTrue(follower.HasFailed);
            Assert.AreEqual(Vector3.zero, gameObject.transform.position);
            Assert.IsFalse(follower.HasArrived);
            Assert.AreEqual(1, provider.planCalls);

            provider.pathExists = true;
            Run(3f); // the routine re-plan tries again and succeeds
            Assert.IsFalse(follower.HasFailed);
            Assert.Greater(gameObject.transform.position.x, 0.5f);
        }

        [Test]
        public void StaysWhereItIs_WhenThereIsNoFloorAheadToStandOn()
        {
            provider.onFloor = false;
            follower.SetDestination(new Vector3(10f, 0f, 0f), MoveSpeed.Walk);

            Run(1f);

            Assert.AreEqual(Vector3.zero, gameObject.transform.position);
            Assert.AreEqual(0f, follower.Speed);
        }

        [Test]
        public void HeightFollowsTheFloor()
        {
            provider.floorHeight = 2f;
            follower.SetDestination(new Vector3(10f, 0f, 0f), MoveSpeed.Walk);

            Run(2f);

            Assert.AreEqual(2f, gameObject.transform.position.y, 0.05f);
        }

        [Test]
        public void FacesTheDirectionOfTravel()
        {
            follower.SetDestination(new Vector3(20f, 0f, 0f), MoveSpeed.Walk); // +X is yaw 90

            Run(1f);

            Assert.AreEqual(90f, gameObject.transform.eulerAngles.y, 1f);
        }

        [Test]
        public void TurnsAtALimitedRate_NotInstantly()
        {
            follower.Settings.turnRateDegrees = 100f;
            follower.SetDestination(new Vector3(0f, 0f, -20f), MoveSpeed.Walk); // behind it: yaw 180

            Run(0.5f); // speed has built up past the turn threshold; at 100 deg/s it can have turned at most about 50 degrees

            float yaw = gameObject.transform.eulerAngles.y;
            Assert.Greater(yaw, 0f, "it started turning");
            Assert.Less(yaw, 90f, "but not instantly");
        }

        [Test]
        public void SetFacing_TurnsAStandingAgentTowardTheLookTarget()
        {
            follower.SetFacing(true, new Vector3(10f, 0f, 0f), false); // +X is yaw 90

            Run(1f);

            Assert.AreEqual(90f, gameObject.transform.eulerAngles.y, 0.5f);
        }

        [Test]
        public void SetFacing_TurnsAtALimitedRate()
        {
            follower.SetFacing(true, new Vector3(10f, 0f, 0f), false);

            Run(0.1f); // 286.5 deg/s * 0.1 s = 28.65 degrees

            Assert.AreEqual(28.65f, gameObject.transform.eulerAngles.y, 0.5f);
        }

        [Test]
        public void SetFacing_Aiming_TurnsFaster()
        {
            follower.SetFacing(true, new Vector3(10f, 0f, 0f), true);

            Run(0.1f); // 286.5 * 1.4 * 0.1 = 40.1 degrees

            Assert.AreEqual(40.1f, gameObject.transform.eulerAngles.y, 0.5f);
        }

        [Test]
        public void LookTarget_WinsOverTheDirectionOfTravel()
        {
            follower.SetFacing(true, new Vector3(500f, 0f, 0f), true); // look (far away) along +X ...
            follower.SetDestination(new Vector3(0f, 0f, 20f), MoveSpeed.Walk); // ... while walking along +Z

            Run(2f);

            Assert.AreEqual(90f, gameObject.transform.eulerAngles.y, 1f);
            Assert.Greater(gameObject.transform.position.z, 1f, "it still walked");
        }

        [Test]
        public void SetFacing_WithoutALookTarget_FacesTheDirectionOfTravelAgain()
        {
            follower.SetFacing(true, new Vector3(50f, 0f, 0f), false);
            Run(1f);

            follower.SetFacing(false, Vector3.zero, false);
            follower.SetDestination(new Vector3(0f, 0f, 20f), MoveSpeed.Walk); // +Z is yaw 0
            Run(2f);

            float yaw = Mathf.DeltaAngle(0f, gameObject.transform.eulerAngles.y);
            Assert.AreEqual(0f, yaw, 1f);
        }

        [Test]
        public void AStandingAgentWithNoLookTarget_KeepsItsFacing()
        {
            gameObject.transform.rotation = Quaternion.Euler(0f, 40f, 0f);
            follower.Configure(provider); // picks up the starting yaw, as Awake does in play mode

            Run(1f);

            Assert.AreEqual(40f, gameObject.transform.eulerAngles.y, 0.01f);
        }

        private sealed class FakeSeparationSource : ISeparationSource
        {
            public Vector2 push;
            public int calls;
            public Vector3 lastPosition;
            public Transform lastSelf;

            public Vector2 ComputeSeparation(Vector3 position, Transform self)
            {
                calls++;
                lastPosition = position;
                lastSelf = self;
                return push;
            }
        }

        [Test]
        public void WithNoSeparationSourceConfigured_BehavesExactlyAsBefore()
        {
            // The default SetUp calls Configure(provider) with one argument: separationSource stays null.
            follower.SetDestination(new Vector3(10f, 0f, 0f), MoveSpeed.Walk);

            Assert.DoesNotThrow(() => Run(1f));
        }

        [Test]
        public void Separation_AddsToTheDesiredVelocity_EvenWhileStanding()
        {
            var separation = new FakeSeparationSource { push = new Vector2(2f, 0f) };
            follower.Configure(provider, separation);
            // No SetDestination: the agent is standing still, only separation should move it.

            follower.Tick(Dt);
            follower.Tick(Dt);

            Assert.AreEqual(2, separation.calls, "queried once per Tick");
            Assert.Greater(follower.Velocity.x, 0f, "separation pushed it even with no destination");
        }

        [Test]
        public void Separation_IsAccelerationLimited_LikeAnyOtherDesiredVelocity()
        {
            var separation = new FakeSeparationSource { push = new Vector2(10f, 0f) }; // far beyond any real speed
            follower.Configure(provider, separation);

            follower.Tick(Dt);

            Assert.AreEqual(7f * Dt, follower.Speed, 0.0001f, "one step of the same 7 m/s^2 acceleration, not an instant jump");
        }

        [Test]
        public void Separation_PassesTheMoversOwnTransform_SoItCanExcludeItself()
        {
            var separation = new FakeSeparationSource();
            follower.Configure(provider, separation);

            follower.Tick(Dt);

            Assert.AreSame(gameObject.transform, separation.lastSelf);
        }

        [Test]
        public void AgentRegistrySeparationSource_WithNoRegistryInTheScene_ReturnsZero()
        {
            // AgentRegistry.Current is null outside a loaded Agents scene (and in this test's isolated GameObject).
            var source = new AgentRegistrySeparationSource();

            Vector2 push = source.ComputeSeparation(Vector3.zero, gameObject.transform);

            Assert.AreEqual(Vector2.zero, push);
        }

        /// <summary>Ticks while resetting the position back to the origin afterwards, simulating something
        /// (a crowd, a corner geometry the fake provider does not know about) that blocks all real progress.</summary>
        private void RunBlocked(float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                follower.Tick(Dt);
                gameObject.transform.position = Vector3.zero;
            }
        }

        [Test]
        public void StuckDetection_NudgesAndForcesAReplan_WhenMakingNoProgress()
        {
            follower.SetDestination(new Vector3(40f, 0f, 0f), MoveSpeed.Walk);

            RunBlocked(1.5f); // just past the 1.4 s check interval

            Assert.AreEqual(1, follower.StuckCount);
            Assert.Greater(provider.planCalls, 1, "stuck detection forced a fresh plan beyond the initial one");
        }

        [Test]
        public void StuckDetection_CanTriggerMultipleTimes_IfStillBlocked()
        {
            follower.SetDestination(new Vector3(40f, 0f, 0f), MoveSpeed.Walk);

            RunBlocked(3f); // two full check intervals

            Assert.GreaterOrEqual(follower.StuckCount, 2);
        }

        [Test]
        public void StuckDetection_DoesNotTrigger_WhenMovingNormally()
        {
            follower.SetDestination(new Vector3(10f, 0f, 0f), MoveSpeed.Walk);

            RunUntilArrived(30f, out _);

            Assert.AreEqual(0, follower.StuckCount);
        }

        [Test]
        public void StuckDetection_NeverTriggers_WhileStandingStill()
        {
            // Never trying to move: the timer must never accumulate towards a stuck event, no matter how long.
            Run(5f); // well past the 1.4 s check interval, several times over

            Assert.AreEqual(0, follower.StuckCount);
        }

        [Test]
        public void StuckDetection_TimerResets_AfterStopping_SoResumingLaterStartsFresh()
        {
            follower.SetDestination(new Vector3(40f, 0f, 0f), MoveSpeed.Walk);
            RunBlocked(1f); // under the 1.4 s interval: no stuck event yet

            follower.Stop(); // stops trying to move: resets the stuck timer and reference position
            Run(1f);

            follower.SetDestination(new Vector3(40f, 0f, 0f), MoveSpeed.Walk);
            RunBlocked(1f); // another 1 s, still under the interval on its own

            Assert.AreEqual(0, follower.StuckCount, "the earlier 1 s should not have carried over through the stop");
        }
    }
}
