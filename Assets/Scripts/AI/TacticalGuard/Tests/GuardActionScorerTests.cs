// Owner: Core Developer (Tactical Guard AI).
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.TacticalGuard.Tests
{
    /// <summary>Each action must win in the situation it exists for, using the default config and fake inputs.</summary>
    public sealed class GuardActionScorerTests
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

        // Calm, healthy, at the post, nothing known, no cover. Each test changes what its situation is about.
        private static GuardSituation Calm()
        {
            return new GuardSituation { distanceToPlayer = 20f, healthFraction = 1f };
        }

        [Test]
        public void Attack_WinsWhenPlayerIsVisibleCloseAndGuardIsHealthy()
        {
            GuardSituation s = Calm();
            s.canSeePlayer = true;
            s.distanceToPlayer = 12f;
            s.knowledge = 1f;
            s.threat = 0.4f;
            s.coverQuality = 0.6f;

            Assert.AreEqual(GuardActionType.Attack, scorer.Choose(s));
        }

        [Test]
        public void Attack_StillWinsAtFullHealthUnderHeavyFire()
        {
            GuardSituation s = Calm();
            s.canSeePlayer = true;
            s.distanceToPlayer = 12f;
            s.knowledge = 1f;
            s.threat = 1f;
            s.coverQuality = 0.6f;

            Assert.AreEqual(GuardActionType.Attack, scorer.Choose(s));
        }

        [Test]
        public void Attack_ScoresZeroWithoutLineOfSight()
        {
            GuardSituation s = Calm();
            s.knowledge = 1f;
            s.distanceToPlayer = 5f;

            scorer.Choose(s);

            Assert.AreEqual(0f, scorer.GetScore(GuardActionType.Attack));
        }

        [Test]
        public void MoveToCover_WinsWhenHurtAndLowOnHealthWithCoverAvailable()
        {
            GuardSituation s = Calm();
            s.canSeePlayer = true;
            s.distanceToPlayer = 15f;
            s.knowledge = 1f;
            s.threat = GuardMath.Threat(true, 1f, false, false, config);
            s.healthFraction = 0.3f;
            s.coverQuality = 0.6f;

            Assert.AreEqual(GuardActionType.MoveToCover, scorer.Choose(s));
        }

        [Test]
        public void MoveToCover_WinsWhenShotFromOutOfSightAndCoverExists()
        {
            GuardSituation s = Calm();
            s.knowledge = 0.9f;
            s.threat = GuardMath.Threat(false, 1f, false, false, config); // 0.7: hit just now, shooter not visible
            s.coverQuality = 0.6f;

            Assert.AreEqual(GuardActionType.MoveToCover, scorer.Choose(s));
        }

        [Test]
        public void MoveToCover_ScoresZeroWithNoCover()
        {
            GuardSituation s = Calm();
            s.canSeePlayer = true;
            s.threat = 1f;
            s.healthFraction = 0.2f;

            scorer.Choose(s);

            Assert.AreEqual(0f, scorer.GetScore(GuardActionType.MoveToCover));
        }

        [Test]
        public void Reposition_WinsRightAfterLosingSightOfThePlayer()
        {
            GuardSituation s = Calm();
            s.knowledge = 0.8f;

            Assert.AreEqual(GuardActionType.Reposition, scorer.Choose(s));
        }

        [Test]
        public void Reposition_WinsWhenPlayerIsVisibleButOutOfEffectiveRange()
        {
            GuardSituation s = Calm();
            s.canSeePlayer = true;
            s.distanceToPlayer = 33f;
            s.knowledge = 1f;
            s.threat = 0.4f;
            s.coverQuality = 0.6f;

            Assert.AreEqual(GuardActionType.Reposition, scorer.Choose(s));
        }

        [Test]
        public void Reposition_BeatsCoverWhenShotButThereIsNoCover()
        {
            GuardSituation s = Calm();
            s.knowledge = 0.9f;
            s.threat = 0.7f;
            s.coverQuality = 0f;

            Assert.AreEqual(GuardActionType.Reposition, scorer.Choose(s));
        }

        [Test]
        public void HoldPost_WinsWhenNothingIsKnown()
        {
            Assert.AreEqual(GuardActionType.HoldPost, scorer.Choose(Calm()));
        }

        [Test]
        public void HoldPost_WinsWhenFarFromPostAndKnowledgeHasFaded()
        {
            GuardSituation s = Calm();
            s.distanceFromPost = 12f;
            s.knowledge = 0.1f;

            Assert.AreEqual(GuardActionType.HoldPost, scorer.Choose(s));
        }

        [Test]
        public void HoldPost_ScoreRisesWithDistanceFromThePost()
        {
            GuardSituation atPost = Calm();
            GuardSituation away = Calm();
            away.distanceFromPost = 12f;

            scorer.Choose(atPost);
            float atPostScore = scorer.GetScore(GuardActionType.HoldPost);
            scorer.Choose(away);

            Assert.Greater(scorer.GetScore(GuardActionType.HoldPost), atPostScore);
        }

        [Test]
        public void Hysteresis_KeepsTheCurrentActionWhenAChallengerIsOnlySlightlyBetter()
        {
            GuardSituation near = Calm();
            near.canSeePlayer = true;
            near.distanceToPlayer = 20f;
            near.knowledge = 1f;
            near.threat = 0.4f;
            Assert.AreEqual(GuardActionType.Attack, scorer.Choose(near));

            GuardSituation edge = near;
            edge.distanceToPlayer = 32f; // Reposition 0.56 vs Attack 0.49: ahead by less than the 0.1 bonus
            Assert.AreEqual(GuardActionType.Attack, scorer.Choose(edge));
            Assert.Greater(scorer.GetScore(GuardActionType.Reposition), scorer.GetScore(GuardActionType.Attack));

            GuardSituation far = near;
            far.distanceToPlayer = 36f;
            Assert.AreEqual(GuardActionType.Reposition, scorer.Choose(far));
        }

        [Test]
        public void Reset_RemovesTheHysteresisBonus()
        {
            GuardSituation near = Calm();
            near.canSeePlayer = true;
            near.knowledge = 1f;
            near.threat = 0.4f;
            near.distanceToPlayer = 20f;
            scorer.Choose(near);

            GuardSituation edge = near;
            edge.distanceToPlayer = 32f;
            scorer.Reset();

            Assert.AreEqual(GuardActionType.Reposition, scorer.Choose(edge));
        }

        [Test]
        public void Weights_ComeFromTheConfigAndApplyImmediately()
        {
            GuardSituation s = Calm(); // HoldPost wins at the post
            Assert.AreEqual(GuardActionType.HoldPost, scorer.Choose(s));

            config.holdPostWeight = 0f;
            s.knowledge = 0.5f;
            Assert.AreEqual(GuardActionType.Reposition, scorer.Choose(s));
        }

        [Test]
        public void Curves_ComeFromTheConfigAndApplyImmediately()
        {
            GuardSituation s = Calm();
            s.canSeePlayer = true;
            s.distanceToPlayer = 12f;
            s.knowledge = 1f;
            scorer.Choose(s);
            float before = scorer.GetScore(GuardActionType.Attack);

            config.attackHealth = new ConsiderationSettings(ResponseCurve.Linear);
            s.healthFraction = 0.25f;
            scorer.Choose(s);

            Assert.Less(scorer.GetScore(GuardActionType.Attack), before);
        }

        [Test]
        public void Scores_StayInsideZeroToTheActionWeight()
        {
            GuardSituation s = Calm();
            s.canSeePlayer = true;
            s.distanceToPlayer = 1f;
            s.knowledge = 1f;
            s.threat = 1f;
            s.coverQuality = 1f;

            scorer.Choose(s);

            Assert.LessOrEqual(scorer.GetScore(GuardActionType.Attack), config.attackWeight + 0.0001f);
            Assert.LessOrEqual(scorer.GetScore(GuardActionType.MoveToCover), config.moveToCoverWeight + 0.0001f);
            Assert.LessOrEqual(scorer.GetScore(GuardActionType.Reposition), config.repositionWeight + 0.0001f);
            Assert.LessOrEqual(scorer.GetScore(GuardActionType.HoldPost), config.holdPostWeight + 0.0001f);
        }
    }
}
