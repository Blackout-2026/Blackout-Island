// Owner: Agent Controller (Commander AI).
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using Blackout.Shared;

namespace Blackout.AI.Commander
{
    /// <summary>Read-only formatting on diagnostic requests. Never called from the brain's think hot path.</summary>
    public static class CommanderDebugText
    {
        public static string Format(string goal, string state, IReadOnlyList<GoapAction> plan,
            int activeStep, WorldState observed, Blackboard board, float now)
        {
            var text = new StringBuilder(512);
            text.Append("Commander [GOAP] goal: ").Append(goal).Append(" | state: ").Append(state);
            text.Append("\nPlan: ");
            float total = 0f;
            float remaining = 0f;
            if (plan == null || plan.Count == 0)
                text.Append("(empty)");
            else
            {
                for (int i = 0; i < plan.Count; i++)
                {
                    if (i > 0) text.Append(" -> ");
                    if (i == activeStep) text.Append('[');
                    text.Append(plan[i].Name).Append("(cost=");
                    Number(text, plan[i].Cost);
                    text.Append(')');
                    if (i == activeStep) text.Append(']');
                    total += plan[i].Cost;
                    if (i >= activeStep) remaining += plan[i].Cost;
                }
            }
            text.Append(" | totalCost=");
            Number(text, total);
            text.Append(" | remainingCost=");
            Number(text, remaining);

            // A snapshot is labelled explicitly: reading diagnostics never refreshes observations or replans.
            text.Append("\nObserved (last think): ");
            Fact(text, observed, CommanderFacts.PlayerLocated);
            Fact(text, observed, CommanderFacts.CureTaken);
            Fact(text, observed, CommanderFacts.GeneratorDisabled);
            text.Append(CommanderFacts.AgentsAlive).Append('=');
            if (observed != null && observed.TryGetInt(CommanderFacts.AgentsAlive, out int living))
                text.Append(living.ToString(CultureInfo.InvariantCulture));
            else
                text.Append("unknown");
            text.Append("; ");
            Fact(text, observed, CommanderFacts.SquadAssembled);
            Fact(text, observed, CommanderFacts.PlayerNeutralized);

            if (board == null)
                return text.Append("\nBlackboard: unavailable (not initialized)").ToString();

            text.Append("\nBlackboard: alertLevel=").Append(board.alertLevel.ToString(CultureInfo.InvariantCulture))
                .Append("; cureTaken=").Append(Bool(board.cureTaken))
                .Append("; generatorDisabled=").Append(Bool(board.generatorDisabled))
                .Append("; squadAssembled=").Append(Bool(board.squadAssembled))
                .Append("; playerNeutralized=").Append(Bool(board.playerNeutralized))
                .Append("; registeredAgents=").Append(board.Agents.Count.ToString(CultureInfo.InvariantCulture));

            text.Append("\nlastKnownPlayerPos=(");
            Number(text, board.lastKnownPlayerPos.x);
            text.Append(", ");
            Number(text, board.lastKnownPlayerPos.y);
            text.Append(", ");
            Number(text, board.lastKnownPlayerPos.z);
            text.Append("); lastKnownPlayerTime=");
            if (float.IsNegativeInfinity(board.lastKnownPlayerTime)) text.Append("never");
            else Number(text, board.lastKnownPlayerTime);
            text.Append("; sightingAge=");
            float age = now - board.lastKnownPlayerTime;
            if (float.IsNaN(age) || float.IsInfinity(age)) text.Append("unknown");
            else Number(text, age);

            text.Append("\nAlert buff: ");
            if (!board.HasAlertBuff) text.Append("none");
            else
            {
                text.Append("speed=");
                Number(text, board.Alert.speedMultiplier);
                text.Append("; vision=");
                Number(text, board.Alert.visionMultiplier);
                text.Append("; aimError=");
                Number(text, board.Alert.aimErrorMultiplier);
            }

            text.Append("\nAgent states: ");
            if (board.AgentStates.Count == 0) text.Append("(none published)");
            bool first = true;
            foreach (KeyValuePair<string, string> agent in board.AgentStates)
            {
                if (!first) text.Append("; ");
                first = false;
                text.Append(agent.Key).Append('=');
                // AgentRoot publishes this very text as the Commander's state. Only include the
                // first line so a later diagnostic request cannot recursively embed old snapshots.
                if (agent.Value == null) text.Append("unknown");
                else
                {
                    int length = System.Math.Min(agent.Value.Length, 240);
                    int newline = agent.Value.IndexOf('\n');
                    int carriage = agent.Value.IndexOf('\r');
                    if (newline >= 0) length = System.Math.Min(length, newline);
                    if (carriage >= 0) length = System.Math.Min(length, carriage);
                    text.Append(agent.Value, 0, length);
                    if (length < agent.Value.Length) text.Append(" ...");
                }
            }
            return text.ToString();
        }

        private static void Fact(StringBuilder text, WorldState observed, string key)
        {
            text.Append(key).Append('=');
            text.Append(observed != null && observed.TryGetBool(key, out bool value) ? Bool(value) : "unknown");
            text.Append("; ");
        }

        private static string Bool(bool value) => value ? "true" : "false";

        private static void Number(StringBuilder text, float value)
        {
            text.Append(value.ToString("G", CultureInfo.InvariantCulture));
        }
    }
}
