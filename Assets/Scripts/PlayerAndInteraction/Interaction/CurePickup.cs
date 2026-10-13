// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace Blackout.Interaction
{
    /// <summary>
    /// The cure on its pedestal: walking into it takes it (a trigger, as in the prototype), raises CureTaken and
    /// sets off an 18 m alarm noise. Until then the case turns slowly and bobs.
    /// </summary>
    public class CurePickup : MonoBehaviour
    {
        // Prototype idle motion: 0.6 rad/s spin, ±0.02 m bob at 1.6 rad/s.
        private const float SpinDegreesPerSecond = 34.4f;
        private const float BobHeight = 0.02f;
        private const float BobRate = 1.6f;
        private const string PlayerTag = "Player";

        [SerializeField] private VoidEventChannel cureTaken;
        [SerializeField] private NoiseEventChannel noiseChannel;
        [SerializeField, Min(0f)] private float alarmRadius = 18f;
        [Tooltip("The case model: spins and bobs, hidden once taken.")]
        [SerializeField] private Transform visual;

        [Header("Audio hooks")]
        public UnityEvent onTaken = new UnityEvent();

        private Vector3 visualRest;

        public bool IsTaken { get; private set; }

        private void Awake()
        {
            if (visual != null)
            {
                visualRest = visual.localPosition;
            }
        }

        private void Update()
        {
            if (IsTaken || visual == null)
            {
                return;
            }

            visual.Rotate(0f, SpinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
            visual.localPosition = visualRest + Vector3.up * (Mathf.Sin(Time.time * BobRate) * BobHeight);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsTaken && other.CompareTag(PlayerTag))
            {
                Take(other.gameObject);
            }
        }

        public void Take(GameObject by)
        {
            if (IsTaken)
            {
                return;
            }

            IsTaken = true;
            if (visual != null)
            {
                visual.gameObject.SetActive(false);
            }

            if (cureTaken != null)
            {
                cureTaken.Raise();
            }

            if (noiseChannel != null)
            {
                noiseChannel.Raise(new NoiseEvent(transform.position, alarmRadius, NoiseType.Alarm, by));
            }

            onTaken.Invoke();
        }
    }
}
