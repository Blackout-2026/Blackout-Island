// Owner: Systems Engineer (Hunter AI).
using System.Collections.Generic;
using System.Text;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// Reads a tree's state for the debug text (docs/reference/systems-engineer.md section 16), from the
    /// <see cref="Node.LastStatus"/> each node keeps, so nothing is recorded while the tree runs.
    /// </summary>
    public static class BehaviourTreeDebug
    {
        /// <summary>
        /// Appends the running path below <paramref name="root"/>, for example "Investigate > Ambush": from each
        /// composite the child that ran this tick (a Selector's first child that did not fail, a Sequence's Running
        /// child), and through each decorator to its child. The root itself is left out. A branch that has just
        /// finished shows only its own name, because finishing resets its children. Returns false (and appends
        /// nothing) if no child of the root ran.
        /// </summary>
        public static bool AppendRunningPath(Node root, StringBuilder text)
        {
            bool any = false;
            Node node = root;
            while (true)
            {
                Node next = ActiveChild(node);
                if (next == null)
                {
                    return any;
                }

                if (any)
                {
                    text.Append(" > ");
                }

                text.Append(next.Name);
                any = true;
                node = next;
            }
        }

        private static Node ActiveChild(Node node)
        {
            switch (node)
            {
                case Selector selector:
                    return FirstWith(selector.Children, true);
                case Sequence sequence:
                    return FirstWith(sequence.Children, false);
                case Decorator decorator:
                    return Ran(decorator.Child.LastStatus, true) ? decorator.Child : null;
                default:
                    return null;
            }
        }

        // A Selector's children before the winner failed this tick, so the first that did not fail is the winner. A
        // Sequence's guards before its current child succeeded, so only the Running child is the current one.
        private static Node FirstWith(IReadOnlyList<Node> children, bool successCounts)
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (Ran(children[i].LastStatus, successCounts))
                {
                    return children[i];
                }
            }

            return null;
        }

        private static bool Ran(NodeStatus? status, bool successCounts)
        {
            return status == NodeStatus.Running || (successCounts && status == NodeStatus.Success);
        }
    }
}
