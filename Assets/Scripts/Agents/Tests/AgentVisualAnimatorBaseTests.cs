using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class AgentVisualAnimatorBaseTests
    {
        /// <summary>A minimal concrete animator so the abstract base can be exercised directly.</summary>
        private sealed class FakeVisualAnimator : AgentVisualAnimatorBase
        {
            public int applyCalls;
            public int hitCalls;
            public int deathBeginCalls;
            public int liveTickCalls;
            public int deathTickCalls;
            public float lastDeathTick;

            protected override void OnApply(in AgentIntent intent) { applyCalls++; }
            protected override void OnHit(in DamageInfo info) { hitCalls++; }
            protected override void OnDeathBegin() { deathBeginCalls++; }
            protected override void OnLiveTick(float dt) { liveTickCalls++; }
            protected override void OnDeathTick(float secondsSinceDeath) { deathTickCalls++; lastDeathTick = secondsSinceDeath; }
        }

        private GameObject gameObject;
        private FakeVisualAnimator animator;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("AnimatorTest");
            animator = gameObject.AddComponent<FakeVisualAnimator>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        private static DamageInfo Hit(float amount)
        {
            return new DamageInfo(amount, Vector3.zero, Vector3.forward, null, DamageType.Bullet);
        }

        [Test]
        public void FadeScale_StaysFull_UntilTheDelayHasPassed()
        {
            Assert.AreEqual(1f, AgentVisualAnimatorBase.FadeScale(0f));
            Assert.AreEqual(1f, AgentVisualAnimatorBase.FadeScale(AgentVisualAnimatorBase.FadeStartDelay));
        }

        [Test]
        public void FadeScale_LerpsToZero_OverTheFadeDuration()
        {
            float half = AgentVisualAnimatorBase.FadeStartDelay + AgentVisualAnimatorBase.FadeDuration * 0.5f;

            Assert.AreEqual(0.5f, AgentVisualAnimatorBase.FadeScale(half), 0.001f);
            Assert.AreEqual(0f, AgentVisualAnimatorBase.FadeScale(AgentVisualAnimatorBase.FadeStartDelay + AgentVisualAnimatorBase.FadeDuration), 0.001f);
        }

        [Test]
        public void FadeScale_NeverGoesNegative_PastTheFade()
        {
            Assert.AreEqual(0f, AgentVisualAnimatorBase.FadeScale(AgentVisualAnimatorBase.FadeStartDelay + AgentVisualAnimatorBase.FadeDuration + 10f));
        }

        [Test]
        public void Apply_BeforeDeath_CallsOnApply()
        {
            animator.Apply(new AgentIntent { aim = true });

            Assert.AreEqual(1, animator.applyCalls);
        }

        [Test]
        public void Apply_AfterDeath_DoesNothing()
        {
            animator.PlayDeath();

            animator.Apply(new AgentIntent { aim = true });

            Assert.AreEqual(0, animator.applyCalls);
        }

        [Test]
        public void PlayHit_AfterDeath_DoesNothing()
        {
            animator.PlayDeath();

            animator.PlayHit(Hit(10f));

            Assert.AreEqual(0, animator.hitCalls);
        }

        [Test]
        public void PlayDeath_CallsOnDeathBeginOnce()
        {
            animator.PlayDeath();
            animator.PlayDeath();

            Assert.AreEqual(1, animator.deathBeginCalls);
        }

        [Test]
        public void Tick_WhileAlive_CallsOnLiveTickNotOnDeathTick()
        {
            animator.Tick(0.1f);
            animator.Tick(0.1f);

            Assert.AreEqual(2, animator.liveTickCalls);
            Assert.AreEqual(0, animator.deathTickCalls);
        }

        [Test]
        public void Tick_AfterDeath_CallsOnDeathTickWithElapsedTime()
        {
            animator.PlayDeath();

            animator.Tick(1f);
            animator.Tick(2f);

            Assert.AreEqual(2, animator.deathTickCalls);
            Assert.AreEqual(3f, animator.lastDeathTick, 0.0001f);
            Assert.AreEqual(0, animator.liveTickCalls);
        }

        [Test]
        public void Tick_AfterDeath_ScalesTheVisualDownOnceTheFadeStarts()
        {
            animator.PlayDeath();

            animator.Tick(AgentVisualAnimatorBase.FadeStartDelay); // right at the start of the fade
            Assert.AreEqual(1f, gameObject.transform.localScale.x, 0.001f);

            animator.Tick(AgentVisualAnimatorBase.FadeDuration * 0.5f); // halfway through the fade
            Assert.AreEqual(0.5f, gameObject.transform.localScale.x, 0.01f);

            animator.Tick(AgentVisualAnimatorBase.FadeDuration * 0.5f); // fade complete
            Assert.AreEqual(0f, gameObject.transform.localScale.x, 0.001f);
            Assert.IsFalse(gameObject.activeSelf);
        }

        [Test]
        public void VisualRoot_DefaultsToItsOwnTransform()
        {
            animator.PlayDeath();

            animator.Tick(AgentVisualAnimatorBase.FadeStartDelay + AgentVisualAnimatorBase.FadeDuration);

            Assert.IsFalse(gameObject.activeSelf, "with no explicit visual root, the fade disables this object itself");
        }
    }
}
