// Owner: Systems Engineer (Hunter AI).
using System;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// Base of every behaviour tree node (docs/reference/systems-engineer.md section 13). Subclasses implement
    /// <see cref="OnTick"/>, and <see cref="OnReset"/> if they keep state between ticks (a Sequence's place, an
    /// action's progress). A parent resets a child when it interrupts it or when its branch starts over.
    /// </summary>
    public abstract class Node
    {
        protected Node(string name)
        {
            Name = string.IsNullOrEmpty(name) ? GetType().Name : name;
        }

        /// <summary>Shown in the debug text (H4), for example "ReactToFootsteps".</summary>
        public string Name { get; }

        /// <summary>The status of the last tick, or null if the node has not been ticked since it was last reset.</summary>
        public NodeStatus? LastStatus { get; private set; }

        public NodeStatus Tick(BtContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            NodeStatus status = OnTick(context);
            LastStatus = status;
            return status;
        }

        /// <summary>Forgets any progress, so the next tick starts the node from the beginning.</summary>
        public void Reset()
        {
            LastStatus = null;
            OnReset();
        }

        protected abstract NodeStatus OnTick(BtContext context);

        protected virtual void OnReset()
        {
        }
    }
}
