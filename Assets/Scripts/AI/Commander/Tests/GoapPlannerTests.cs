// Owner: Agent Controller (Commander AI).
using System.Collections.Generic;
using NUnit.Framework;

namespace Blackout.AI.Commander.Tests
{
    public sealed class GoapPlannerTests
    {
        private readonly GoapPlanner planner = new GoapPlanner();

        [Test]
        public void Plan_FindsActionSequenceThatSatisfiesGoal()
        {
            WorldState initial = new WorldState().Set("HasKey", false);
            GoapGoal goal = new GoapGoal(new WorldState().Set("DoorOpen", true));
            GoapAction[] actions =
            {
                Action("FindKey", new WorldState(), new WorldState().Set("HasKey", true), 1f),
                Action("OpenDoor", new WorldState().Set("HasKey", true), new WorldState().Set("DoorOpen", true), 1f)
            };

            IReadOnlyList<GoapAction> plan = planner.Plan(initial, goal, actions);

            Assert.That(plan, Has.Count.EqualTo(2));
            Assert.AreEqual("FindKey", plan[0].Name);
            Assert.AreEqual("OpenDoor", plan[1].Name);
        }

        [Test]
        public void Plan_ChoosesLowestTotalCostSequence()
        {
            WorldState initial = new WorldState();
            GoapGoal goal = new GoapGoal(new WorldState().Set("DoorOpen", true));
            GoapAction[] actions =
            {
                Action("LongRoute", new WorldState(), new WorldState().Set("DoorOpen", true), 8f),
                Action("FindKey", new WorldState(), new WorldState().Set("HasKey", true), 1f),
                Action("OpenDoor", new WorldState().Set("HasKey", true), new WorldState().Set("DoorOpen", true), 1f)
            };

            IReadOnlyList<GoapAction> plan = planner.Plan(initial, goal, actions);

            Assert.That(plan, Has.Count.EqualTo(2));
            Assert.AreEqual("FindKey", plan[0].Name);
            Assert.AreEqual("OpenDoor", plan[1].Name);
        }

        [Test]
        public void Plan_ReturnsEmptyWhenGoalIsUnreachable()
        {
            WorldState initial = new WorldState().Set("HasKey", false);
            GoapGoal goal = new GoapGoal(new WorldState().Set("DoorOpen", true));

            IReadOnlyList<GoapAction> plan = planner.Plan(initial, goal, new GoapAction[0]);

            Assert.That(plan, Is.Empty);
            Assert.IsFalse(planner.CanPlan(initial, new[] { goal }, new GoapAction[0]));
        }

        [Test]
        public void Plan_ReplansFromChangedWorldState()
        {
            WorldState initial = new WorldState().Set("HasKey", false);
            GoapGoal goal = new GoapGoal(new WorldState().Set("DoorOpen", true));
            GoapAction[] actions =
            {
                Action("FindKey", new WorldState(), new WorldState().Set("HasKey", true), 1f),
                Action("OpenDoor", new WorldState().Set("HasKey", true), new WorldState().Set("DoorOpen", true), 1f)
            };

            IReadOnlyList<GoapAction> firstPlan = planner.Plan(initial, goal, actions);
            WorldState updated = new WorldState().Set("HasKey", true);
            IReadOnlyList<GoapAction> replanned = planner.Plan(updated, goal, actions);

            Assert.That(firstPlan, Has.Count.EqualTo(2));
            Assert.That(replanned, Has.Count.EqualTo(1));
            Assert.AreEqual("OpenDoor", replanned[0].Name);
        }

        [Test]
        public void Plan_PrefersHigherPriorityReachableGoal()
        {
            GoapGoal lowPriority = new GoapGoal(new WorldState().Set("Patrolling", true), 1);
            GoapGoal highPriority = new GoapGoal(new WorldState().Set("AlertRaised", true), 5);
            GoapAction[] actions =
            {
                Action("Patrol", new WorldState(), new WorldState().Set("Patrolling", true), 1f),
                Action("RaiseAlert", new WorldState(), new WorldState().Set("AlertRaised", true), 10f)
            };

            IReadOnlyList<GoapAction> plan = planner.Plan(
                new WorldState(), new[] { lowPriority, highPriority }, actions);

            Assert.That(plan, Has.Count.EqualTo(1));
            Assert.AreEqual("RaiseAlert", plan[0].Name);
        }

        private static GoapAction Action(string name, WorldState preconditions, WorldState effects, float cost)
        {
            return new GoapAction(name, preconditions, effects, cost);
        }
    }
}
