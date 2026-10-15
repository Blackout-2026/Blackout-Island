// Owner: Core Developer (Tactical Guard AI).
using System.Collections.Generic;
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>Selects the highest-scoring action, with a small bias toward the current one.</summary>
    public static class UtilityActionSelector
    {
        public const float DefaultHysteresisBonus = 0.05f;

        public static UtilityAction Select(
            IReadOnlyList<UtilityAction> actions,
            UtilityAction currentAction,
            float hysteresisBonus = DefaultHysteresisBonus)
        {
            if (actions == null || actions.Count == 0)
                return null;

            float bonus = float.IsNaN(hysteresisBonus) || float.IsInfinity(hysteresisBonus)
                ? 0f
                : Mathf.Max(0f, hysteresisBonus);

            UtilityAction selected = null;
            float highestBiasedScore = float.NegativeInfinity;
            for (int i = 0; i < actions.Count; i++)
            {
                UtilityAction action = actions[i];
                if (action == null)
                    continue;

                float biasedScore = action.Evaluate();
                if (object.ReferenceEquals(action, currentAction))
                    biasedScore += bonus;

                // Strict comparison makes ties deterministic: the first action wins.
                if (biasedScore > highestBiasedScore)
                {
                    selected = action;
                    highestBiasedScore = biasedScore;
                }
            }

            return selected;
        }
    }
}
