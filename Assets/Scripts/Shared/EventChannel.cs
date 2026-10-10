// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using System;
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>
    /// Base for event channels that carry a value. A channel is a ScriptableObject asset both sides reference,
    /// so the sender and the listeners never reference each other, even across additive scenes.
    /// </summary>
    public abstract class EventChannel<T> : ScriptableObject
    {
        public event Action<T> OnRaised;

        public void Raise(T value)
        {
            OnRaised?.Invoke(value);
        }
    }
}
