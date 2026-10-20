// Owner: Systems Engineer (Hunter AI).
using System.Collections.Generic;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>A test leaf: returns whatever the test sets, counts its ticks and resets, and logs its name when ticked.</summary>
    internal sealed class ScriptedNode : Node
    {
        private readonly List<string> log;

        public ScriptedNode(string name, NodeStatus result, List<string> log = null) : base(name)
        {
            Result = result;
            this.log = log;
        }

        public NodeStatus Result { get; set; }
        public int Ticks { get; private set; }
        public int Resets { get; private set; }

        protected override NodeStatus OnTick(BtContext context)
        {
            Ticks++;
            log?.Add(Name);
            return Result;
        }

        protected override void OnReset()
        {
            Resets++;
        }
    }
}
