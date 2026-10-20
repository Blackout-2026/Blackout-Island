// Owner: Systems Engineer (Hunter AI).
using System;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// Stops its child running again too soon (docs/reference/systems-engineer.md section 13). When the child
    /// finishes (Success or Failure), the cooldown starts; until it ends this node returns Failure without ticking the
    /// child, so a <see cref="Selector"/> moves on to its next branch. Reset resets the child but keeps the cooldown,
    /// otherwise an interrupted branch could fire again straight away.
    /// </summary>
    public sealed class Cooldown : Decorator
    {
        public Cooldown(string name, float seconds, Node child) : base(name, child)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Cooldown must be 0 s or more.");
            }

            Seconds = seconds;
        }

        public float Seconds { get; }

        /// <summary>The tree time (<see cref="BtContext.Time"/>) at which the child may run again.</summary>
        public float ReadyAt { get; private set; } = float.NegativeInfinity;

        public bool IsCoolingDown(float time)
        {
            return time < ReadyAt;
        }

        protected override NodeStatus OnTick(BtContext context)
        {
            if (IsCoolingDown(context.Time))
            {
                return NodeStatus.Failure;
            }

            NodeStatus status = Child.Tick(context);
            if (status != NodeStatus.Running)
            {
                ReadyAt = context.Time + Seconds;
            }

            return status;
        }
    }
}
