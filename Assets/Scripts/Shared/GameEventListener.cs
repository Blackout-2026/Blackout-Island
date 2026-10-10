// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using UnityEngine;
using UnityEngine.Events;

namespace Blackout.Shared
{
    /// <summary>
    /// Wires a <see cref="VoidEventChannel"/> to a UnityEvent in the Inspector, so designers can react to an event
    /// (play a sound, enable an object) without writing code. Listens only while enabled.
    /// </summary>
    public class GameEventListener : MonoBehaviour
    {
        [SerializeField] private VoidEventChannel channel;
        [SerializeField] private UnityEvent response = new UnityEvent();

        public UnityEvent Response => response;

        private void OnEnable()
        {
            if (channel != null)
            {
                channel.OnRaised += OnEventRaised;
            }
        }

        private void OnDisable()
        {
            if (channel != null)
            {
                channel.OnRaised -= OnEventRaised;
            }
        }

        private void OnEventRaised()
        {
            response.Invoke();
        }
    }
}
