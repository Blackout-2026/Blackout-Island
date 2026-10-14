using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class PivotRulesTests
    {
        private static Bounds Box(Vector3 min, Vector3 max)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        [Test]
        public void BaseCentre_NeedsTheBottomOnTheFloorAndTheFootprintCentred()
        {
            Assert.IsNull(PivotRules.CheckPivot(Box(new Vector3(-1, 0, -0.5f), new Vector3(1, 2, 0.5f)), PropPivot.BaseCentre));
            StringAssert.Contains("bottom", PivotRules.CheckPivot(Box(new Vector3(-1, 0.4f, -0.5f), new Vector3(1, 2, 0.5f)), PropPivot.BaseCentre));
            StringAssert.Contains("centre", PivotRules.CheckPivot(Box(new Vector3(0, 0, -0.5f), new Vector3(2, 2, 0.5f)), PropPivot.BaseCentre));
        }

        [Test]
        public void BaseCentre_AllowsAnyHeightAboveTheFloor()
        {
            // The centre rule is on the footprint only; a tall prop's bounds centre is above y 0 and that is correct.
            Assert.IsNull(PivotRules.CheckPivot(Box(new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 3, 0.5f)), PropPivot.BaseCentre));
        }

        [Test]
        public void Centre_NeedsTheBoundsCentredOnTheOriginInEveryAxis()
        {
            Assert.IsNull(PivotRules.CheckPivot(Box(Vector3.one * -0.75f, Vector3.one * 0.75f), PropPivot.Centre));
            Assert.IsNotNull(PivotRules.CheckPivot(Box(new Vector3(-0.75f, 0f, -0.75f), new Vector3(0.75f, 1.5f, 0.75f)), PropPivot.Centre));
        }

        [Test]
        public void WallFace_NeedsTheBackFaceAtZeroAndTheFaceCentred()
        {
            Assert.IsNull(PivotRules.CheckPivot(Box(new Vector3(-0.5f, -0.3f, 0f), new Vector3(0.5f, 0.3f, 0.08f)), PropPivot.WallFace));
            StringAssert.Contains("back face", PivotRules.CheckPivot(Box(new Vector3(-0.5f, -0.3f, -0.2f), new Vector3(0.5f, 0.3f, 0.08f)), PropPivot.WallFace));
            Assert.IsNotNull(PivotRules.CheckPivot(Box(new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0.6f, 0.08f)), PropPivot.WallFace));
        }

        [Test]
        public void Grip_IsNotChecked()
        {
            Assert.IsNull(PivotRules.CheckPivot(Box(Vector3.one * 5f, Vector3.one * 9f), PropPivot.Grip));
        }

        [Test]
        public void CheckSize_AcceptsFivePercentOfAMetreOnEveryAxis()
        {
            Bounds bounds = Box(Vector3.zero, new Vector3(1f, 2f, 3f));

            Assert.IsNull(PivotRules.CheckSize(bounds, new Vector3(1.04f, 1.96f, 3.0f)));
            StringAssert.Contains("expected", PivotRules.CheckSize(bounds, new Vector3(1f, 2.2f, 3f)));
        }
    }
}
