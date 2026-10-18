// Owner: Agent Controller (Commander AI).
using System;
using System.Collections.Generic;

namespace Blackout.AI.Commander
{
    /// <summary>Finds least-cost action sequences using A* over discrete world states.</summary>
    public sealed class GoapPlanner
    {
        /// <summary>
        /// Plans for the supplied goal. Returns an empty list when the goal is already met or
        /// cannot be reached; callers can use <see cref="CanPlan"/> to distinguish those cases.
        /// </summary>
        public IReadOnlyList<GoapAction> Plan(
            WorldState currentState,
            GoapGoal goal,
            IEnumerable<GoapAction> actions)
        {
            if (goal == null)
                throw new ArgumentNullException(nameof(goal));

            return Plan(currentState, new[] { goal }, actions);
        }

        /// <summary>
        /// Chooses the cheapest reachable plan among the highest-priority reachable goals.
        /// Goals with equal priority are compared by plan cost; input order breaks exact ties.
        /// </summary>
        public IReadOnlyList<GoapAction> Plan(
            WorldState currentState,
            IEnumerable<GoapGoal> goals,
            IEnumerable<GoapAction> actions)
        {
            ValidateInputs(currentState, goals, actions, out List<GoapGoal> goalList,
                out List<GoapAction> actionList);

            List<int> priorities = new List<int>();
            for (int i = 0; i < goalList.Count; i++)
            {
                int priority = goalList[i].Priority;
                if (!priorities.Contains(priority))
                    priorities.Add(priority);
            }
            priorities.Sort((left, right) => right.CompareTo(left));

            for (int priorityIndex = 0; priorityIndex < priorities.Count; priorityIndex++)
            {
                int priority = priorities[priorityIndex];
                PlanResult bestPlan = null;

                for (int goalIndex = 0; goalIndex < goalList.Count; goalIndex++)
                {
                    GoapGoal goal = goalList[goalIndex];
                    if (goal.Priority != priority)
                        continue;

                    PlanResult candidate = Search(currentState, goal, actionList);
                    if (candidate != null && (bestPlan == null || candidate.Cost < bestPlan.Cost))
                        bestPlan = candidate;
                }

                if (bestPlan != null)
                    return bestPlan.Actions;
            }

            return EmptyPlan();
        }

        /// <summary>True when at least one supplied goal is already met or reachable.</summary>
        public bool CanPlan(
            WorldState currentState,
            IEnumerable<GoapGoal> goals,
            IEnumerable<GoapAction> actions)
        {
            ValidateInputs(currentState, goals, actions, out List<GoapGoal> goalList,
                out List<GoapAction> actionList);

            for (int i = 0; i < goalList.Count; i++)
            {
                if (Search(currentState, goalList[i], actionList) != null)
                    return true;
            }

            return false;
        }

        private static PlanResult Search(
            WorldState start,
            GoapGoal goal,
            List<GoapAction> actions)
        {
            if (goal.IsSatisfiedBy(start))
                return new PlanResult(EmptyPlan(), 0f);

            List<SearchNode> open = new List<SearchNode>();
            Dictionary<WorldState, float> bestCosts = new Dictionary<WorldState, float>();
            WorldState startState = start.Clone();
            SearchNode startNode = new SearchNode(startState, 0f, Heuristic(startState, goal, actions), null, null);
            open.Add(startNode);
            bestCosts.Add(startState, 0f);

            while (open.Count > 0)
            {
                SearchNode current = RemoveLowestEstimate(open);
                float knownCost;
                if (bestCosts.TryGetValue(current.State, out knownCost) && current.Cost > knownCost)
                    continue;

                if (goal.IsSatisfiedBy(current.State))
                    return new PlanResult(BuildPlan(current), current.Cost);

                for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
                {
                    GoapAction action = actions[actionIndex];
                    if (!action.IsApplicable(current.State))
                        continue;

                    float nextCost = current.Cost + action.Cost;
                    if (float.IsInfinity(nextCost))
                        continue;

                    WorldState nextState = action.Apply(current.State);
                    float previousCost;
                    if (bestCosts.TryGetValue(nextState, out previousCost) && nextCost >= previousCost)
                        continue;

                    bestCosts[nextState] = nextCost;
                    float estimate = nextCost + Heuristic(nextState, goal, actions);
                    open.Add(new SearchNode(nextState, nextCost, estimate, current, action));
                }
            }

            return null;
        }

        private static SearchNode RemoveLowestEstimate(List<SearchNode> open)
        {
            int bestIndex = 0;
            for (int i = 1; i < open.Count; i++)
            {
                SearchNode candidate = open[i];
                SearchNode best = open[bestIndex];
                if (candidate.Estimate < best.Estimate ||
                    (candidate.Estimate == best.Estimate && candidate.Cost < best.Cost))
                {
                    bestIndex = i;
                }
            }

            SearchNode result = open[bestIndex];
            open.RemoveAt(bestIndex);
            return result;
        }

        private static float Heuristic(WorldState state, GoapGoal goal, List<GoapAction> actions)
        {
            WorldState desired = goal.DesiredState;
            if (desired.Count == 0)
                return 0f;

            int unmetFacts = 0;
            foreach (KeyValuePair<string, object> fact in desired.Values)
            {
                object currentValue;
                if (!state.Values.TryGetValue(fact.Key, out currentValue) || !Equals(currentValue, fact.Value))
                    unmetFacts++;
            }

            if (unmetFacts == 0)
                return 0f;

            // Scale the unmet-key count so one cheap action that satisfies several goal facts
            // cannot make the heuristic overestimate the remaining cost.
            float cheapestCostPerFact = float.PositiveInfinity;
            for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
            {
                WorldState effects = actions[actionIndex].Effects;
                int usefulEffects = 0;
                foreach (KeyValuePair<string, object> effect in effects.Values)
                {
                    object desiredValue;
                    if (desired.Values.TryGetValue(effect.Key, out desiredValue) && Equals(effect.Value, desiredValue))
                        usefulEffects++;
                }

                if (usefulEffects > 0)
                {
                    float costPerFact = actions[actionIndex].Cost / usefulEffects;
                    if (costPerFact < cheapestCostPerFact)
                        cheapestCostPerFact = costPerFact;
                }
            }

            return float.IsPositiveInfinity(cheapestCostPerFact)
                ? 0f
                : unmetFacts * cheapestCostPerFact;
        }

        private static IReadOnlyList<GoapAction> BuildPlan(SearchNode goalNode)
        {
            List<GoapAction> plan = new List<GoapAction>();
            SearchNode node = goalNode;
            while (node.Parent != null)
            {
                plan.Add(node.Action);
                node = node.Parent;
            }
            plan.Reverse();
            return plan.AsReadOnly();
        }

        private static IReadOnlyList<GoapAction> EmptyPlan()
        {
            return new List<GoapAction>().AsReadOnly();
        }

        private static void ValidateInputs(
            WorldState currentState,
            IEnumerable<GoapGoal> goals,
            IEnumerable<GoapAction> actions,
            out List<GoapGoal> goalList,
            out List<GoapAction> actionList)
        {
            if (currentState == null)
                throw new ArgumentNullException(nameof(currentState));
            if (goals == null)
                throw new ArgumentNullException(nameof(goals));
            if (actions == null)
                throw new ArgumentNullException(nameof(actions));

            goalList = new List<GoapGoal>(goals);
            actionList = new List<GoapAction>(actions);
            for (int i = 0; i < goalList.Count; i++)
            {
                if (goalList[i] == null)
                    throw new ArgumentException("Goal lists must not contain null entries.", nameof(goals));
            }
            for (int i = 0; i < actionList.Count; i++)
            {
                if (actionList[i] == null)
                    throw new ArgumentException("Action lists must not contain null entries.", nameof(actions));
            }
        }

        private sealed class PlanResult
        {
            public PlanResult(IReadOnlyList<GoapAction> actions, float cost)
            {
                Actions = actions;
                Cost = cost;
            }

            public IReadOnlyList<GoapAction> Actions { get; }
            public float Cost { get; }
        }

        private sealed class SearchNode
        {
            public SearchNode(WorldState state, float cost, float estimate, SearchNode parent, GoapAction action)
            {
                State = state;
                Cost = cost;
                Estimate = estimate;
                Parent = parent;
                Action = action;
            }

            public WorldState State { get; }
            public float Cost { get; }
            public float Estimate { get; }
            public SearchNode Parent { get; }
            public GoapAction Action { get; }
        }
    }
}
