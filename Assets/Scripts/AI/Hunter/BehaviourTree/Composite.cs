// Owner: Systems Engineer (Hunter AI).
using System;
using System.Collections.Generic;

namespace Blackout.AI.Hunter
{
    /// <summary>A node with children, ticked in order (<see cref="Selector"/>, <see cref="Sequence"/>).</summary>
    public abstract class Composite : Node
    {
        private readonly Node[] children;

        protected Composite(string name, Node[] children) : base(name)
        {
            if (children == null)
            {
                throw new ArgumentNullException(nameof(children));
            }

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null)
                {
                    throw new ArgumentException($"{Name}: child {i} is null.", nameof(children));
                }
            }

            // Copied, so the caller cannot change the tree after building it.
            this.children = (Node[])children.Clone();
        }

        /// <summary>In priority order. For the debug text (H4); do not cast and modify.</summary>
        public IReadOnlyList<Node> Children => children;

        protected Node[] ChildArray => children;

        protected override void OnReset()
        {
            for (int i = 0; i < children.Length; i++)
            {
                children[i].Reset();
            }
        }
    }
}
