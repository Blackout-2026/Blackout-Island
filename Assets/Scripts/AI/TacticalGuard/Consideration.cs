// Owner: Core Developer (Tactical Guard AI).
using System;
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    public enum ResponseCurve
    {
        Linear,
        Quadratic,
        Logistic,
        Inverse
    }

    /// <summary>Maps an input value to a utility in the 0..1 range.</summary>
    [Serializable]
    public sealed class Consideration
    {
        [Range(0f, 1f)] public float inputValue;
        [Tooltip("Choose a curve that describes how strongly this input should affect an action.")]
        public ResponseCurve responseCurve = ResponseCurve.Linear;

        [Tooltip("Logistic curve slope. Larger values make the transition around Midpoint sharper.")]
        [Min(0f)] public float steepness = 10f;

        [Range(0f, 1f)] public float midpoint = 0.5f;

        public Consideration() { }

        public Consideration(ResponseCurve responseCurve, float steepness = 10f, float midpoint = 0.5f)
        {
            this.responseCurve = responseCurve;
            this.steepness = steepness;
            this.midpoint = midpoint;
        }

        public float Evaluate(float inputValue)
        {
            if (!IsFinite(inputValue))
                return 0f;

            float input = Mathf.Clamp01(inputValue);
            switch (responseCurve)
            {
                case ResponseCurve.Quadratic:
                    return input * input;
                case ResponseCurve.Logistic:
                    return EvaluateLogistic(input);
                case ResponseCurve.Inverse:
                    return 1f - input;
                default:
                    return input;
            }
        }

        public float Evaluate() => Evaluate(inputValue);

        private float EvaluateLogistic(float input)
        {
            // Normalize the sigmoid so the configured input range still maps to exact 0 and 1 endpoints.
            float slope = Mathf.Max(0f, IsFinite(steepness) ? steepness : 0f);
            float center = ClampFinite01(midpoint);
            if (slope == 0f)
                return input;

            float low = Sigmoid(-slope * center);
            float high = Sigmoid(slope * (1f - center));
            float range = high - low;
            if (range <= Mathf.Epsilon)
                return input;

            return ClampFinite01((Sigmoid(slope * (input - center)) - low) / range);
        }

        private static float Sigmoid(float value)
        {
            // Bound the exponent to avoid overflow for unusually large inspector values.
            value = Mathf.Clamp(value, -80f, 80f);
            return 1f / (1f + Mathf.Exp(-value));
        }

        internal static float ClampFinite01(float value)
        {
            return IsFinite(value) ? Mathf.Clamp01(value) : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
