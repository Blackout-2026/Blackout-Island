// Owner: Core Developer (Tactical Guard AI).
using NUnit.Framework;

namespace Blackout.AI.TacticalGuard.Tests
{
    public sealed class ConsiderationFloorTests
    {
        [Test]
        public void DefaultFloor_IsZero_SoExistingCurvesAreUnchanged()
        {
            var consideration = new Consideration { responseCurve = ResponseCurve.Linear };

            Assert.AreEqual(0f, consideration.floor);
            Assert.That(consideration.Evaluate(0f), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Floor_RescalesTheCurveIntoFloorToOne()
        {
            var consideration = new Consideration(ResponseCurve.Linear, floor: 0.4f);

            Assert.That(consideration.Evaluate(0f), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(consideration.Evaluate(0.5f), Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(consideration.Evaluate(1f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Floor_AppliesToInverseCurveToo()
        {
            var consideration = new Consideration(ResponseCurve.Inverse, floor: 0.25f);

            Assert.That(consideration.Evaluate(1f), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(consideration.Evaluate(0f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Floor_KeepsAnActionAliveWhenOneInputIsZero()
        {
            var action = new UtilityAction(
                "Floored",
                1f,
                new Consideration { inputValue = 0f, responseCurve = ResponseCurve.Linear, floor = 0.25f },
                new Consideration { inputValue = 1f, responseCurve = ResponseCurve.Linear });

            Assert.That(action.Evaluate(), Is.EqualTo(0.5f).Within(0.0001f)); // sqrt(0.25 * 1)
        }

        [Test]
        public void Floor_OutOfRangeOrNonFiniteIsClamped_AndInvalidInputStillScoresZero()
        {
            Assert.That(new Consideration(ResponseCurve.Linear, floor: 2f).Evaluate(0f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(new Consideration(ResponseCurve.Linear, floor: -1f).Evaluate(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(new Consideration(ResponseCurve.Linear, floor: float.NaN).Evaluate(0.3f), Is.EqualTo(0.3f).Within(0.0001f));
            Assert.AreEqual(0f, new Consideration(ResponseCurve.Linear, floor: 0.5f).Evaluate(float.NaN));
        }
    }
}
