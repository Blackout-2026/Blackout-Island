// Owner: World Builder (Security Patrol AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>
    /// The agent host calls Initialize once and Tick at its think rate (normally 10 Hz).
    /// States return intents; movement, animation and weapon execution belong to the agent host.
    /// </summary>
    public sealed class SecurityPatrolBrain : MonoBehaviour, IAgentBrain, IAlertReceiver
    {
        [Tooltip("Perception, state and executor reference tuning for this patrol.")]
        [SerializeField] private SecurityPatrolConfig config;
        [SerializeField] private NoiseEventChannel noiseChannel;
        [SerializeField] private VoidEventChannel playerRespawned;
        [Tooltip("World geometry and doors; exclude Enemy, Player and trigger volumes.")]
        [SerializeField] private LayerMask geometryMask =
            (1 << PhysicsLayers.Default) | (1 << PhysicsLayers.Level) | (1 << PhysicsLayers.Interactable);

        private IAgentContext context;
        private State[] states;
        private State current;
        private State pending;
        private Vector3 pendingTarget;
        private string markerOwner;
        private IReadOnlyList<Marker> sourceMarkers;
        private Marker[] route = Array.Empty<Marker>();
        private int sourceCount;
        private bool hasSeenPlayer;
        private Vector3 lastSeenPosition;
        private float timeSinceSeen;
        private bool canFire;
        private PatrolSensing senses;
        private IPerceptionRaycast sightRaycast;
        private Health boundHealth;
        private VoidEventChannel boundRespawn;
        private float rangeMultiplier = 1f;

        internal Vector3 Position => context.Self.position;
        internal Marker[] Route => route;
        private bool IsAlert => context.Blackboard != null &&
            (context.Blackboard.cureTaken || context.Blackboard.alertLevel > 0);
        internal float SearchTime => IsAlert ? (config != null ? config.alertSearchTime : 30f) :
            (config != null ? config.searchTime : 14f);
        internal float AimHeight => config != null ? config.aimHeight : 1.8f * 0.62f;
        internal bool CanFire => canFire;
        private float AttackRange => (config != null ? config.attackRange : 22f) * rangeMultiplier;
        private Vector3 LastSeenPosition => hasSeenPlayer ? lastSeenPosition : current.Target;
        private bool CanThink => current != null && isActiveAndEnabled && context.Self != null &&
            (context.Health == null || context.Health.IsAlive);

        public SecurityPatrolConfig Config => config;
        public bool IsInitialized => current != null;
        public float VisionRangeMultiplier => rangeMultiplier;
        public SecurityPatrolState? CurrentState => current == null ? (SecurityPatrolState?)null : current.Id;

        public void Initialize(IAgentContext newContext) =>
            Initialize(newContext, new PhysicsPerceptionRaycast(geometryMask));

        /// <summary>Raycast injection keeps the full sensing-to-state path testable without scene geometry.</summary>
        public void Initialize(IAgentContext newContext, IPerceptionRaycast raycast)
        {
            if (newContext == null)
                throw new ArgumentNullException(nameof(newContext));
            if (newContext.Self == null)
                throw new ArgumentException("A patrol brain needs its own Transform.", nameof(newContext));
            if (raycast == null)
                throw new ArgumentNullException(nameof(raycast));

            // Enum order matches this array. Allocate only on first initialization, never on a state change.
            if (states == null)
            {
                states = new State[]
                {
                    new PatrolState(this),
                    new InvestigateState(this),
                    new PursueState(this),
                    new AttackState(this),
                    new SearchState(this)
                };
            }

            UnbindSensing();
            current?.Exit();
            context = newContext;
            sightRaycast = raycast;
            rangeMultiplier = 1f;
            RefreshAlertRange();
            pending = null;
            pendingTarget = default;
            hasSeenPlayer = false;
            lastSeenPosition = default;
            timeSinceSeen = 0f;
            canFire = false;
            markerOwner = context.Self.name;
            sourceMarkers = null;
            sourceCount = 0;
            route = Array.Empty<Marker>();
            ((PatrolState)states[(int)SecurityPatrolState.Patrol]).ResetRoute();
            RefreshRoute();
            current = states[(int)SecurityPatrolState.Patrol];
            current.Enter(context.Self.position);
            BindSensing();
        }

        /// <summary>
        /// Queue one legal transition for the next Tick. The first accepted request wins until that tick;
        /// callers must check conditions in priority order. S1 exposes this seam for structural tests.
        /// A target is a world position, copied by value; requesting a state never moves the agent.
        /// </summary>
        public bool TryRequestTransition(SecurityPatrolTrigger trigger, Vector3 target)
        {
            if (!CanThink || pending != null)
                return false;

            if (!SecurityPatrolTransitions.TryGetNext(current.Id, trigger, out SecurityPatrolState next))
                return false;

            pending = states[(int)next];
            pendingTarget = target;
            return true;
        }

        public AgentIntent Tick(float deltaTime)
        {
            if (!CanThink) return AgentIntent.Idle;
            if (!(deltaTime >= 0f) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            BindSensing();
            RefreshAlertRange();
            PatrolPerceptionInput input = senses.Read(rangeMultiplier);
            if (senses.PlayerChanged || senses.PlayerIsDead) ResetPlayerMemory();
            if (senses.PlayerIsDead) input = default;
            return Tick(deltaTime, input);
        }

        public AgentIntent Tick(float deltaTime, in PatrolPerceptionInput input)
        {
            if (!CanThink)
                return AgentIntent.Idle;

            if (!(deltaTime >= 0f) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            RefreshAlertRange();
            RefreshRoute();
            if (input.canSeePlayer)
            {
                hasSeenPlayer = true;
                lastSeenPosition = input.playerPosition;
                timeSinceSeen = 0f;
                if (context.Blackboard != null)
                {
                    context.Blackboard.lastKnownPlayerPos = input.playerPosition;
                    context.Blackboard.lastKnownPlayerTime = Time.time;
                }
            }
            else
                timeSinceSeen += deltaTime;

            bool hadRequest = pending != null;
            ApplyPendingTransition();
            if (input.canSeePlayer &&
                (current.Id == SecurityPatrolState.Pursue || current.Id == SecurityPatrolState.Attack))
                current.Retarget(input.playerPosition);
            current.Advance(deltaTime);
            if (!hadRequest)
                EvaluateTransitions(input);
            ApplyPendingTransition();

            // fire requests engagement; the separate shooter owns reaction time and random aim error.
            // Clear fire immediately on lost sight, even during Attack's one-second transition grace.
            canFire = current.Id == SecurityPatrolState.Attack && input.canSeePlayer &&
                IsNear(input.playerPosition, AttackRange);
            return current.Tick(deltaTime);
        }

        private void EvaluateTransitions(in PatrolPerceptionInput input)
        {
            // Confirmed sight outranks noise, investigation completion and a search timeout.
            if (input.canSeePlayer && current.Id != SecurityPatrolState.Attack)
            {
                if (IsNear(input.playerPosition, AttackRange))
                    TryRequestTransition(SecurityPatrolTrigger.CanAttack, input.playerPosition);
                else if (current.Id != SecurityPatrolState.Pursue)
                    TryRequestTransition(SecurityPatrolTrigger.DetectedPlayer, input.playerPosition);
                return;
            }

            if (current.Id == SecurityPatrolState.Attack)
            {
                if ((!input.canSeePlayer && timeSinceSeen >= 1f) ||
                    (input.canSeePlayer && !IsNear(input.playerPosition, AttackRange * 1.15f)))
                    TryRequestTransition(SecurityPatrolTrigger.CannotAttack, LastSeenPosition);
            }
            else if (current.Id == SecurityPatrolState.Pursue && !input.canSeePlayer)
            {
                if (IsNear(LastSeenPosition, 1.5f) || (timeSinceSeen > 2.5f && current.TimeInState >= 4f))
                    TryRequestTransition(SecurityPatrolTrigger.LostPlayer, LastSeenPosition);
            }
            else if (current.Id == SecurityPatrolState.Patrol && input.heardNoise)
                TryRequestTransition(SecurityPatrolTrigger.HeardNoise, input.noisePosition);
            else if (current is InvestigateState investigate && investigate.IsComplete)
                TryRequestTransition(IsAlert ? SecurityPatrolTrigger.InvestigationAlerted :
                    SecurityPatrolTrigger.InvestigationComplete, IsAlert ? current.Target : Position);
            else if (current.Id == SecurityPatrolState.Search && current.TimeInState >= SearchTime)
                TryRequestTransition(SecurityPatrolTrigger.SearchComplete, Position);
        }

        /// <summary>Absolute multiplier shared with the executors; never multiply an already-buffed range.</summary>
        public void OnAlert(AgentAlert alert) => rangeMultiplier = alert.visionMultiplier;

        private void RefreshAlertRange()
        {
            Blackboard board = context.Blackboard;
            if (board == null) return;
            if (board.HasAlertBuff) rangeMultiplier = board.Alert.visionMultiplier;
            else if (board.generatorDisabled || board.cureTaken) rangeMultiplier = 1.4f;
        }

        private void OnEnable() => BindSensing();

        private void OnDisable()
        {
            UnbindSensing();
            ResetPlayerMemory();
        }

        private void OnDestroy() => UnbindSensing();

        private void BindSensing()
        {
            if (senses != null || !CanThink) return;
            senses = new PatrolSensing(context, config, sightRaycast, noiseChannel);
            boundHealth = context.Health;
            if (boundHealth != null) boundHealth.OnDied += HandleOwnDeath;
            boundRespawn = playerRespawned;
            if (boundRespawn != null) boundRespawn.OnRaised += ResetPlayerMemory;
        }

        private void UnbindSensing()
        {
            senses?.Dispose();
            senses = null;
            if (boundHealth != null) boundHealth.OnDied -= HandleOwnDeath;
            if (boundRespawn != null) boundRespawn.OnRaised -= ResetPlayerMemory;
            boundHealth = null;
            boundRespawn = null;
        }

        private void HandleOwnDeath()
        {
            UnbindSensing();
            ResetPlayerMemory();
        }

        private void ResetPlayerMemory()
        {
            senses?.Forget();
            pending = null;
            pendingTarget = default;
            hasSeenPlayer = false;
            lastSeenPosition = default;
            timeSinceSeen = 0f;
            canFire = false;
            if (current == null || context.Self == null) return;
            current.Exit();
            current = states[(int)SecurityPatrolState.Patrol];
            current.Enter(Position);
        }

        private void ApplyPendingTransition()
        {
            if (pending != null)
            {
                current.Exit();
                current = pending;
                pending = null;
                current.Enter(pendingTarget);
                pendingTarget = default;
            }
        }

        private void RefreshRoute()
        {
            IReadOnlyList<Marker> markers = context.GetMarkers(MarkerType.Waypoint, markerOwner);
            int count = markers != null ? markers.Count : 0;
            if (ReferenceEquals(markers, sourceMarkers) && count == sourceCount)
                return;

            sourceMarkers = markers;
            sourceCount = count;
            int ownedCount = 0;
            for (int i = 0; i < count; i++)
                if (IsOwnedWaypoint(markers[i])) ownedCount++;

            route = ownedCount == 0 ? Array.Empty<Marker>() : new Marker[ownedCount];
            int destination = 0;
            for (int i = 0; i < count; i++)
                if (IsOwnedWaypoint(markers[i])) route[destination++] = markers[i];
            Array.Sort(route, CompareMarkers);
            ((PatrolState)states[(int)SecurityPatrolState.Patrol]).ResetRoute();
        }

        private bool IsOwnedWaypoint(Marker marker) => marker != null &&
            marker.Type == MarkerType.Waypoint && marker.Owner == markerOwner;

        private static int CompareMarkers(Marker a, Marker b) => string.CompareOrdinal(a.Id, b.Id);

        internal bool IsNear(Vector3 target, float radius) =>
            (Position - target).sqrMagnitude <= radius * radius;

        internal AgentIntent LookAround(float time, bool aim)
        {
            float phase = time * 0.7f;
            return new AgentIntent
            {
                moveSpeed = MoveSpeed.Stop,
                hasLookTarget = true,
                lookTarget = Position + new Vector3(Mathf.Sin(phase) * 4f, 1.5f, Mathf.Cos(phase) * 4f),
                aim = aim
            };
        }
    }
}
