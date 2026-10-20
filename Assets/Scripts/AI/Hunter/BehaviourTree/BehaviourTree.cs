// Owner: Systems Engineer (Hunter AI).
using System;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// A built tree with its context: <see cref="Tick"/> moves the tree's clock forward and ticks the root. The brain
    /// (H3) owns one per Hunter and decides how often to tick it (10 Hz in the prototype).
    /// </summary>
    public sealed class BehaviourTree
    {
        public BehaviourTree(Node root) : this(root, new BtContext())
        {
        }

        public BehaviourTree(Node root, BtContext context)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Node Root { get; }
        public BtContext Context { get; }
        public LocalBlackboard Blackboard => Context.Blackboard;

        public NodeStatus Tick(float deltaTime)
        {
            Context.Advance(deltaTime);
            return Root.Tick(Context);
        }

        /// <summary>Starts every branch over. Keeps the clock, the blackboard and any running cooldowns.</summary>
        public void Reset()
        {
            Root.Reset();
        }
    }
}
