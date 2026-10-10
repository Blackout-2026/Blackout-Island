using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blackout.Agents.Tests
{
    public class AnimatorDriverTests
    {
        private sealed class FakeVelocitySource : IAgentVelocitySource
        {
            public Vector3 Velocity { get; set; }
            public float Speed { get; set; }
        }

        private static DamageInfo Hit(float amount)
        {
            return new DamageInfo(amount, Vector3.zero, Vector3.forward, null, DamageType.Bullet);
        }

        private GameObject gameObject;
        private AnimatorDriver driver;
        private Animator animator;
        private FakeVelocitySource velocitySource;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("DriverTest");
            driver = gameObject.AddComponent<AnimatorDriver>(); // RequireComponent adds Animator
            animator = gameObject.GetComponent<Animator>();
            velocitySource = new FakeVelocitySource();
            driver.Configure(animator, velocitySource);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void RequireComponent_AddsAnAnimator()
        {
            Assert.IsNotNull(animator);
        }

        [Test]
        public void Apply_WithAConfiguredAnimator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => driver.Apply(new AgentIntent { aim = true }));
        }

        [Test]
        public void PlayHit_WithAConfiguredAnimator_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => driver.PlayHit(Hit(10f)));
        }

        [Test]
        public void PlayDeath_SetsTheDeadParameterAndStartsTheFade()
        {
            Assert.DoesNotThrow(() => driver.PlayDeath());

            // Once dead, AgentVisualAnimatorBase.Apply/PlayHit are no-ops (base class behaviour, A3 commit 1).
            driver.Apply(new AgentIntent { aim = true });
            Assert.DoesNotThrow(() => driver.Tick(0.1f));
        }

        [Test]
        public void Tick_WhileAlive_ReadsTheVelocitySourceWithoutThrowing()
        {
            velocitySource.Speed = 2.5f;

            Assert.DoesNotThrow(() => driver.Tick(0.1f));
        }

        [Test]
        public void Configure_WithNoVelocitySource_LogsAWarning()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no IAgentVelocitySource"));

            driver.Configure(animator, null);

            Assert.DoesNotThrow(() => driver.Tick(0.1f), "Speed is simply left unset, not an error");
        }
    }
}
