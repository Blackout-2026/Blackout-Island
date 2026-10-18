// Owner: Agent Controller (Commander AI).
using System;
using System.Globalization;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Commander.Tests
{
    public sealed class CommanderDebugTextTests
    {
        [Test]
        public void BeforeInitialization_ReportsEmptyPlanAndUnknownFactsSafely()
        {
            string text = CommanderDebugText.Format("none (monitoring)", "Uninitialized",
                Array.Empty<GoapAction>(), 0, new WorldState(), null, 100f);

            Assert.That(text, Does.Contain("goal: none (monitoring) | state: Uninitialized"));
            Assert.That(text, Does.Contain("Plan: (empty) | totalCost=0 | remainingCost=0"));
            Assert.That(text, Does.Contain("playerLocated=unknown"));
            Assert.That(text, Does.Contain("agentsAlive=unknown"));
            Assert.That(text, Does.Contain("Blackboard: unavailable (not initialized)"));
        }

        [Test]
        public void Plan_ListsActionCostsAndDistinguishesTotalFromRemainingCost()
        {
            GoapAction[] plan = { Action("OrderRegroup", 2f), Action("OrderConverge", 2f) };

            string text = CommanderDebugText.Format("PlayerNeutralized", "OrderConverge",
                plan, 1, new WorldState(), new Blackboard(), 100f);

            Assert.That(text, Does.Contain("Plan: OrderRegroup(cost=2) -> [OrderConverge(cost=2)] | totalCost=4 | remainingCost=2"));
        }

        [Test]
        public void FractionalCosts_AreCultureIndependentAndNotRoundedToZero()
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                GoapAction[] plan = { Action("SmallCost", 0.0001f), Action("FractionalCost", 2.5f) };

                string text = CommanderDebugText.Format("Example", "SmallCost", plan, 0, null, null, 0f);

                Assert.That(text, Does.Contain("[SmallCost(cost=0.0001)]"));
                Assert.That(text, Does.Contain("FractionalCost(cost=2.5)"));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Test]
        public void Values_DistinguishObservedFactsFromTheCurrentBlackboard()
        {
            var observed = new WorldState()
                .Set(CommanderFacts.PlayerLocated, false)
                .Set(CommanderFacts.CureTaken, false)
                .Set(CommanderFacts.GeneratorDisabled, false)
                .Set(CommanderFacts.AgentsAlive, 3)
                .Set(CommanderFacts.SquadAssembled, false)
                .Set(CommanderFacts.PlayerNeutralized, false);
            var board = new Blackboard
            {
                cureTaken = true,
                generatorDisabled = true,
                squadAssembled = true,
                playerNeutralized = true,
                lastKnownPlayerPos = new Vector3(12.5f, 2f, -8.25f),
                lastKnownPlayerTime = 95f
            };
            board.BroadcastAlert(new AgentAlert(1.25f, 1.4f, 0.9f));
            board.SetAgentState("Patrol-1", "Search");

            string text = CommanderDebugText.Format("PlayerLocated", "OrderSearchZone",
                Array.Empty<GoapAction>(), 0, observed, board, 100f);

            Assert.That(text, Does.Contain("Observed (last think): playerLocated=false; cureTaken=false; generatorDisabled=false; agentsAlive=3; squadAssembled=false; playerNeutralized=false"));
            Assert.That(text, Does.Contain("Blackboard: alertLevel=1; cureTaken=true; generatorDisabled=true; squadAssembled=true; playerNeutralized=true"));
            Assert.That(text, Does.Contain("lastKnownPlayerPos=(12.5, 2, -8.25); lastKnownPlayerTime=95; sightingAge=5"));
            Assert.That(text, Does.Contain("Alert buff: speed=1.25; vision=1.4; aimError=0.9"));
            Assert.That(text, Does.Contain("Agent states: Patrol-1=Search"));
            Assert.That(observed.Satisfies(new WorldState().Set(CommanderFacts.CureTaken, false)), Is.True);
        }

        [Test]
        public void NeverSeen_ReportsSentinelInsteadOfInventingASighting()
        {
            string text = CommanderDebugText.Format("PlayerLocated", "Monitor", null, 0,
                new WorldState(), new Blackboard(), 100f);

            Assert.That(text, Does.Contain("lastKnownPlayerTime=never; sightingAge=unknown"));
            Assert.That(text, Does.Contain("Alert buff: none"));
            Assert.That(text, Does.Contain("Agent states: (none published)"));
        }

        [Test]
        public void PublishedDebugState_DoesNotRecursivelyEmbedPreviousSnapshots()
        {
            var board = new Blackboard();
            string original = CommanderDebugText.Format("PlayerLocated", "OrderSearchZone", null, 0, null, board, 100f);
            board.SetAgentState("Commander", original);
            string firstPublication = CommanderDebugText.Format("PlayerLocated", "OrderSearchZone", null, 0, null, board, 100f);
            board.SetAgentState("Commander", firstPublication);

            string nextPublication = CommanderDebugText.Format("PlayerLocated", "OrderSearchZone", null, 0, null, board, 100f);

            Assert.That(nextPublication, Is.EqualTo(firstPublication));
            Assert.That(nextPublication, Does.Contain("Agent states: Commander=Commander [GOAP] goal: PlayerLocated"));
        }

        private static GoapAction Action(string name, float cost) =>
            new GoapAction(name, new WorldState(), new WorldState(), cost);
    }
}
