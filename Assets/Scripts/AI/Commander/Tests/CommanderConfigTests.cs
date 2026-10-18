// Owner: Agent Controller (Commander AI).
using System;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Commander.Tests
{
    public sealed class CommanderConfigTests
    {
        private CommanderConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<CommanderConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void Defaults_PlanSearchWhenPlayerIsUnknown()
        {
            var state = new WorldState().Set(CommanderFacts.PlayerLocated, false);
            var goal = new GoapGoal(new WorldState().Set(CommanderFacts.PlayerLocated, true));

            var plan = new GoapPlanner().Plan(state, goal, config.BuildActions());

            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan[0].Name, Is.EqualTo(CommanderConfig.OrderSearchZone));
            Assert.That(plan[0].Cost, Is.EqualTo(3f));
        }

        [Test]
        public void Defaults_PlanRegroupThenConvergeForUnassembledSquad()
        {
            var state = new WorldState()
                .Set(CommanderFacts.PlayerLocated, true)
                .Set(CommanderFacts.SquadAssembled, false)
                .Set(CommanderFacts.PlayerNeutralized, false);
            var goal = new GoapGoal(new WorldState().Set(CommanderFacts.PlayerNeutralized, true));

            var plan = new GoapPlanner().Plan(state, goal, config.BuildActions());

            Assert.That(plan.Count, Is.EqualTo(2));
            Assert.That(plan[0].Name, Is.EqualTo(CommanderConfig.OrderRegroup));
            Assert.That(plan[1].Name, Is.EqualTo(CommanderConfig.OrderConverge));
            Assert.That(plan[0].Cost + plan[1].Cost, Is.EqualTo(4f));
        }

        [Test]
        public void ConfiguredFactsAndCost_ControlPlanAvailability()
        {
            config.orderSearchZone.cost = 7f;
            config.orderSearchZone.preconditions = new[]
            {
                new CommanderFactValue(CommanderFact.GeneratorDisabled, true),
                new CommanderFactValue(CommanderFact.AgentsAlive, 3)
            };
            config.orderSearchZone.effects = new[]
            {
                new CommanderFactValue(CommanderFact.CureTaken, true)
            };
            var state = new WorldState()
                .Set(CommanderFacts.GeneratorDisabled, true)
                .Set(CommanderFacts.AgentsAlive, 3);
            var goal = new GoapGoal(new WorldState().Set(CommanderFacts.CureTaken, true));
            GoapAction[] actions = config.BuildActions();
            var planner = new GoapPlanner();

            var plan = planner.Plan(state, goal, actions);

            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan[0].Name, Is.EqualTo(CommanderConfig.OrderSearchZone));
            Assert.That(plan[0].Cost, Is.EqualTo(7f));
            state.Set(CommanderFacts.AgentsAlive, 2);
            Assert.That(planner.Plan(state, goal, actions), Is.Empty);
        }

        [Test]
        public void BuiltActions_KeepTheirSnapshotWhenConfigChanges()
        {
            GoapAction[] actions = config.BuildActions();
            config.orderSearchZone.cost = 20f;
            config.orderSearchZone.preconditions[0].booleanValue = true;

            Assert.That(actions[1].Cost, Is.EqualTo(3f));
            Assert.That(actions[1].IsApplicable(
                new WorldState().Set(CommanderFacts.PlayerLocated, false)), Is.True);
        }

        [Test]
        public void BuildActions_RejectsConflictingDuplicateFacts()
        {
            config.orderConverge.preconditions = new[]
            {
                new CommanderFactValue(CommanderFact.PlayerLocated, true),
                new CommanderFactValue(CommanderFact.PlayerLocated, false)
            };

            Assert.Throws<ArgumentException>(() => config.BuildActions());
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void BuildActions_RejectsInvalidPlanningCosts(float cost)
        {
            config.orderRegroup.cost = cost;

            Assert.Throws<ArgumentOutOfRangeException>(() => config.BuildActions());
        }
    }
}
