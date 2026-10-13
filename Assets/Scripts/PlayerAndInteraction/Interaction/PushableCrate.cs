// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Interaction
{
    /// <summary>
    /// The barricade crate: a Rigidbody the player pushes by walking into it. While pushed (and for 0.12 s after
    /// each contact) its horizontal velocity eases towards the push velocity at rate 6; then floor friction and
    /// drag stop it. It scrapes (an 11 m Crate noise every 0.45 s) while moving. A carving NavMeshObstacle on the
    /// same object re-carves when it stops, so agents path around it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PushableCrate : MonoBehaviour
    {
        private const float PushHoldTime = 0.12f;
        private const float PushDamping = 6f;
        private const float MovingSpeed = 0.05f;
        private const float ScrapeInterval = 0.45f;

        [SerializeField] private NoiseEventChannel noiseChannel;
        [SerializeField, Min(0f)] private float scrapeNoise = 11f;

        private Rigidbody body;
        private Vector3 pushVelocity;
        private float pushTimeLeft;
        private float scrapeTimer;
        private GameObject lastPusher;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        /// <summary>Called by the player while walking into the crate.</summary>
        public void Push(Vector3 direction, float pusherSpeed, GameObject pusher)
        {
            pushVelocity = CrateMath.PushVelocity(direction, pusherSpeed);
            pushTimeLeft = PushHoldTime;
            lastPusher = pusher;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 velocity = body.linearVelocity;
            if (pushTimeLeft > 0f)
            {
                pushTimeLeft -= dt;
                float blend = 1f - Mathf.Exp(-PushDamping * dt);
                velocity.x = Mathf.Lerp(velocity.x, pushVelocity.x, blend);
                velocity.z = Mathf.Lerp(velocity.z, pushVelocity.z, blend);
                body.linearVelocity = velocity;
            }

            bool moving = new Vector2(velocity.x, velocity.z).sqrMagnitude > MovingSpeed * MovingSpeed;
            if (!moving)
            {
                scrapeTimer = 0f;
                return;
            }

            scrapeTimer -= dt;
            if (scrapeTimer <= 0f)
            {
                scrapeTimer = ScrapeInterval;
                if (noiseChannel != null && scrapeNoise > 0f)
                {
                    noiseChannel.Raise(new NoiseEvent(transform.position, scrapeNoise, NoiseType.Crate, lastPusher));
                }
            }
        }
    }
}
