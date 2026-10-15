// Owner: Core Developer (Tactical Guard AI).
using NUnit.Framework;

namespace Blackout.AI.TacticalGuard.Tests
{
    public sealed class UtilityFrameworkTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.25f, 0.25f)]
        [TestCase(1f, 1f)]
        public void LinearCurve_ReturnsClampedInput(float input, float expected)
        {
            var consideration = new Consideration { responseCurve = ResponseCurve.Linear };

            Assert.That(consideration.Evaluate(input), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void EveryCurve_ClampsOutOfRangeAndNonFiniteInputs()
        {
            foreach (ResponseCurve curve in System.Enum.GetValues(typeof(ResponseCurve)))
            {
                var consideration = new Consideration { responseCurve = curve };
                float lowerBoundUtility = curve == ResponseCurve.Inverse ? 1f : 0f;
                float upperBoundUtility = curve == ResponseCurve.Inverse ? 0f : 1f;

                Assert.AreEqual(lowerBoundUtility, consideration.Evaluate(-2f), curve.ToString() + " lower bound");
                Assert.AreEqual(upperBoundUtility, consideration.Evaluate(2f), curve.ToString() + " upper bound");
                Assert.AreEqual(0f, consideration.Evaluate(float.NaN), curve.ToString() + " NaN");
                Assert.AreEqual(0f, consideration.Evaluate(float.PositiveInfinity), curve.ToString() + " infinity");
            }
        }

        [Test]
        public void QuadraticCurve_SquaresClampedInput()
        {
            var consideration = new Consideration { responseCurve = ResponseCurve.Quadratic };

            Assert.That(consideration.Evaluate(0.5f), Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void InverseCurve_ReversesClampedInput()
        {
            var consideration = new Consideration { responseCurve = ResponseCurve.Inverse };

            Assert.That(consideration.Evaluate(0.2f), Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void LogisticCurve_IsNormalizedAtEndpointsAndMidpoint()
        {
            var consideration = new Consideration(ResponseCurve.Logistic, 10f, 0.5f);

            Assert.That(consideration.Evaluate(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(consideration.Evaluate(0.5f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(consideration.Evaluate(1f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void LogisticCurve_ZeroSteepnessFallsBackToLinear()
        {
            var consideration = new Consideration(ResponseCurve.Logistic, 0f, 0.5f);

            Assert.That(consideration.Evaluate(0.3f), Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void Consideration_EvaluateUsesConfiguredInputValue()
        {
            var consideration = new Consideration
            {
                inputValue = 0.4f,
                responseCurve = ResponseCurve.Quadratic
            };

            Assert.That(consideration.Evaluate(), Is.EqualTo(0.16f).Within(0.0001f));
        }

        [Test]
        public void UtilityAction_UsesGeometricMeanCompensationAndWeight()
        {
            var action = new UtilityAction(
                "Weighted",
                2f,
                Linear(0.25f),
                Linear(0.81f));

            Assert.That(action.Evaluate(), Is.EqualTo(0.9f).Within(0.0001f));
        }

        [Test]
        public void UtilityAction_WithNoConsiderationsScoresZero()
        {
            var action = new UtilityAction { actionName = "Empty" };

            Assert.AreEqual(0f, action.Evaluate());
        }

        [Test]
        public void Selector_ChoosesHighestScoringAction()
        {
            UtilityAction lower = Action("Lower", 0.4f);
            UtilityAction higher = Action("Higher", 0.8f);

            Assert.AreSame(higher, UtilityActionSelector.Select(new[] { lower, higher }, null));
        }

        [Test]
        public void Selector_HysteresisKeepsCurrentActionWhenScoresAreClose()
        {
            UtilityAction current = Action("Current", 0.5f);
            UtilityAction challenger = Action("Challenger", 0.53f);

            Assert.AreSame(current, UtilityActionSelector.Select(new[] { current, challenger }, current));
        }

        [Test]
        public void Selector_SwitchesWhenChallengerExceedsHysteresisBonus()
        {
            UtilityAction current = Action("Current", 0.5f);
            UtilityAction challenger = Action("Challenger", 0.56f);

            Assert.AreSame(challenger, UtilityActionSelector.Select(new[] { current, challenger }, current));
        }

        [Test]
        public void Selector_UsesFirstActionForExactTies()
        {
            UtilityAction first = Action("First", 0.6f);
            UtilityAction second = Action("Second", 0.6f);

            Assert.AreSame(first, UtilityActionSelector.Select(new[] { first, second }, null, 0f));
        }

        [Test]
        public void Selector_ReturnsNullForNullOrEmptyActionLists()
        {
            Assert.IsNull(UtilityActionSelector.Select(null, null));
            Assert.IsNull(UtilityActionSelector.Select(new UtilityAction[0], null));
        }

        private static Consideration Linear(float input)
        {
            return new Consideration { inputValue = input, responseCurve = ResponseCurve.Linear };
        }

        private static UtilityAction Action(string name, float score)
        {
            return new UtilityAction(name, 1f, Linear(score));
        }
    }
}
