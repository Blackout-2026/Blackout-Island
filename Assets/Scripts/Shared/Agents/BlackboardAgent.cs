// Owner: Agent Controller. Shared contract: extend it, never break it.
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>One registered squad member. Orders survive Commander death until another order replaces them.</summary>
    public sealed class BlackboardAgent
    {
        public string Name { get; }
        public Transform Transform { get; private set; }
        public Health Health { get; private set; }
        public IDirectiveReceiver Receiver { get; private set; }
        public bool IsAlive => Transform != null && Health != null && Health.IsAlive;
        public bool HasDirective { get; private set; }
        public Directive LastDirective { get; private set; }

        internal BlackboardAgent(string name, Transform transform, Health health, IDirectiveReceiver receiver)
        {
            Name = name;
            Update(transform, health, receiver);
        }

        internal void Update(Transform transform, Health health, IDirectiveReceiver receiver)
        {
            Transform = transform;
            Health = health;
            Receiver = receiver;
        }

        /// <summary>Retains the order even if this agent's owner has not yet implemented a receiver.</summary>
        public void Issue(Directive directive)
        {
            if (!IsAlive)
                return;
            LastDirective = directive;
            HasDirective = true;
            // Interface references do not use Unity's destroyed-object null comparison automatically.
            if (Receiver != null && !(Receiver is Object unityObject && unityObject == null))
                Receiver.OnDirective(directive);
        }
    }
}
