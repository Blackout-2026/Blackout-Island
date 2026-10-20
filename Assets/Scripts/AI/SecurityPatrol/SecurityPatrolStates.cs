// Owner: World Builder (Security Patrol AI).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>One cached state object per brain. Enter/Exit own state-local data; Tick only returns intent.</summary>
    internal abstract class State
    {
        public SecurityPatrolState Id { get; }
        protected Vector3 Target { get; private set; }

        protected State(SecurityPatrolState id) => Id = id;

        public virtual void Enter(Vector3 target) => Target = target;
        public abstract AgentIntent Tick(float deltaTime);
        public virtual void Exit() => Target = default;
    }

    internal sealed class PatrolState : State
    {
        public PatrolState() : base(SecurityPatrolState.Patrol) { }

        // S3 supplies owned waypoints and pause/look behaviour. No route means stand still.
        public override AgentIntent Tick(float deltaTime) => AgentIntent.Idle;
    }

    internal sealed class InvestigateState : State
    {
        public InvestigateState() : base(SecurityPatrolState.Investigate) { }

        public override AgentIntent Tick(float deltaTime) => new AgentIntent
        {
            moveTarget = Target,
            moveSpeed = MoveSpeed.Walk
        };
    }

    internal sealed class PursueState : State
    {
        public PursueState() : base(SecurityPatrolState.Pursue) { }

        public override AgentIntent Tick(float deltaTime) => new AgentIntent
        {
            moveTarget = Target,
            moveSpeed = MoveSpeed.Run
        };
    }

    internal sealed class AttackState : State
    {
        public AttackState() : base(SecurityPatrolState.Attack) { }

        public override AgentIntent Tick(float deltaTime) => new AgentIntent
        {
            moveSpeed = MoveSpeed.Stop,
            lookTarget = Target,
            hasLookTarget = true,
            aim = true,
            // S1 has no verified LOS or reaction timer. S3 supplies those guards before enabling fire.
            fire = false
        };
    }

    internal sealed class SearchState : State
    {
        public SearchState() : base(SecurityPatrolState.Search) { }

        public override AgentIntent Tick(float deltaTime) => new AgentIntent
        {
            moveTarget = Target,
            moveSpeed = MoveSpeed.Walk,
            lookTarget = Target,
            hasLookTarget = true,
            aim = true
        };
    }
}
