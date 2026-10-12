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
    public class PathFollower : MonoBehaviour, IAgentMover, IAgentFacing, IAgentVelocitySource
    {
        [SerializeField] private MovementSettings settings = new MovementSettings();

        private readonly PathTracker tracker = new PathTracker();
        private IPathProvider provider;
        private ISeparationSource separationSource;

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
        private bool hasLookTarget;
        private Vector3 lookTarget;
        private bool aiming;
        private float stuckTimer;
        private Vector3 stuckReferencePosition;

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

        /// <summary>A4: how many times this mover has nudged itself free after making no progress. For the debug overlay.</summary>
        public int StuckCount { get; private set; }

        private void Awake()
        {
            Configure(new PathProvider(), new AgentRegistrySeparationSource());
        }

        /// <summary>
        /// Chooses the navigation source and (A4) the separation source. Awake uses the NavMesh and the live
        /// AgentRegistry; tests plug in fakes. separationSource may be null, meaning this mover never separates.
        /// </summary>
        public void Configure(IPathProvider newProvider, ISeparationSource newSeparationSource = null)
        {
            provider = newProvider;
            separationSource = newSeparationSource;
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

        public void SetFacing(bool hasLook, Vector3 look, bool isAiming)
        {
            hasLookTarget = hasLook;
            lookTarget = look;
            aiming = isAiming;
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

            // A4: applies even while standing still, so an idle agent still gets nudged out of a crowd.
            if (separationSource != null)
            {
                desired += separationSource.ComputeSeparation(position, transform);
            }

            bool stuckNudge = UpdateStuckDetection(position, desired, dt);

            velocity = MotionMath.StepVelocity(velocity, desired, settings.acceleration, settings.decelerationMultiplier, dt);
            if (stuckNudge)
            {
                velocity += new Vector2(Random.Range(-settings.stuckNudgeSpeed, settings.stuckNudgeSpeed), Random.Range(-settings.stuckNudgeSpeed, settings.stuckNudgeSpeed));
            }

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

            // Face the look target if there is one (even when standing still), otherwise the direction of travel.
            bool turn = false;
            float targetYaw = yaw;
            Vector2 toLook = new Vector2(lookTarget.x - position.x, lookTarget.z - position.z);
            if (hasLookTarget && toLook.sqrMagnitude > 1e-4f)
            {
                targetYaw = MotionMath.YawOf(toLook);
                turn = true;
            }
            else if (speed > settings.minTurnSpeed)
            {
                targetYaw = MotionMath.YawOf(velocity);
                turn = true;
            }

            if (turn)
            {
                float rate = settings.turnRateDegrees * (aiming ? settings.aimTurnMultiplier : 1f);
                yaw = MotionMath.TurnToward(yaw, targetYaw, rate, dt);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private float RandomRepathDelay()
        {
            return settings.repathInterval + Random.value * settings.repathJitter;
        }

        /// <summary>
        /// A4, docs/reference/agent-controller.md section 3.2. While the agent wants to move faster than
        /// stuckTryingSpeedThreshold: every stuckCheckInterval seconds, if it covered less than stuckMinProgress
        /// since the last check, it is stuck — clears the path and forces an immediate replan, counts it, and
        /// tells the caller to apply a random nudge. The timer and reference position reset the moment it stops
        /// trying to move, so stopping cleanly is never mistaken for being stuck.
        /// </summary>
        private bool UpdateStuckDetection(Vector3 position, Vector2 desired, float dt)
        {
            if (desired.magnitude <= settings.stuckTryingSpeedThreshold)
            {
                stuckTimer = 0f;
                stuckReferencePosition = position;
                return false;
            }

            stuckTimer += dt;
            if (stuckTimer < settings.stuckCheckInterval)
            {
                return false;
            }

            stuckTimer = 0f;
            float moved = Vector3.Distance(position, stuckReferencePosition);
            stuckReferencePosition = position;
            if (moved >= settings.stuckMinProgress)
            {
                return false;
            }

            StuckCount++;
            needsPlan = true; // "clear the path": the corners go, the destination itself is untouched
            repathTimer = 0f;
            tracker.Clear();
            return true;
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
