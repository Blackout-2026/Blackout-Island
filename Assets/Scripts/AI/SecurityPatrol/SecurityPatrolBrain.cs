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
    public sealed class SecurityPatrolBrain : MonoBehaviour, IAgentBrain
    {
        [Tooltip("Perception, state and executor reference tuning for this patrol.")]
        [SerializeField] private SecurityPatrolConfig config;

        private IAgentContext context;
        private State[] states;
        private State current;
        private State pending;
        private Vector3 pendingTarget;
        private string markerOwner;
        private IReadOnlyList<Marker> sourceMarkers;
        private Marker[] route = Array.Empty<Marker>();
        private int sourceCount;

        internal Vector3 Position => context.Self.position;
        internal Marker[] Route => route;
        internal float SearchTime => config != null ? config.searchTime : 14f;

        public SecurityPatrolConfig Config => config;
        public bool IsInitialized => current != null;
        public SecurityPatrolState? CurrentState => current == null ? (SecurityPatrolState?)null : current.Id;

        public void Initialize(IAgentContext newContext)
        {
            if (newContext == null)
                throw new ArgumentNullException(nameof(newContext));
            if (newContext.Self == null)
                throw new ArgumentException("A patrol brain needs its own Transform.", nameof(newContext));

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

            current?.Exit();
            context = newContext;
            pending = null;
            pendingTarget = default;
            markerOwner = context.Self.name;
            sourceMarkers = null;
            sourceCount = 0;
            route = Array.Empty<Marker>();
            ((PatrolState)states[(int)SecurityPatrolState.Patrol]).ResetRoute();
            RefreshRoute();
            current = states[(int)SecurityPatrolState.Patrol];
            current.Enter(context.Self.position);
        }

        /// <summary>
        /// Queue one legal transition for the next Tick. The first accepted request wins until that tick;
        /// callers must check conditions in priority order. S1 exposes this seam for structural tests.
        /// A target is a world position, copied by value; requesting a state never moves the agent.
        /// </summary>
        public bool TryRequestTransition(SecurityPatrolTrigger trigger, Vector3 target)
        {
            if (current == null || pending != null || context.Self == null)
                return false;

            if (!SecurityPatrolTransitions.TryGetNext(current.Id, trigger, out SecurityPatrolState next))
                return false;

            pending = states[(int)next];
            pendingTarget = target;
            return true;
        }

        // Runtime sensing is connected in the integration chunk. Explicit input keeps decisions testable.
        public AgentIntent Tick(float deltaTime) => Tick(deltaTime, default);

        public AgentIntent Tick(float deltaTime, in PatrolPerceptionInput input)
        {
            if (current == null || context.Self == null ||
                (context.Health != null && !context.Health.IsAlive))
                return AgentIntent.Idle;

            if (!(deltaTime >= 0f) || float.IsInfinity(deltaTime))
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            RefreshRoute();
            bool hadRequest = pending != null;
            ApplyPendingTransition();
            current.Advance(deltaTime);
            if (!hadRequest)
                EvaluateTransitions(input);
            ApplyPendingTransition();

            return current.Tick(deltaTime);
        }

        private void EvaluateTransitions(in PatrolPerceptionInput input)
        {
            if (current.Id == SecurityPatrolState.Patrol && input.heardNoise)
                TryRequestTransition(SecurityPatrolTrigger.HeardNoise, input.noisePosition);
            else if (current is InvestigateState investigate && investigate.IsComplete)
                TryRequestTransition(SecurityPatrolTrigger.InvestigationComplete, Position);
            else if (current.Id == SecurityPatrolState.Search && current.TimeInState >= SearchTime)
                TryRequestTransition(SecurityPatrolTrigger.SearchComplete, Position);
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
