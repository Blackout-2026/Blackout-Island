// Owner: Systems Engineer (Hunter AI).
namespace Blackout.AI.Hunter
{
    /// <summary>
    /// "Try these in priority order": returns the first child result that is not Failure, or Failure if every child
    /// fails. Reactive, as in the prototype (docs/reference/systems-engineer.md section 13): every tick starts again
    /// from the first child, so a higher-priority branch interrupts a lower one that is still Running, and the
    /// interrupted child is reset.
    /// </summary>
    public sealed class Selector : Composite
    {
        // The child that returned Running last tick, or -1.
        private int running = -1;

        public Selector(string name, params Node[] children) : base(name, children)
        {
        }

        protected override NodeStatus OnTick(BtContext context)
        {
            Node[] children = ChildArray;
            for (int i = 0; i < children.Length; i++)
            {
                NodeStatus status = children[i].Tick(context);
                if (status == NodeStatus.Failure)
                {
                    continue;
                }

                if (running >= 0 && running != i)
                {
                    children[running].Reset();
                }

                running = status == NodeStatus.Running ? i : -1;
                return status;
            }

            if (running >= 0)
            {
                children[running].Reset();
                running = -1;
            }

            return NodeStatus.Failure;
        }

        protected override void OnReset()
        {
            running = -1;
            base.OnReset();
        }
    }
}
