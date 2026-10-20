// Owner: Systems Engineer (Hunter AI).
using System;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// A leaf that does something: it returns Running until it is done, then Success or Failure. Named ActionNode
    /// because Action is System.Action. An action that remembers progress between ticks (a target, a timer) keeps it
    /// on the <see cref="LocalBlackboard"/> or in its closure, and clears it in <c>onReset</c>, which runs when the
    /// action is interrupted or its branch starts over (the prototype's <c>mem</c> object).
    /// </summary>
    public sealed class ActionNode : Node
    {
        private readonly Func<BtContext, NodeStatus> action;
        private readonly Action onReset;

        public ActionNode(string name, Func<BtContext, NodeStatus> action, Action onReset = null) : base(name)
        {
            this.action = action ?? throw new ArgumentNullException(nameof(action));
            this.onReset = onReset;
        }

        protected override NodeStatus OnTick(BtContext context)
        {
            return action(context);
        }

        protected override void OnReset()
        {
            onReset?.Invoke();
        }
    }
}
