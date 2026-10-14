// Owner: Agent Controller.
using System;
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// PLACEHOLDER brain, not one of the four real AIs. It only walks the agent's own Waypoint markers in order,
    /// pausing at each, so the spawner, the mover and the stress test have agents that really move until the
    /// Security Patrol, Hunter, Tactical Guard and Commander brains are merged. It never looks, aims or fires.
    /// The spawner adds it only to a prefab that has no brain of its own, and only when asked to.
    /// </summary>
    public class WaypointWalkerBrain : MonoBehaviour, IAgentBrain, IDebugInfo
    {
        [Tooltip("The prototype's patrol pauses 2.2 s at each waypoint.")]
        [SerializeField, Min(0f)] private float pauseSeconds = 2.2f;
        [Tooltip("The prototype's patrol counts a waypoint as reached within 0.9 m.")]
        [SerializeField, Min(0.1f)] private float reachDistance = 0.9f;

        private IAgentContext context;
        private IReadOnlyList<Marker> sourceMarkers;
        private Marker[] route = Array.Empty<Marker>();
        private int routeIndex;
        private float pauseLeft;

        public void Initialize(IAgentContext newContext)
        {
            context = newContext;
            sourceMarkers = null;
            route = Array.Empty<Marker>();
            routeIndex = 0;
            pauseLeft = 0f;
        }

        public AgentIntent Tick(float deltaTime)
        {
            RefreshRoute();
            if (route.Length == 0)
            {
                return AgentIntent.Idle;
            }

            Vector3 target = route[routeIndex].transform.position;
            if (pauseLeft > 0f)
            {
                pauseLeft -= deltaTime;
                if (pauseLeft <= 0f)
                {
                    routeIndex = (routeIndex + 1) % route.Length;
                }
                return AgentIntent.Idle;
            }

            if (FlatDistance(context.Self.position, target) < reachDistance)
            {
                pauseLeft = pauseSeconds;
                return AgentIntent.Idle;
            }

            return new AgentIntent { moveTarget = target, moveSpeed = MoveSpeed.Walk };
        }

        public string GetDebugText()
        {
            return route.Length == 0 ? "WaypointWalker (no waypoints)" : $"WaypointWalker -> {route[routeIndex].Id}";
        }

        // The markers may load after the agent, and the registry may rebuild its index: follow whichever list it gives now.
        private void RefreshRoute()
        {
            IReadOnlyList<Marker> markers = context.GetMarkers(MarkerType.Waypoint, context.Self.name);
            if (ReferenceEquals(markers, sourceMarkers))
            {
                return;
            }

            sourceMarkers = markers;
            var sorted = new Marker[markers.Count];
            for (int i = 0; i < sorted.Length; i++)
            {
                sorted[i] = markers[i];
            }
            Array.Sort(sorted, (a, b) => string.CompareOrdinal(a.Id, b.Id)); // Waypoint_Patrol-1_01, _02, ... in route order
            route = sorted;
            routeIndex = 0;
            pauseLeft = 0f;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
