// Owner: Core Developer (Tactical Guard AI).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>Scores an action by combining its considerations with a geometric mean.</summary>
    [Serializable]
    public sealed class UtilityAction
    {
        public string actionName;
        [Min(0f)] public float weight = 1f;
        public List<Consideration> considerations = new List<Consideration>();

        public UtilityAction() { }

        public UtilityAction(string actionName, float weight, params Consideration[] considerations)
        {
            this.actionName = actionName;
            this.weight = weight;
            this.considerations = considerations == null
                ? new List<Consideration>()
                : new List<Consideration>(considerations);
        }

        /// <summary>
        /// Scores this action by applying the geometric-mean compensation factor to the
        /// product of its consideration utilities, then multiplying by its weight.
        /// </summary>
        public float Evaluate()
        {
            if (considerations == null || considerations.Count == 0)
                return 0f;

            double product = 1d;
            int count = 0;
            for (int i = 0; i < considerations.Count; i++)
            {
                Consideration consideration = considerations[i];
                if (consideration == null)
                    return 0f;

                product *= consideration.Evaluate();
                count++;
            }

            if (count == 0)
                return 0f;

            float safeWeight = float.IsNaN(weight) || float.IsInfinity(weight)
                ? 0f
                : Mathf.Max(0f, weight);
            return (float)(Math.Pow(product, 1d / count) * safeWeight);
        }
    }
}
