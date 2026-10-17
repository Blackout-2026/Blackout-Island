// Owner: Core Developer (Tactical Guard AI).
using System.Globalization;
using System.Text;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>
    /// The text the shared debug overlay shows for a guard: the chosen action, every action's score, and the inputs
    /// that produced them. Three short lines, so the first line alone is still a readable state.
    /// </summary>
    public static class GuardDebugFormatter
    {
        public static string ActionName(GuardActionType action)
        {
            switch (action)
            {
                case GuardActionType.Attack: return "Attack";
                case GuardActionType.MoveToCover: return "MoveToCover";
                case GuardActionType.Reposition: return "Reposition";
                default: return "HoldPost";
            }
        }

        /// <summary>Appends the three lines to the builder (the caller clears it). coverId may be null.</summary>
        public static void Append(StringBuilder text, GuardActionScorer scorer, in GuardSituation situation, string coverId)
        {
            text.Append("TacticalGuard ").Append(ActionName(scorer.CurrentAction));
            if (!string.IsNullOrEmpty(coverId))
            {
                text.Append(" (cover ").Append(coverId).Append(')');
            }

            text.Append("\nscores:");
            for (int i = 0; i < GuardActionScorer.ActionCount; i++)
            {
                var action = (GuardActionType)i;
                text.Append(i == 0 ? " " : " | ").Append(ActionName(action)).Append(' ').Append(Format(scorer.GetScore(action)));
                if (action == scorer.CurrentAction)
                {
                    text.Append('*'); // the winner; it may not have the highest raw score because of the hysteresis bonus
                }
            }

            text.Append("\ninputs: see=").Append(situation.canSeePlayer ? "yes" : "no")
                .Append(" dist=").Append(situation.distanceToPlayer.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" know=").Append(Format(situation.knowledge))
                .Append(" threat=").Append(Format(situation.threat))
                .Append(" hp=").Append(Format(situation.healthFraction))
                .Append(" post=").Append(situation.distanceFromPost.ToString("0.0", CultureInfo.InvariantCulture))
                .Append(" cover=").Append(Format(situation.coverQuality));
        }

        private static string Format(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
