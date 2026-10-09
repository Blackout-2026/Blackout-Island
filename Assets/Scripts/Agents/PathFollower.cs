// Owner: Agent Controller.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Moves an agent to a destination along a NavMesh path: the mover half of "brains decide, the mover executes".
    /// It plans a corner array through <see cref="IPathProvider"/>, follows it with acceleration-limited velocity,
    /// slows down on arrival, turns at a limited rate and stays on the NavMesh. It re-plans when the destination
    /// moves far enough, on a timer, and as soon as the way to the next corner is cut (for example by a pushed
    /// barricade). Unchanged destinations are ignored, so AgentRoot may call SetDestination every think tick.
    /// Ports the prototype's AgentController (agents/movement.js) steps 1, 2, 4, 5 and 7.
    /// </summary>
    public class PathFollower : MonoBehaviour, IAgentMover
    {
        [SerializeField] private MovementSettings settings = new MovementSettings();

        private readonly PathTracker tracker = new PathTracker();
        private IPathProvider provider;

        private bool hasDestination;
        private Vector3 requestedDestination;
        private Vector3 plannedDestination;
        private MoveSpeed speedClass;
        private bool needsPlan;
        private float sinceLastPlan = float.MaxValue; // the first plan is never delayed
        private float repathTimer;
        private float blockedTimer;
        private Vector2 velocity; // x = world X, y = world Z
        private float yaw;

        public MovementSettings Settings => settings;

        /// <summary>Multiplies the top speed, for example 1.25 while the alert buff is on.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>True when the last plan found no complete path (or the agent is off the NavMesh). It retries on the repath timer.</summary>
        public bool HasFailed { get; private set; }

        /// <summary>Current velocity in world space, for the animator.</summary>
        public Vector3 Velocity => new Vector3(velocity.x, 0f, velocity.y);
        public float Speed => velocity.magnitude;

        public bool HasArrived => hasDestination && tracker.Arrived;
        public float RemainingDistance => tracker.RemainingDistance(transform.position);
        public Vector3[] CurrentPath => tracker.Corners;

        private void Awake()
        {
            Configure(new PathProvider());
        }

        /// <summary>Chooses the navigation source. Awake uses the NavMesh; tests plug in a fake.</summary>
        public void Configure(IPathProvider newProvider)
        {
            provider = newProvider;
            yaw = transform.eulerAngles.y;
            repathTimer = RandomRepathDelay();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void SetDestination(Vector3 destination, MoveSpeed speed)
        {
            if (speed == MoveSpeed.Stop)
            {
                Stop();
                return;
            }

            speedClass = speed;
            requestedDestination = destination;

            // Only a destination that moved far enough forces a new plan (prototype: 1.6 m running, 0.9 m otherwise).
            if (!hasDestination ||
                PathTracker.FlatDistance(destination, plannedDestination) > settings.ReplanDistanceFor(speed))
            {
                hasDestination = true;
                needsPlan = true;
            }
        }

        public void Stop()
        {
            hasDestination = false;
            needsPlan = false;
            HasFailed = false;
            tracker.Clear();
        }

        /// <summary>One movement step. Update calls it every frame; public so tests can drive time.</summary>
        public void Tick(float dt)
        {
            sinceLastPlan += dt;
            Vector3 position = transform.position;

            if (hasDestination)
            {
                CheckReplanTriggers(position, dt);
                if (needsPlan && sinceLastPlan >= settings.minReplanInterval)
                {
                    Plan(position);
                }
                tracker.Advance(position, settings);
            }

            Vector2 desired = Vector2.zero;
            if (hasDestination && tracker.HasPath && !tracker.Arrived)
            {
                Vector3 toTarget = tracker.Target - position;
                Vector2 direction = new Vector2(toTarget.x, toTarget.z);
                if (direction.sqrMagnitude > 1e-6f)
                {
                    float speed = settings.SpeedFor(speedClass) * SpeedMultiplier * tracker.SpeedFactor(position, settings);
                    desired = direction.normalized * speed;
                }
            }

            velocity = MotionMath.StepVelocity(velocity, desired, settings.acceleration, settings.decelerationMultiplier, dt);
            MoveAndFace(position, dt);
        }

        private void CheckReplanTriggers(Vector3 position, float dt)
        {
            repathTimer -= dt;
            if (repathTimer <= 0f)
            {
                needsPlan = true; // routine re-plan: also notices a barricade that has been moved
            }

            if (tracker.HasPath && !tracker.Arrived)
            {
                blockedTimer -= dt;
                if (blockedTimer <= 0f)
                {
                    blockedTimer = settings.blockedCheckInterval;
                    if (provider.IsBlocked(position, tracker.Target))
                    {
                        needsPlan = true; // the way to the next corner is cut
                    }
                }
            }
        }

        private void Plan(Vector3 position)
        {
            needsPlan = false;
            sinceLastPlan = 0f;
            repathTimer = RandomRepathDelay();
            plannedDestination = requestedDestination;

            if (provider.TryCalculatePath(position, requestedDestination, out Vector3[] corners))
            {
                tracker.SetPath(corners);
                HasFailed = false;
            }
            else
            {
                tracker.Clear();
                HasFailed = true;
            }
        }

        private void MoveAndFace(Vector3 position, float dt)
        {
            float speed = velocity.magnitude;
            if (speed > 1e-4f)
            {
                Vector3 next = position + new Vector3(velocity.x, 0f, velocity.y) * dt;

                // Clamp onto the NavMesh. No floor nearby means the step is refused, so an agent never leaves the mesh.
                if (provider.TrySnap(next, settings.snapDistance, out Vector3 snapped))
                {
                    float y = Mathf.Lerp(position.y, snapped.y, 1f - Mathf.Exp(-settings.groundDamping * dt));
                    position = new Vector3(snapped.x, y, snapped.z);
                    transform.position = position;
                }
                else
                {
                    velocity = Vector2.zero;
                    speed = 0f;
                }
            }

            if (speed > settings.minTurnSpeed)
            {
                yaw = MotionMath.TurnToward(yaw, MotionMath.YawOf(velocity), settings.turnRateDegrees, dt);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private float RandomRepathDelay()
        {
            return settings.repathInterval + Random.value * settings.repathJitter;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3[] corners = tracker.Corners;
            if (corners == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Vector3 previous = transform.position;
            for (int i = tracker.CornerIndex; i < corners.Length; i++)
            {
                Gizmos.DrawLine(previous, corners[i]);
                Gizmos.DrawSphere(corners[i], 0.1f);
                previous = corners[i];
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(corners[corners.Length - 1], settings.arriveDistance);
        }
    }
}
