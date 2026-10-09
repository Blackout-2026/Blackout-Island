// Owner: Agent Controller. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
// Created by the Systems Engineer in Y1; only the Agent Controller edits it from now on.
using UnityEngine;

namespace Blackout.Shared
{
    public enum DirectiveType
    {
        Converge,
        SearchZone,
        Regroup
    }

    /// <summary>An order from the Commander to another agent: where to go and how wide an area to cover.</summary>
    public readonly struct Directive
    {
        public readonly DirectiveType type;
        public readonly Vector3 position;
        /// <summary>Area radius in metres around <see cref="position"/>.</summary>
        public readonly float radius;

        public Directive(DirectiveType type, Vector3 position, float radius)
        {
            this.type = type;
            this.position = position;
            this.radius = radius;
        }
    }

    /// <summary>Implemented by brains that accept Commander orders. Each brain maps a directive to its own states.</summary>
    public interface IDirectiveReceiver
    {
        void OnDirective(Directive directive);
    }
}
