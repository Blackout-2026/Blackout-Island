// Owner: Systems Engineer (Hunter AI).
using System;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// A yes/no question about the agent or the world: true is Success, false is Failure, never Running. A
    /// <see cref="Sequence"/> asks it again every tick while a later child is running.
    /// </summary>
    public sealed class Condition : Node
    {
        private readonly Func<BtContext, bool> predicate;

        public Condition(string name, Func<BtContext, bool> predicate) : base(name)
        {
            this.predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public override bool IsGuard => true;

        protected override NodeStatus OnTick(BtContext context)
        {
            return predicate(context) ? NodeStatus.Success : NodeStatus.Failure;
        }
    }
}
