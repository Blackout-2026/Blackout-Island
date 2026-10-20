// Owner: World Builder (Security Patrol AI).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>One cached state object per brain. Enter/Exit own state-local data; Tick only returns intent.</summary>
    internal abstract class State
    {
        public SecurityPatrolState Id { get; }
        public float TimeInState { get; private set; }
        protected SecurityPatrolBrain Brain { get; }
        protected Vector3 Target { get; private set; }

        protected State(SecurityPatrolState id, SecurityPatrolBrain brain)
        {
            Id = id;
            Brain = brain;
        }

        public virtual void Enter(Vector3 target)
        {
            Target = target;
            TimeInState = 0f;
        }

        public virtual void Advance(float deltaTime) => TimeInState += deltaTime;
        public abstract AgentIntent Tick(float deltaTime);
        public virtual void Exit() => Target = default;
    }

    internal sealed class PatrolState : State
    {
        private int routeIndex;
        private float pauseLeft;

        public PatrolState(SecurityPatrolBrain brain) : base(SecurityPatrolState.Patrol, brain) { }

        public override void Enter(Vector3 target)
        {
            base.Enter(target);
            pauseLeft = 0f;
        }

        public override void Advance(float deltaTime)
        {
            base.Advance(deltaTime);
            Marker[] route = Brain.Route;
            if (route.Length == 0) return;
            routeIndex %= route.Length;

            if (pauseLeft > 0f)
            {
                pauseLeft = Mathf.Max(0f, pauseLeft - deltaTime);
                if (pauseLeft == 0f) routeIndex = (routeIndex + 1) % route.Length;
            }
            else if (route[routeIndex] != null && Brain.IsNear(route[routeIndex].transform.position, 0.9f))
                pauseLeft = 2.2f;
        }

        public override AgentIntent Tick(float deltaTime)
        {
            Marker[] route = Brain.Route;
            if (route.Length == 0) return AgentIntent.Idle;
            routeIndex %= route.Length;
            if (route[routeIndex] == null) return AgentIntent.Idle;
            if (pauseLeft > 0f) return Brain.LookAround(TimeInState + routeIndex, false);
            return new AgentIntent { moveTarget = route[routeIndex].transform.position, moveSpeed = MoveSpeed.Walk };
        }

        public override void Exit()
        {
            base.Exit();
            pauseLeft = 0f;
        }

        public void ResetRoute() => routeIndex = 0;
    }

    internal sealed class InvestigateState : State
    {
        private float lookingTime;
        public bool IsComplete => lookingTime >= 3.5f;

        public InvestigateState(SecurityPatrolBrain brain) : base(SecurityPatrolState.Investigate, brain) { }

        public override void Enter(Vector3 target)
        {
            base.Enter(target);
            lookingTime = 0f;
        }

        public override void Advance(float deltaTime)
        {
            base.Advance(deltaTime);
            lookingTime = Brain.IsNear(Target, 1.5f) ? lookingTime + deltaTime : 0f;
        }

        public override AgentIntent Tick(float deltaTime) => Brain.IsNear(Target, 1.5f)
            ? Brain.LookAround(lookingTime, false) : new AgentIntent
        {
            moveTarget = Target,
            moveSpeed = MoveSpeed.Walk
        };
    }

    internal sealed class PursueState : State
    {
        public PursueState(SecurityPatrolBrain brain) : base(SecurityPatrolState.Pursue, brain) { }

        public override AgentIntent Tick(float deltaTime) => new AgentIntent
        {
            moveTarget = Target,
            moveSpeed = MoveSpeed.Run
        };
    }

    internal sealed class AttackState : State
    {
        public AttackState(SecurityPatrolBrain brain) : base(SecurityPatrolState.Attack, brain) { }

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
        public SearchState(SecurityPatrolBrain brain) : base(SecurityPatrolState.Search, brain) { }

        public override AgentIntent Tick(float deltaTime) => Brain.IsNear(Target, 1.2f)
            ? Brain.LookAround(TimeInState, true) : new AgentIntent
        {
            moveTarget = Target,
            moveSpeed = MoveSpeed.Walk,
            lookTarget = Target,
            hasLookTarget = true,
            aim = true
        };
    }
}
