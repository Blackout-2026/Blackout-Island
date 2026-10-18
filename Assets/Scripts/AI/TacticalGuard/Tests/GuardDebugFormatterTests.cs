// Owner: Core Developer (Tactical Guard AI).
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.TacticalGuard.Tests
{
    public sealed class GuardDebugFormatterTests
    {
        private TacticalGuardConfig config;
        private GuardActionScorer scorer;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<TacticalGuardConfig>();
            scorer = new GuardActionScorer(config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        private static GuardSituation HurtAndVisible()
        {
            return new GuardSituation
            {
                canSeePlayer = true,
                distanceToPlayer = 15f,
                knowledge = 1f,
                threat = 1f,
                healthFraction = 0.3f,
                distanceFromPost = 4f,
                coverQuality = 0.8298f
            };
        }

        private string Text(in GuardSituation situation, string coverId = null)
        {
            scorer.Choose(situation);
            var builder = new StringBuilder();
            GuardDebugFormatter.Append(builder, scorer, situation, coverId);
            return builder.ToString();
        }

        [Test]
        public void Text_IsThreeLines_FirstNamesTheChosenAction()
        {
            string[] lines = Text(HurtAndVisible()).Split('\n');

            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual("TacticalGuard MoveToCover", lines[0]);
            StringAssert.StartsWith("scores:", lines[1]);
            StringAssert.StartsWith("inputs:", lines[2]);
        }

        [Test]
        public void Text_ListsEveryActionScoreAndMarksTheWinner()
        {
            string scores = Text(HurtAndVisible()).Split('\n')[1];

            Assert.AreEqual("scores: Attack 0.71 | MoveToCover 0.91* | Reposition 0.08 | HoldPost 0.37", scores);
        }

        [Test]
        public void Text_ShowsTheInputsBehindTheScores()
        {
            string inputs = Text(HurtAndVisible()).Split('\n')[2];

            Assert.AreEqual("inputs: see=yes dist=15.0 know=1.00 threat=1.00 hp=0.30 post=4.0 cover=0.83", inputs);
        }

        [Test]
        public void Text_NamesTheHeldCoverWhenThereIsOne()
        {
            string first = Text(HurtAndVisible(), "CoverPoint_03").Split('\n')[0];

            Assert.AreEqual("TacticalGuard MoveToCover (cover CoverPoint_03)", first);
        }

        [Test]
        public void Text_UsesAPointDecimalWhateverTheCulture()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

                StringAssert.Contains("dist=15.0", Text(HurtAndVisible()));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [TestCase(GuardActionType.Attack, "Attack")]
        [TestCase(GuardActionType.MoveToCover, "MoveToCover")]
        [TestCase(GuardActionType.Reposition, "Reposition")]
        [TestCase(GuardActionType.HoldPost, "HoldPost")]
        public void ActionName_MatchesTheEnum(GuardActionType action, string expected)
        {
            Assert.AreEqual(expected, GuardDebugFormatter.ActionName(action));
            Assert.AreEqual(action.ToString(), GuardDebugFormatter.ActionName(action));
        }

        // The worked example in docs/viva/tactical-guard.md. If a default number changes, this fails and the doc must follow.
        [Test]
        public void WorkedExample_MatchesTheHandCalculationInTheVivaNotes()
        {
            GuardSituation s = HurtAndVisible();

            GuardActionType winner = scorer.Choose(s);

            Assert.AreEqual(GuardActionType.MoveToCover, winner);
            Assert.That(scorer.GetScore(GuardActionType.Attack), Is.EqualTo(0.7134f).Within(0.001f));
            Assert.That(scorer.GetScore(GuardActionType.MoveToCover), Is.EqualTo(0.9109f).Within(0.001f));
            Assert.That(scorer.GetScore(GuardActionType.Reposition), Is.EqualTo(0.0822f).Within(0.001f));
            Assert.That(scorer.GetScore(GuardActionType.HoldPost), Is.EqualTo(0.3695f).Within(0.001f));
        }
    }
}
