// Owner: Systems Engineer (Hunter AI).
using System;

namespace Blackout.AI.Hunter
{
    /// <summary>A node that wraps one child and changes how it runs (<see cref="Inverter"/>, <see cref="Cooldown"/>).</summary>
    public abstract class Decorator : Node
    {
        protected Decorator(string name, Node child) : base(name)
        {
            Child = child ?? throw new ArgumentNullException(nameof(child));
        }

        public Node Child { get; }

        protected override void OnReset()
        {
            Child.Reset();
        }
    }
}
