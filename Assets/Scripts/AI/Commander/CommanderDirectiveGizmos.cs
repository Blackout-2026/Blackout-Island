// Owner: Agent Controller (Commander diagnostics).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Commander
{
    /// <summary>One visualization of a living recipient's latest retained directive.</summary>
    public readonly struct CommanderDirectiveGizmo
    {
        public readonly DirectiveType Type;
        public readonly Vector3 Source;
        public readonly Vector3 Target;
        public readonly float Radius;
        public readonly Color Color;
        public float DrawRadius => Mathf.Max(0.25f, Radius);

        private CommanderDirectiveGizmo(Directive order, Vector3 source, Color color)
        {
            Type = order.type;
            Source = source;
            Target = order.position;
            Radius = order.radius;
            Color = color;
        }

        public static bool TryCreate(BlackboardAgent agent, Transform commander, out CommanderDirectiveGizmo gizmo)
        {
            gizmo = default;
            if (agent == null || !agent.IsAlive || !agent.HasDirective || agent.Transform == commander)
                return false;
            Directive order = agent.LastDirective;
            Vector3 source = agent.Transform.position;
            if (!Finite(source) || !Finite(order.position) || !Finite(order.radius) || order.radius < 0f)
                return false;

            Color color;
            switch (order.type)
            {
                case DirectiveType.Converge: color = UnityEngine.Color.cyan; break;
                case DirectiveType.SearchZone: color = UnityEngine.Color.yellow; break;
                case DirectiveType.Regroup: color = UnityEngine.Color.magenta; break;
                default: return false;
            }
            gizmo = new CommanderDirectiveGizmo(order, source, color);
            return true;
        }

        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Scene-view-only rendering. No Update, brain ticks, directive delivery or executor calls.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CommanderBrain))]
    public sealed class CommanderDirectiveGizmos : MonoBehaviour
    {
        private CommanderBrain brain;

        private CommanderBrain Brain
        {
            get
            {
                if (brain == null) brain = GetComponent<CommanderBrain>();
                return brain;
            }
        }

        /// <summary>The same read-only geometry query used by the Scene renderer and EditMode tests.</summary>
        public bool TryGetDirective(int rosterIndex, out CommanderDirectiveGizmo gizmo)
        {
            gizmo = default;
            Blackboard board = Brain != null ? Brain.DebugBlackboard : null;
            if (board == null || rosterIndex < 0 || rosterIndex >= board.Agents.Count)
                return false;
            return CommanderDirectiveGizmo.TryCreate(board.Agents[rosterIndex], transform, out gizmo);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enabled || Brain == null || Brain.DebugBlackboard == null) return;
            Blackboard board = Brain.DebugBlackboard;
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            try
            {
                Gizmos.matrix = Matrix4x4.identity;
                for (int i = 0; i < board.Agents.Count; i++)
                {
                    if (!TryGetDirective(i, out CommanderDirectiveGizmo order)) continue;
                    Gizmos.color = order.Color;
                    Gizmos.DrawLine(order.Source, order.Target);
                    Gizmos.DrawWireSphere(order.Target, order.DrawRadius);
                    if (order.Type == DirectiveType.Regroup)
                        Gizmos.DrawWireCube(order.Target, Vector3.one * 0.5f);
                }
            }
            finally
            {
                Gizmos.color = previousColor;
                Gizmos.matrix = previousMatrix;
            }
        }
#endif
    }
}
