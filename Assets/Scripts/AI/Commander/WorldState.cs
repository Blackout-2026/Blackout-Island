// Owner: Agent Controller (Commander AI).
using System;
using System.Collections.Generic;

namespace Blackout.AI.Commander
{
    /// <summary>A small, value-comparable set of boolean and integer world facts.</summary>
    public sealed class WorldState : IEquatable<WorldState>
    {
        private readonly Dictionary<string, object> values;
        private static readonly object TrueValue = true;
        private static readonly object FalseValue = false;

        public WorldState()
        {
            values = new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private WorldState(Dictionary<string, object> source)
        {
            values = new Dictionary<string, object>(source, StringComparer.Ordinal);
        }

        public int Count => values.Count;

        public IReadOnlyDictionary<string, object> Values => values;

        public WorldState Set(string key, bool value)
        {
            values[ValidateKey(key)] = value ? TrueValue : FalseValue;
            return this;
        }

        public WorldState Set(string key, int value)
        {
            ValidateKey(key);
            // Observation writes these facts every think tick: only box a changed count.
            if (!values.TryGetValue(key, out object previous) || !(previous is int oldValue) || oldValue != value)
                values[key] = value;
            return this;
        }

        public bool Remove(string key)
        {
            return values.Remove(ValidateKey(key));
        }

        public bool TryGetBool(string key, out bool value)
        {
            object raw;
            if (values.TryGetValue(ValidateKey(key), out raw) && raw is bool)
            {
                value = (bool)raw;
                return true;
            }

            value = default(bool);
            return false;
        }

        public bool TryGetInt(string key, out int value)
        {
            object raw;
            if (values.TryGetValue(ValidateKey(key), out raw) && raw is int)
            {
                value = (int)raw;
                return true;
            }

            value = default(int);
            return false;
        }

        /// <summary>Returns true when every fact in the requested state matches this state.</summary>
        public bool Satisfies(WorldState requested)
        {
            if (requested == null)
                throw new ArgumentNullException(nameof(requested));

            foreach (KeyValuePair<string, object> pair in requested.values)
            {
                object value;
                if (!values.TryGetValue(pair.Key, out value) || !Equals(value, pair.Value))
                    return false;
            }

            return true;
        }

        public WorldState Clone()
        {
            return new WorldState(values);
        }

        public bool Equals(WorldState other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (ReferenceEquals(other, null) || values.Count != other.values.Count)
                return false;

            foreach (KeyValuePair<string, object> pair in values)
            {
                object otherValue;
                if (!other.values.TryGetValue(pair.Key, out otherValue) || !Equals(pair.Value, otherValue))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WorldState);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            foreach (KeyValuePair<string, object> pair in values)
            {
                // XOR keeps the hash independent of insertion order, matching dictionary equality.
                hash ^= StringComparer.Ordinal.GetHashCode(pair.Key) ^ pair.Value.GetHashCode();
            }

            return hash;
        }

        internal WorldState WithEffects(WorldState effects)
        {
            WorldState result = Clone();
            foreach (KeyValuePair<string, object> pair in effects.values)
                result.values[pair.Key] = pair.Value;
            return result;
        }

        private static string ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("World-state keys must not be empty.", nameof(key));
            return key;
        }
    }
}
