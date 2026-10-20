// Owner: Systems Engineer (Hunter AI).
namespace Blackout.AI.Hunter
{
    /// <summary>
    /// Swaps its child's Success and Failure; Running passes through. Inverting a <see cref="Condition"/> gives a
    /// "not" guard, which a <see cref="Sequence"/> re-checks like any other condition.
    /// </summary>
    public sealed class Inverter : Decorator
    {
        public Inverter(string name, Node child) : base(name, child)
        {
        }

        public override bool IsGuard => Child.IsGuard;

        protected override NodeStatus OnTick(BtContext context)
        {
            switch (Child.Tick(context))
            {
                case NodeStatus.Success:
                    return NodeStatus.Failure;
                case NodeStatus.Failure:
                    return NodeStatus.Success;
                default:
                    return NodeStatus.Running;
            }
        }
    }
}
