// Owner: World Builder (Security Patrol AI).
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>
    /// S1 FSM structure. The agent host calls Initialize once and Tick at its think rate (normally 10 Hz).
    /// No Update, movement, physics, animation or weapon execution belongs here.
    /// S2/S3 will turn perception into guarded transition requests and fill in each state's behaviour.
    /// </summary>
    public sealed class SecurityPatrolBrain : MonoBehaviour, IAgentBrain
    {
        [Tooltip("Tuning for S2 perception and S3 state behaviour; S1 only establishes the FSM structure.")]
        [SerializeField] private SecurityPatrolConfig config;

        private IAgentContext context;
        private State[] states;
        private State current;
        private State pending;
        private Vector3 pendingTarget;

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
                    new PatrolState(),
                    new InvestigateState(),
                    new PursueState(),
                    new AttackState(),
                    new SearchState()
                };
            }

            current?.Exit();
            context = newContext;
            pending = null;
            pendingTarget = default;
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

        public AgentIntent Tick(float deltaTime)
        {
            if (current == null || context.Self == null ||
                (context.Health != null && !context.Health.IsAlive))
                return AgentIntent.Idle;

            if (pending != null)
            {
                current.Exit();
                current = pending;
                pending = null;
                current.Enter(pendingTarget);
                pendingTarget = default;
            }

            return current.Tick(deltaTime);
        }
    }
}
