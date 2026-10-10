using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class PathTrackerTests
    {
        private MovementSettings settings;
        private PathTracker tracker;

        // An L-shaped path: 10 m along +X, then 10 m along +Z.
        private static readonly Vector3[] LPath =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(10f, 0f, 0f),
            new Vector3(10f, 0f, 10f),
        };

        [SetUp]
        public void SetUp()
        {
            settings = new MovementSettings(); // cornerReach 0.55, arrive 0.4, slow 1.4, min factor 0.15
            tracker = new PathTracker();
        }

        [Test]
        public void NewTracker_HasNoPath()
        {
            Assert.IsFalse(tracker.HasPath);
            Assert.IsFalse(tracker.Arrived);
            Assert.AreEqual(0f, tracker.RemainingDistance(Vector3.zero));
        }

        [Test]
        public void SetPath_SkipsTheStartCorner_AndEmptyPathMeansNoPath()
        {
            tracker.SetPath(LPath);
            Assert.IsTrue(tracker.HasPath);
            Assert.AreEqual(1, tracker.CornerIndex, "corner 0 is where the agent stood when the path was planned");
            Assert.AreEqual(LPath[1], tracker.Target);

            tracker.SetPath(new Vector3[0]);
            Assert.IsFalse(tracker.HasPath);

            tracker.SetPath(null);
            Assert.IsFalse(tracker.HasPath);
        }

        [Test]
        public void Advance_DoesNotWalkBackToTheStartCorner_WhenTheAgentWasPushedAway()
        {
            tracker.SetPath(LPath);

            tracker.Advance(new Vector3(5f, 0f, 3f), settings); // far from corner 0, and from corner 1

            Assert.AreEqual(1, tracker.CornerIndex);
        }

        [Test]
        public void Advance_StaysOnACornerUntilItIsClose()
        {
            tracker.SetPath(LPath);

            tracker.Advance(new Vector3(9.0f, 0f, 0f), settings); // 1 m away, outside the 0.55 m reach
            Assert.AreEqual(1, tracker.CornerIndex);

            tracker.Advance(new Vector3(9.6f, 0f, 0f), settings); // 0.4 m away, inside it
            Assert.AreEqual(2, tracker.CornerIndex);
        }

        [Test]
        public void Advance_CanSkipSeveralCornersInOneCall()
        {
            tracker.SetPath(new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.2f, 0f, 0f),
                new Vector3(0.4f, 0f, 0f),
                new Vector3(5f, 0f, 0f),
            });

            tracker.Advance(Vector3.zero, settings);

            Assert.AreEqual(3, tracker.CornerIndex);
        }

        [Test]
        public void Advance_NeverMovesPastTheLastCorner()
        {
            tracker.SetPath(LPath);
            tracker.Advance(new Vector3(10f, 0f, 0f), settings); // at corner 1: on to the last corner

            tracker.Advance(new Vector3(10f, 0f, 10f), settings); // at the last corner: arrives, index stays

            Assert.AreEqual(2, tracker.CornerIndex);
            Assert.IsTrue(tracker.OnLastCorner);
        }

        [Test]
        public void Arrived_NeedsTheLastCornerWithinTheArriveDistance()
        {
            tracker.SetPath(LPath);
            tracker.Advance(new Vector3(10f, 0f, 0f), settings); // corner 1 reached, heading for the last corner
            tracker.Advance(new Vector3(10f, 0f, 9.3f), settings); // 0.7 m away
            Assert.IsFalse(tracker.Arrived);

            tracker.Advance(new Vector3(10f, 0f, 9.7f), settings); // 0.3 m away
            Assert.IsTrue(tracker.Arrived);
        }

        [Test]
        public void Arrived_IsStickyUntilANewPathIsSet()
        {
            tracker.SetPath(LPath);
            tracker.Advance(new Vector3(10f, 0f, 0f), settings);
            tracker.Advance(new Vector3(10f, 0f, 10f), settings);
            Assert.IsTrue(tracker.Arrived);

            tracker.Advance(new Vector3(0f, 0f, 0f), settings); // pushed away afterwards
            Assert.IsTrue(tracker.Arrived);

            tracker.SetPath(LPath);
            Assert.IsFalse(tracker.Arrived);
        }

        [Test]
        public void SinglePointPath_ArrivesWhenCloseEnough()
        {
            tracker.SetPath(new[] { new Vector3(3f, 0f, 0f) });

            tracker.Advance(new Vector3(2.5f, 0f, 0f), settings);
            Assert.IsFalse(tracker.Arrived);

            tracker.Advance(new Vector3(2.8f, 0f, 0f), settings);
            Assert.IsTrue(tracker.Arrived);
        }

        [Test]
        public void SpeedFactor_IsFullSpeedBeforeTheLastLeg()
        {
            tracker.SetPath(LPath);
            tracker.Advance(new Vector3(0.1f, 0f, 0f), settings);

            Assert.AreEqual(1f, tracker.SpeedFactor(new Vector3(0.1f, 0f, 0f), settings), "corner 1 is not the last corner");
        }

        [Test]
        public void SpeedFactor_EasesDownOnTheLastLeg_ButNeverBelowTheMinimum()
        {
            tracker.SetPath(LPath);
            tracker.Advance(new Vector3(10f, 0f, 0f), settings); // corner 1 reached: now on the last leg

            Assert.AreEqual(1f, tracker.SpeedFactor(new Vector3(10f, 0f, 5f), settings), 0.0001f, "5 m out: full speed");
            Assert.AreEqual(0.7f / 1.4f + 0.15f, tracker.SpeedFactor(new Vector3(10f, 0f, 9.3f), settings), 0.0001f);
            Assert.AreEqual(0.15f, tracker.SpeedFactor(new Vector3(10f, 0f, 10f), settings), 0.0001f, "at the target: the creeping minimum");
        }

        [Test]
        public void RemainingDistance_AddsTheCurrentCornerAndTheLegsAfterIt()
        {
            tracker.SetPath(LPath);
            tracker.Advance(new Vector3(4f, 0f, 0f), settings); // still heading for corner 1 (10, 0, 0)

            Assert.AreEqual(6f + 10f, tracker.RemainingDistance(new Vector3(4f, 0f, 0f)), 0.0001f);
        }

        [Test]
        public void Distances_IgnoreHeight()
        {
            tracker.SetPath(new[] { new Vector3(3f, 0f, 0f) });

            tracker.Advance(new Vector3(3f, 50f, 0f), settings);

            Assert.IsTrue(tracker.Arrived);
        }

        [Test]
        public void Clear_RemovesThePath()
        {
            tracker.SetPath(LPath);

            tracker.Clear();

            Assert.IsFalse(tracker.HasPath);
            Assert.IsFalse(tracker.Arrived);
        }
    }
}
