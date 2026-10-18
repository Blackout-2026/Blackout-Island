// Owner: Agent Controller (Commander coordination tests).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Commander.Coordination.Tests
{
    // Test-only copy of the tiny mailbox in docs/viva/commander.md.
    // These are contract examples, not replacements for the other owners' brains.
    internal sealed class DirectiveHandling : IDirectiveReceiver
    {
        public bool HasOrder { get; private set; }
        public Directive Order { get; private set; }

        public void OnDirective(Directive directive)
        {
            switch (directive.type)
            {
                case DirectiveType.Converge:
                case DirectiveType.SearchZone:
                case DirectiveType.Regroup:
                    Order = directive;
                    HasOrder = true;
                    break;
            }
        }

        public void CompleteOrder() => HasOrder = false;
    }

    internal abstract class FakeDirectiveReceiver : IDirectiveReceiver
    {
        protected readonly DirectiveHandling orders = new DirectiveHandling();
        public bool HasOrder => orders.HasOrder;
        public Directive LastOrder => orders.Order;
        public float OrderElapsed { get; private set; }

        public virtual void OnDirective(Directive directive)
        {
            bool changed = !HasOrder || directive.type != LastOrder.type ||
                directive.position != LastOrder.position || directive.radius != LastOrder.radius;
            orders.OnDirective(directive);
            // Unknown enum values leave the mailbox untouched, including its elapsed time.
            bool accepted = directive.type == DirectiveType.Converge ||
                directive.type == DirectiveType.SearchZone || directive.type == DirectiveType.Regroup;
            if (accepted && changed) OrderElapsed = 0f;
        }

        public void CompleteOrder()
        {
            orders.CompleteOrder();
            OrderElapsed = 0f;
        }

        protected void AdvanceOrder(float deltaTime)
        {
            if (HasOrder) OrderElapsed += Mathf.Max(0f, deltaTime);
        }

        public abstract AgentIntent Tick(float deltaTime);

        protected static AgentIntent Move(Vector3 target, MoveSpeed speed)
        {
            return new AgentIntent { moveTarget = target, moveSpeed = speed };
        }
    }

    internal enum PatrolState { Patrol, Pursue, Search, Investigate, Attack }

    internal sealed class FakePatrolReceiver : FakeDirectiveReceiver
    {
        public PatrolState State { get; private set; } = PatrolState.Patrol;
        public bool LocalCombat { get; set; }

        public override AgentIntent Tick(float deltaTime)
        {
            if (LocalCombat)
            {
                State = PatrolState.Attack;
                return AgentIntent.Idle; // the fake has no actual perception or shooting executor
            }
            if (!HasOrder)
            {
                State = PatrolState.Patrol;
                return AgentIntent.Idle;
            }
            AdvanceOrder(deltaTime);
            switch (LastOrder.type)
            {
                case DirectiveType.Converge:
                    State = PatrolState.Pursue;
                    return Move(LastOrder.position, MoveSpeed.Run);
                case DirectiveType.SearchZone:
                    State = PatrolState.Search;
                    return Move(LastOrder.position, MoveSpeed.Walk);
                default:
                    State = PatrolState.Investigate;
                    return Move(LastOrder.position, MoveSpeed.Walk);
            }
        }
    }

    internal enum HunterBranch { Prowl, FollowOrders, Engage }
    internal enum HunterTask { None, Converge, Search, Regroup }

    internal sealed class FakeHunterReceiver : FakeDirectiveReceiver
    {
        public HunterBranch Branch { get; private set; } = HunterBranch.Prowl;
        public HunterTask Task { get; private set; }
        public bool LocalCombat { get; set; }

        public override AgentIntent Tick(float deltaTime)
        {
            if (LocalCombat)
            {
                Branch = HunterBranch.Engage;
                Task = HunterTask.None;
                return AgentIntent.Idle;
            }
            if (!HasOrder)
            {
                Branch = HunterBranch.Prowl;
                Task = HunterTask.None;
                return AgentIntent.Idle;
            }
            AdvanceOrder(deltaTime);
            Branch = HunterBranch.FollowOrders;
            switch (LastOrder.type)
            {
                case DirectiveType.Converge:
                    Task = HunterTask.Converge;
                    return Move(LastOrder.position, MoveSpeed.Run);
                case DirectiveType.SearchZone:
                    Task = HunterTask.Search;
                    // A deterministic point demonstrates use of the zone radius without a scene or NavMesh.
                    return Move(LastOrder.position + Vector3.right * (LastOrder.radius * 0.5f), MoveSpeed.Run);
                default:
                    Task = HunterTask.Regroup;
                    return Move(LastOrder.position, MoveSpeed.Walk);
            }
        }
    }

    internal enum GuardDecision { HoldPost, Attack, MoveToCover, Reposition }

    internal sealed class FakeGuardReceiver : FakeDirectiveReceiver
    {
        public Vector3 Post { get; private set; }
        // The local utility system owns this choice. OnDirective never overwrites it.
        public GuardDecision SelectedAction { get; set; } = GuardDecision.HoldPost;

        public override void OnDirective(Directive directive)
        {
            base.OnDirective(directive);
            if (HasOrder) Post = LastOrder.position;
        }

        public override AgentIntent Tick(float deltaTime)
        {
            // This fake only models post movement, not combat, cover or utility scoring.
            if (SelectedAction != GuardDecision.HoldPost) return AgentIntent.Idle;
            AdvanceOrder(deltaTime);
            // A completed order does not undo a Guard's new post.
            return Move(Post, MoveSpeed.Walk);
        }
    }

    /// <summary>Pure data harness: no MonoBehaviours, GameObjects, scenes or Commander assembly.</summary>
    internal sealed class SceneFreeDirectiveHarness
    {
        public readonly FakePatrolReceiver Patrol = new FakePatrolReceiver();
        public readonly FakeHunterReceiver Hunter = new FakeHunterReceiver();
        public readonly FakeGuardReceiver Guard = new FakeGuardReceiver();
        private readonly IDirectiveReceiver[] receivers;

        public AgentIntent PatrolIntent { get; private set; }
        public AgentIntent HunterIntent { get; private set; }
        public AgentIntent GuardIntent { get; private set; }

        public SceneFreeDirectiveHarness()
        {
            receivers = new IDirectiveReceiver[] { Patrol, Hunter, Guard };
        }

        public void Broadcast(Directive directive)
        {
            for (int i = 0; i < receivers.Length; i++) receivers[i].OnDirective(directive);
        }

        public void Tick(float deltaTime)
        {
            PatrolIntent = Patrol.Tick(deltaTime);
            HunterIntent = Hunter.Tick(deltaTime);
            GuardIntent = Guard.Tick(deltaTime);
        }
    }
}
