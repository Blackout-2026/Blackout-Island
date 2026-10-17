// Owner: Core Developer (Tactical Guard AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.TacticalGuard.Tests
{
    public sealed class CoverTests
    {
        private sealed class FakeSensor : IGuardSensor
        {
            /// <summary>Returns true when the two points can see each other. Default: everything is blocked (all cover hides).</summary>
            public Func<Vector3, Vector3, bool> rule = (from, to) => false;
            public bool HasLineOfSight(Vector3 from, Vector3 to) => rule(from, to);
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private TacticalGuardConfig config;
        private FakeSensor sensor;
        private CoverClaims claims;
        private CoverSelector selector;
        private readonly object guardA = new object();
        private readonly object guardB = new object();

        // Guard and post at the origin, player 20 m away on +z.
        private static readonly Vector3 GuardPos = Vector3.zero;
        private static readonly Vector3 PostPos = Vector3.zero;
        private static readonly Vector3 PlayerPos = new Vector3(0f, 0f, 20f);

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<TacticalGuardConfig>();
            sensor = new FakeSensor();
            claims = new CoverClaims();
            selector = new CoverSelector(config, sensor, claims);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
            created.Clear();
            UnityEngine.Object.DestroyImmediate(config);
        }

        private Marker Cover(float x, float z)
        {
            var go = new GameObject("CoverPoint_" + x + "_" + z);
            go.transform.position = new Vector3(x, 0f, z);
            created.Add(go);
            return go.AddComponent<Marker>();
        }

        private Marker Pick(IReadOnlyList<Marker> covers, object claimant, out float quality)
        {
            return selector.Pick(covers, GuardPos, PlayerPos, PostPos, claimant, out quality);
        }

        // ---- claims ----

        [Test]
        public void Claims_AUnclaimedCoverCanBeClaimed()
        {
            Marker cover = Cover(3f, 0f);

            Assert.IsTrue(claims.TryClaim(cover, guardA));
            Assert.AreSame(cover, claims.GetClaim(guardA));
            Assert.IsFalse(claims.IsClaimedByOther(cover, guardA));
            Assert.IsTrue(claims.IsClaimedByOther(cover, guardB));
        }

        [Test]
        public void Claims_ASecondClaimantIsRefusedAndNothingChanges()
        {
            Marker cover = Cover(3f, 0f);
            claims.TryClaim(cover, guardA);

            Assert.IsFalse(claims.TryClaim(cover, guardB));
            Assert.AreSame(cover, claims.GetClaim(guardA));
            Assert.IsNull(claims.GetClaim(guardB));
        }

        [Test]
        public void Claims_ClaimingAgainByTheSameClaimantIsFine()
        {
            Marker cover = Cover(3f, 0f);
            claims.TryClaim(cover, guardA);

            Assert.IsTrue(claims.TryClaim(cover, guardA));
            Assert.AreEqual(1, claims.Count);
        }

        [Test]
        public void Claims_AClaimantHoldsOnlyOneAndNewClaimFreesTheOld()
        {
            Marker first = Cover(3f, 0f);
            Marker second = Cover(-3f, 0f);
            claims.TryClaim(first, guardA);

            claims.TryClaim(second, guardA);

            Assert.AreSame(second, claims.GetClaim(guardA));
            Assert.IsFalse(claims.IsClaimedByOther(first, guardB), "the first cover is free again");
            Assert.AreEqual(1, claims.Count);
        }

        [Test]
        public void Claims_ReleaseFreesTheCoverForOthers()
        {
            Marker cover = Cover(3f, 0f);
            claims.TryClaim(cover, guardA);

            claims.Release(guardA);

            Assert.IsTrue(claims.TryClaim(cover, guardB));
            claims.Release(guardA); // releasing when holding nothing is harmless
            Assert.AreSame(cover, claims.GetClaim(guardB));
        }

        // ---- selection ----

        [Test]
        public void Pick_ChoosesTheNearestHidingCover()
        {
            Marker near = Cover(3f, 0f);
            Marker far = Cover(10f, 0f);

            Marker picked = Pick(new[] { far, near }, guardA, out float quality);

            Assert.AreSame(near, picked);
            Assert.Greater(quality, 0f);
        }

        [Test]
        public void Pick_SkipsCoverClaimedByAnotherGuard()
        {
            Marker near = Cover(3f, 0f);
            Marker far = Cover(10f, 0f);
            claims.TryClaim(near, guardB);

            Assert.AreSame(far, Pick(new[] { near, far }, guardA, out _));
        }

        [Test]
        public void Pick_KeepsTheCoverTheGuardAlreadyHolds()
        {
            Marker near = Cover(3f, 0f);
            Marker far = Cover(10f, 0f);
            claims.TryClaim(near, guardA);

            Assert.AreSame(near, Pick(new[] { near, far }, guardA, out _));
        }

        [Test]
        public void Pick_SkipsCoverThePlayerCanSee()
        {
            Marker near = Cover(3f, 0f);
            Marker far = Cover(10f, 0f);
            sensor.rule = (from, to) => Mathf.Abs(to.x - 3f) < 0.01f; // only the near cover is exposed

            Assert.AreSame(far, Pick(new[] { near, far }, guardA, out _));
        }

        [Test]
        public void Pick_IgnoresCoverBeyondTheSearchRadius()
        {
            Marker tooFar = Cover(config.coverSearchRadius + 1f, 0f);

            Assert.IsNull(Pick(new[] { tooFar }, guardA, out float quality));
            Assert.AreEqual(0f, quality);
        }

        [Test]
        public void Pick_IgnoresCoverTooFarFromThePost()
        {
            Marker cover = Cover(3f, 0f);
            Vector3 farPost = new Vector3(0f, 0f, -(config.coverPostRadius + 10f));

            Assert.IsNull(selector.Pick(new[] { cover }, GuardPos, PlayerPos, farPost, guardA, out _));
        }

        [Test]
        public void Pick_PrefersCoverAwayFromThePlayerOverCoverRightNextToThem()
        {
            Vector3 player = new Vector3(3f, 0f, 1f);
            Marker nextToPlayer = Cover(3f, 0f);
            Marker awayFromPlayer = Cover(-3f, 0f);

            Marker picked = selector.Pick(new[] { nextToPlayer, awayFromPlayer }, GuardPos, player, PostPos, guardA, out _);

            Assert.AreSame(awayFromPlayer, picked);
        }

        [Test]
        public void Pick_TwoGuardsDoNotChooseTheSameCover()
        {
            Marker near = Cover(3f, 0f);
            Marker far = Cover(10f, 0f);
            Marker[] covers = { near, far };

            Marker forA = Pick(covers, guardA, out _);
            claims.TryClaim(forA, guardA);
            Marker forB = Pick(covers, guardB, out _);
            claims.TryClaim(forB, guardB);

            Assert.AreSame(near, forA);
            Assert.AreSame(far, forB);
        }

        [Test]
        public void Pick_NoMarkersMeansNoCover()
        {
            Assert.IsNull(Pick(new Marker[0], guardA, out float quality));
            Assert.AreEqual(0f, quality);
        }

        [Test]
        public void Evaluate_IsZeroOnceThePlayerCanSeeTheCover()
        {
            Marker cover = Cover(3f, 0f);
            Assert.Greater(selector.Evaluate(cover, GuardPos, PlayerPos, PostPos), 0f);

            sensor.rule = (from, to) => true;

            Assert.AreEqual(0f, selector.Evaluate(cover, GuardPos, PlayerPos, PostPos));
            Assert.AreEqual(0f, selector.Evaluate(null, GuardPos, PlayerPos, PostPos));
        }

        [Test]
        public void Pick_ChecksLineOfSightFromThePlayersEyeToACrouchedHead()
        {
            Marker cover = Cover(3f, 0f);
            Vector3 seenFrom = default;
            Vector3 seenTo = default;
            sensor.rule = (from, to) => { seenFrom = from; seenTo = to; return false; };

            Pick(new[] { cover }, guardA, out _);

            Assert.That(seenFrom.y, Is.EqualTo(config.playerEyeHeight).Within(0.0001f));
            Assert.That(seenTo.y, Is.EqualTo(config.coverHeadHeight).Within(0.0001f));
        }
    }
}
