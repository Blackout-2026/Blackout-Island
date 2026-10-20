// Owner: World Builder (Security Patrol AI).
using System;
using System.Collections.Generic;
using Blackout.AI.SecurityPatrol;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>Selected patrol diagnostics only. Never initializes a brain or saves a scene/asset.</summary>
    [InitializeOnLoad]
    public static class SecurityPatrolGizmos
    {
        public readonly struct VisionSector
        {
            public readonly Vector3 Origin, Left, Right;
            public readonly float Range, Angle;

            public VisionSector(Vector3 origin, Vector3 left, Vector3 right, float range, float angle)
            {
                Origin = origin;
                Left = left;
                Right = right;
                Range = range;
                Angle = angle;
            }
        }

        private static readonly Dictionary<Transform, Marker[]> PreviewRoutes = new Dictionary<Transform, Marker[]>();
        private static Marker[] previewMarkers;
        private static readonly Color RouteColor = new Color32(0x47, 0xd1, 0xd6, 0xff);

        static SecurityPatrolGizmos()
        {
            EditorApplication.hierarchyChanged += InvalidatePreviewCache;
            Undo.undoRedoPerformed += InvalidatePreviewCache;
            Undo.postprocessModifications += OnPropertiesChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void InvalidatePreviewCache()
        {
            previewMarkers = null;
            PreviewRoutes.Clear();
        }

        private static UndoPropertyModification[] OnPropertiesChanged(UndoPropertyModification[] changes)
        {
            InvalidatePreviewCache();
            return changes;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state) => InvalidatePreviewCache();

        /// <summary>A horizontal slice at the sensing eye, with the sensor's full angle and effective range.</summary>
        public static bool TryGetVisionSector(SecurityPatrolBrain brain, out VisionSector sector)
        {
            sector = default;
            if (brain == null || brain.AgentTransform == null) return false;
            Transform self = brain.AgentTransform;
            Vector3 forward = self.forward;
            forward.y = 0f;
            float range = brain.EffectiveVisionRange;
            float angle = brain.VisionAngle;
            if (!(range > 0f) || float.IsInfinity(range) || !(angle >= 0f && angle <= 360f) ||
                forward.sqrMagnitude < 0.000001f) return false;

            forward.Normalize();
            sector = new VisionSector(self.position + Vector3.up * brain.VisionEyeHeight,
                Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * forward,
                Quaternion.AngleAxis(angle * 0.5f, Vector3.up) * forward, range, angle);
            return true;
        }

        /// <summary>Play uses the actual FSM route; Edit Mode previews owned markers in the same stage.</summary>
        public static IReadOnlyList<Marker> GetWaypoints(SecurityPatrolBrain brain)
        {
            if (brain == null || brain.AgentTransform == null) return Array.Empty<Marker>();
            if (brain.IsInitialized) return brain.Waypoints;
            Transform self = brain.AgentTransform;
            if (PreviewRoutes.TryGetValue(self, out Marker[] cached)) return cached;

            // Search once after an editor change, never once per Scene-view repaint.
            if (previewMarkers == null)
                previewMarkers = Object.FindObjectsByType<Marker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var stage = StageUtility.GetStageHandle(self.gameObject);
            var owned = new List<Marker>();
            for (int i = 0; i < previewMarkers.Length; i++)
            {
                Marker marker = previewMarkers[i];
                if (marker != null && marker.Type == MarkerType.Waypoint && marker.Owner == self.name &&
                    StageUtility.GetStageHandle(marker.gameObject).Equals(stage))
                    owned.Add(marker);
            }
            owned.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            Marker[] route = owned.ToArray();
            PreviewRoutes.Add(self, route);
            return route;
        }

        [DrawGizmo(GizmoType.Selected)]
        private static void DrawSelected(SecurityPatrolBrain brain, GizmoType type)
        {
            if (brain == null || brain.AgentTransform == null) return;
            Transform self = brain.AgentTransform;
            Health health = self.GetComponent<Health>();
            Color stateColor = new Color32(0x40, 0xff, 0x80, 0xff);
            if (!brain.isActiveAndEnabled || (brain.IsInitialized && health != null && !health.IsAlive))
                stateColor = new Color32(0x55, 0x55, 0x55, 0xff);
            else if (brain.CurrentState == SecurityPatrolState.Pursue || brain.CurrentState == SecurityPatrolState.Attack)
                stateColor = new Color32(0xff, 0x30, 0x20, 0xff);
            else if (brain.CurrentState == SecurityPatrolState.Investigate || brain.CurrentState == SecurityPatrolState.Search)
                stateColor = new Color32(0xff, 0xb0, 0x30, 0xff);

            using (new Handles.DrawingScope(stateColor, Matrix4x4.identity))
            {
                if (TryGetVisionSector(brain, out VisionSector cone))
                {
                    Handles.DrawWireArc(cone.Origin, Vector3.up, cone.Left, cone.Angle, cone.Range);
                    if (cone.Angle < 360f)
                    {
                        Handles.DrawLine(cone.Origin, cone.Origin + cone.Left * cone.Range);
                        Handles.DrawLine(cone.Origin, cone.Origin + cone.Right * cone.Range);
                    }
                }
            }

            IReadOnlyList<Marker> route = GetWaypoints(brain);
            using (new Handles.DrawingScope(RouteColor, Matrix4x4.identity))
            {
                for (int i = 0; i < route.Count; i++)
                {
                    Marker point = route[i];
                    if (point == null) continue;
                    Vector3 position = point.transform.position + Vector3.up * 0.1f;
                    Handles.DrawWireDisc(position, Vector3.up, 0.18f);
                    Handles.Label(position + Vector3.up * 0.25f, point.Id);
                    Marker next = route[(i + 1) % route.Count];
                    if (route.Count > 1 && next != null)
                        Handles.DrawLine(position, next.transform.position + Vector3.up * 0.1f);
                }
            }
        }
    }
}
