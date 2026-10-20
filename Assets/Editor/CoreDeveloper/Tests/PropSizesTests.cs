using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class PropSizesTests
    {
        [TestCase(0.49f, PropSize.Small)]
        [TestCase(0.5f, PropSize.Medium)]
        [TestCase(1.5f, PropSize.Medium)]
        [TestCase(1.51f, PropSize.Large)]
        public void Classify_UsesTheLongestSide(float longest, PropSize expected)
        {
            Assert.AreEqual(expected, PropSizes.Classify(new Vector3(0.1f, longest, 0.2f)));
            Assert.AreEqual(expected, PropSizes.Classify(new Vector3(longest, 0.1f, 0.2f)));
            Assert.AreEqual(expected, PropSizes.Classify(new Vector3(0.1f, 0.2f, longest)));
        }

        [Test]
        public void BudgetOf_MatchesTheArtPipelineTable()
        {
            Assert.AreEqual(1200, PropSizes.BudgetOf(PropSize.Small).TargetTriangles);
            Assert.AreEqual(1800, PropSizes.BudgetOf(PropSize.Small).MaxTriangles);
            Assert.AreEqual(2000, PropSizes.BudgetOf(PropSize.Medium).TargetTriangles);
            Assert.AreEqual(3000, PropSizes.BudgetOf(PropSize.Medium).MaxTriangles);
            Assert.AreEqual(3000, PropSizes.BudgetOf(PropSize.Large).TargetTriangles);
            Assert.AreEqual(4500, PropSizes.BudgetOf(PropSize.Large).MaxTriangles);
            Assert.AreEqual(4, PropSizes.BudgetOf(PropSize.Large).MaxMaterials);
        }
    }
}
