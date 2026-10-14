using System;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class DifficultyConfigTests
    {
        private DifficultyConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<DifficultyConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void DefaultStats_HaveExactlyOneRowPerKind()
        {
            AgentStats[] defaults = DifficultyConfig.DefaultStats();

            foreach (AgentKind kind in Enum.GetValues(typeof(AgentKind)))
            {
                int rows = 0;
                foreach (AgentStats row in defaults)
                {
                    if (row.kind == kind)
                    {
                        rows++;
                    }
                }
                Assert.AreEqual(1, rows, $"{kind} should have one row");
            }
        }

        [Test]
        public void NewAsset_HoldsThePrototypeHealthAndDamage()
        {
            Assert.AreEqual(90f, config.Get(AgentKind.Hunter).maxHealth);
            Assert.AreEqual(80f, config.Get(AgentKind.SecurityPatrol).maxHealth);
            Assert.AreEqual(100f, config.Get(AgentKind.TacticalGuard).maxHealth);
            Assert.AreEqual(70f, config.Get(AgentKind.Commander).maxHealth);

            Assert.AreEqual(12f, config.Get(AgentKind.Hunter).damage);
            Assert.AreEqual(7f, config.Get(AgentKind.SecurityPatrol).damage);
            Assert.AreEqual(8f, config.Get(AgentKind.TacticalGuard).damage);
            Assert.AreEqual(5f, config.Get(AgentKind.Commander).damage);
        }

        [Test]
        public void NewAsset_HoldsThePrototypeReactionAndAimError()
        {
            Assert.AreEqual(0.7f, config.Get(AgentKind.Hunter).reactionDelay, 0.0001f);
            Assert.AreEqual(0.6f, config.Get(AgentKind.SecurityPatrol).reactionDelay, 0.0001f);
            Assert.AreEqual(0.5f, config.Get(AgentKind.TacticalGuard).reactionDelay, 0.0001f);
            Assert.AreEqual(0.8f, config.Get(AgentKind.Commander).reactionDelay, 0.0001f);

            Assert.AreEqual(0.024f, config.Get(AgentKind.Hunter).spread, 0.0001f);
            Assert.AreEqual(0.034f, config.Get(AgentKind.SecurityPatrol).spread, 0.0001f);
            Assert.AreEqual(0.032f, config.Get(AgentKind.TacticalGuard).spread, 0.0001f);
            Assert.AreEqual(0.05f, config.Get(AgentKind.Commander).spread, 0.0001f);
        }

        [Test]
        public void NewAsset_HoldsThePrototypeBurstsRangesAndSpeeds()
        {
            AgentStats hunter = config.Get(AgentKind.Hunter);
            Assert.AreEqual(1, hunter.burstCount);
            Assert.AreEqual(45f, hunter.range);
            Assert.AreEqual(1.6f, hunter.walkSpeed);
            Assert.AreEqual(4.5f, hunter.runSpeed);

            AgentStats guard = config.Get(AgentKind.TacticalGuard);
            Assert.AreEqual(4, guard.burstCount);
            Assert.AreEqual(3.5f, guard.runSpeed);
        }

        [Test]
        public void TurnRates_AreConvertedFromRadiansToDegrees()
        {
            Assert.AreEqual(5f * Mathf.Rad2Deg, config.Get(AgentKind.SecurityPatrol).turnRateDegrees, 0.01f);
            Assert.AreEqual(6f * Mathf.Rad2Deg, config.Get(AgentKind.Hunter).turnRateDegrees, 0.01f);
            Assert.AreEqual(4f * Mathf.Rad2Deg, config.Get(AgentKind.TacticalGuard).turnRateDegrees, 0.01f);
        }

        [Test]
        public void Multipliers_AllOne_LeaveTheNumbersUnchanged()
        {
            AgentStats expected = DifficultyConfig.DefaultStats()[0];

            AgentStats actual = config.Get(expected.kind);

            Assert.AreEqual(expected.maxHealth, actual.maxHealth);
            Assert.AreEqual(expected.damage, actual.damage);
            Assert.AreEqual(expected.reactionDelay, actual.reactionDelay);
            Assert.AreEqual(expected.spread, actual.spread);
        }

        [Test]
        public void Multipliers_ScaleOnlyHealthDamageReactionAndAimError()
        {
            config.healthMultiplier = 2f;
            config.damageMultiplier = 1.5f;
            config.reactionDelayMultiplier = 0.5f;
            config.aimErrorMultiplier = 0.25f;

            AgentStats scaled = config.Get(AgentKind.Hunter);
            AgentStats basis = DifficultyConfig.DefaultStats()[(int)AgentKind.Hunter];

            Assert.AreEqual(basis.maxHealth * 2f, scaled.maxHealth, 0.0001f);
            Assert.AreEqual(basis.damage * 1.5f, scaled.damage, 0.0001f);
            Assert.AreEqual(basis.reactionDelay * 0.5f, scaled.reactionDelay, 0.0001f);
            Assert.AreEqual(basis.spread * 0.25f, scaled.spread, 0.0001f);

            Assert.AreEqual(basis.range, scaled.range, "range is not a difficulty setting");
            Assert.AreEqual(basis.walkSpeed, scaled.walkSpeed, "speed is not a difficulty setting");
            Assert.AreEqual(basis.burstCount, scaled.burstCount);
        }

        [Test]
        public void Get_ReturnsACopy_SoScalingNeverChangesTheAsset()
        {
            config.healthMultiplier = 3f;

            config.Get(AgentKind.Hunter);
            config.healthMultiplier = 1f;

            Assert.AreEqual(90f, config.Get(AgentKind.Hunter).maxHealth);
        }

        [Test]
        public void Scaled_IsAPureFunctionOfItsInputs()
        {
            AgentStats basis = DifficultyConfig.DefaultStats()[(int)AgentKind.Commander];

            AgentStats scaled = DifficultyConfig.Scaled(basis, 2f, 2f, 2f, 2f);

            Assert.AreEqual(140f, scaled.maxHealth);
            Assert.AreEqual(70f, basis.maxHealth, "the input is not modified");
        }
    }
}
