// Owner: Systems Engineer (Hunter AI).
using NUnit.Framework;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>The attenuation formula and the decay (docs/reference/systems-engineer.md section 14).</summary>
    public class HearingMathTests
    {
        private const float WallFactor = 0.35f;
        private const float Hearing = 1.7f;
        private const float Threshold = 0.04f;
        private const float Tolerance = 1e-5f;

        [TestCase(0f, 1f)]
        [TestCase(6.5f, 0.5f)]
        [TestCase(13f, 0f)]
        [TestCase(20f, 0f)]
        public void OpenSpace_FallsLinearlyToZeroAtTheRadius(float distance, float expected)
        {
            Assert.AreEqual(expected, HearingMath.OpenSpace(13f, distance), Tolerance);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void OpenSpace_NoRadius_IsSilent(float radius)
        {
            Assert.AreEqual(0f, HearingMath.OpenSpace(radius, 1f));
        }

        /// <summary>The playbook's test: the same sprint footstep 6.5 m away in open space, behind one wall and two.</summary>
        [Test]
        public void OpenSpace_VersusOneWall_VersusTwoWalls()
        {
            float open = HearingMath.Perceived(13f, 6.5f, 0, WallFactor, Hearing);
            float oneWall = HearingMath.Perceived(13f, 6.5f, 1, WallFactor, Hearing);
            float twoWalls = HearingMath.Perceived(13f, 6.5f, 2, WallFactor, Hearing);

            Assert.AreEqual(0.85f, open, Tolerance);
            Assert.AreEqual(0.2975f, oneWall, Tolerance);
            Assert.AreEqual(0.104125f, twoWalls, Tolerance);
            Assert.AreEqual(WallFactor, oneWall / open, Tolerance, "each wall keeps 35 %");
            Assert.AreEqual(WallFactor, twoWalls / oneWall, Tolerance);
            Assert.IsTrue(HearingMath.IsAudible(twoWalls, Threshold), "still heard through two walls");
        }

        [Test]
        public void ThreeWalls_MakeThatFootstepInaudible()
        {
            float threeWalls = HearingMath.Perceived(13f, 6.5f, 3, WallFactor, Hearing);

            Assert.AreEqual(0.0364f, threeWalls, 1e-4f);
            Assert.IsFalse(HearingMath.IsAudible(threeWalls, Threshold));
        }

        [Test]
        public void ThroughWalls_IsWallFactorToThePowerOfWalls()
        {
            Assert.AreEqual(1f, HearingMath.ThroughWalls(0, WallFactor));
            Assert.AreEqual(0.35f, HearingMath.ThroughWalls(1, WallFactor), Tolerance);
            Assert.AreEqual(0.1225f, HearingMath.ThroughWalls(2, WallFactor), Tolerance);
            Assert.AreEqual(0.042875f, HearingMath.ThroughWalls(3, WallFactor), Tolerance);
        }

        [Test]
        public void IsAudible_OnlyAboveTheThreshold()
        {
            Assert.IsFalse(HearingMath.IsAudible(0.04f, Threshold), "the prototype needs loud > 0.04");
            Assert.IsTrue(HearingMath.IsAudible(0.0401f, Threshold));
        }

        /// <summary>The "heard up to" column of the table in the reference, section 14.</summary>
        [TestCase(0, 0.976f)]
        [TestCase(1, 0.933f)]
        [TestCase(2, 0.808f)]
        [TestCase(3, 0.451f)]
        public void MaxHeardDistance_MatchesTheReferenceTable(int walls, float fractionOfRadius)
        {
            float distance = HearingMath.MaxHeardDistance(100f, walls, WallFactor, Hearing, Threshold);

            Assert.AreEqual(fractionOfRadius * 100f, distance, 0.1f);
            Assert.AreEqual(Threshold, HearingMath.Perceived(100f, distance, walls, WallFactor, Hearing), 1e-4f);
        }

        [Test]
        public void MaxHeardDistance_TooManyWalls_IsZero()
        {
            Assert.AreEqual(0f, HearingMath.MaxHeardDistance(60f, 8, WallFactor, Hearing, Threshold));
        }

        [TestCase(0f, 1f)]
        [TestCase(2f, 0.75f)]
        [TestCase(4f, 0.5f)]
        [TestCase(8f, 0f)]
        [TestCase(12f, 0f)]
        [TestCase(-1f, 1f)]
        public void Faded_FallsLinearlyToZeroOverTheMemory(float age, float expected)
        {
            Assert.AreEqual(expected, HearingMath.Faded(1f, age, 8f), Tolerance);
        }
    }
}
