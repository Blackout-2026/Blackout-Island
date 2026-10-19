// Owner: Systems Engineer (Hunter AI).
using System;
using System.Collections.Generic;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// Names one value on a <see cref="LocalBlackboard"/>. Create keys once, when the tree is built, and share them
    /// between the nodes that write and read the value. Keys are compared by reference: two keys with the same name
    /// are different keys.
    /// </summary>
    public sealed class BlackboardKey<T>
    {
        public BlackboardKey(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// One agent's private memory, shared by the nodes of its tree (the prototype's <c>this.bb</c>): a condition
    /// writes what it found, and later actions in the same branch read it. Not the squad
    /// <see cref="Blackout.Shared.Blackboard"/>, which every agent shares.
    /// Values are stored in one dictionary per value type, so a float or a Vector3 is never boxed, and writing a key
    /// that already has a value allocates nothing (docs/reference/systems-engineer.md section 13).
    /// </summary>
    public sealed class LocalBlackboard
    {
        private readonly Dictionary<Type, IStore> stores = new Dictionary<Type, IStore>();

        private interface IStore
        {
            int Count { get; }
            void Clear();
        }

        private sealed class Store<T> : IStore
        {
            public readonly Dictionary<BlackboardKey<T>, T> Values = new Dictionary<BlackboardKey<T>, T>();

            public int Count => Values.Count;

            public void Clear()
            {
                Values.Clear();
            }
        }

        /// <summary>How many keys have a value, over all value types.</summary>
        public int Count
        {
            get
            {
                int count = 0;
                foreach (IStore store in stores.Values)
                {
                    count += store.Count;
                }

                return count;
            }
        }

        public void Set<T>(BlackboardKey<T> key, T value)
        {
            CheckKey(key);
            GetStore<T>(true).Values[key] = value;
        }

        /// <summary>False if the key was never set, or was removed, so "nothing heard" differs from "heard at 0,0,0".</summary>
        public bool TryGet<T>(BlackboardKey<T> key, out T value)
        {
            CheckKey(key);
            Store<T> store = GetStore<T>(false);
            if (store != null && store.Values.TryGetValue(key, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>The key's value, or <paramref name="fallback"/> if it has none.</summary>
        public T Get<T>(BlackboardKey<T> key, T fallback = default)
        {
            return TryGet(key, out T value) ? value : fallback;
        }

        public bool Has<T>(BlackboardKey<T> key)
        {
            CheckKey(key);
            Store<T> store = GetStore<T>(false);
            return store != null && store.Values.ContainsKey(key);
        }

        /// <summary>Forgets the key's value. Returns false if it had none.</summary>
        public bool Remove<T>(BlackboardKey<T> key)
        {
            CheckKey(key);
            Store<T> store = GetStore<T>(false);
            return store != null && store.Values.Remove(key);
        }

        /// <summary>Forgets every value. Keeps the dictionaries, so refilling them does not allocate again.</summary>
        public void Clear()
        {
            foreach (IStore store in stores.Values)
            {
                store.Clear();
            }
        }

        private Store<T> GetStore<T>(bool create)
        {
            if (stores.TryGetValue(typeof(T), out IStore store))
            {
                return (Store<T>)store;
            }

            if (!create)
            {
                return null;
            }

            var created = new Store<T>();
            stores.Add(typeof(T), created);
            return created;
        }

        private static void CheckKey<T>(BlackboardKey<T> key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
        }
    }
}
