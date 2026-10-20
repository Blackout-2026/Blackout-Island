// Owner: Systems Engineer (Hunter AI).
namespace Blackout.AI.Hunter
{
    /// <summary>
    /// "Do these in order": ticks its children one after another and returns Success when the last one succeeds, or
    /// Failure as soon as one fails. It keeps its place while a child is Running. As in the prototype
    /// (docs/reference/systems-engineer.md section 13), the guards before the running child (Conditions, and
    /// Inverters of Conditions) are checked again every tick, so a running action stops as soon as its premise is
    /// false. It starts over from the first child after it succeeds or fails.
    /// </summary>
    public sealed class Sequence : Composite
    {
        // The child that runs next; the ones before it have succeeded.
        private int current;

        public Sequence(string name, params Node[] children) : base(name, children)
        {
        }

        protected override NodeStatus OnTick(BtContext context)
        {
            Node[] children = ChildArray;
            for (int i = 0; i < current; i++)
            {
                if (children[i].IsGuard && children[i].Tick(context) == NodeStatus.Failure)
                {
                    Reset();
                    return NodeStatus.Failure;
                }
            }

            while (current < children.Length)
            {
                NodeStatus status = children[current].Tick(context);
                if (status == NodeStatus.Running)
                {
                    return NodeStatus.Running;
                }

                if (status == NodeStatus.Failure)
                {
                    Reset();
                    return NodeStatus.Failure;
                }

                current++;
            }

            Reset();
            return NodeStatus.Success;
        }

        protected override void OnReset()
        {
            current = 0;
            base.OnReset();
        }
    }
}
