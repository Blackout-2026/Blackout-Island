using System.Collections.Generic;
using Blackout.Player;
using Blackout.Shared;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Debug window that lists every Checkpoint marker in the open scenes and teleports the player to one.
    /// Markers live in the Environment scene, so they are found at runtime with FindObjectsByType, never through
    /// Inspector references. It only works in Play mode, so it never changes a scene file.
    /// </summary>
    public class CheckpointTeleportWindow : EditorWindow
    {
        private readonly List<Marker> checkpoints = new List<Marker>();
        private Vector2 scroll;

        [MenuItem("Blackout/Debug/Teleport To Checkpoint")]
        public static void Open()
        {
            GetWindow<CheckpointTeleportWindow>("Teleport");
        }

        /// <summary>Checkpoint markers in all open scenes, sorted by id so the list order never changes.</summary>
        public static List<Marker> FindCheckpoints(List<Marker> results)
        {
            results.Clear();
            foreach (Marker marker in FindObjectsByType<Marker>(FindObjectsSortMode.None))
            {
                if (marker.Type == MarkerType.Checkpoint)
                {
                    results.Add(marker);
                }
            }

            results.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return results;
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            FindCheckpoints(checkpoints);
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnFocus()
        {
            FindCheckpoints(checkpoints);
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            FindCheckpoints(checkpoints);
            Repaint();
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to teleport the player.", MessageType.Info);
            }

            if (GUILayout.Button("Refresh"))
            {
                FindCheckpoints(checkpoints);
            }

            if (checkpoints.Count == 0)
            {
                EditorGUILayout.HelpBox("No Checkpoint markers in the open scenes. Open Environment.unity.",
                    MessageType.Warning);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                foreach (Marker marker in checkpoints)
                {
                    // A marker can be destroyed when scenes unload; skip it until the next refresh.
                    if (marker != null && GUILayout.Button(string.IsNullOrEmpty(marker.Id) ? marker.name : marker.Id))
                    {
                        Teleport(marker);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void Teleport(Marker marker)
        {
            var motor = FindFirstObjectByType<PlayerMotor>();
            if (motor == null)
            {
                Debug.LogWarning("[Blackout] No PlayerMotor in the open scenes; nothing to teleport.");
                return;
            }

            Transform target = marker.transform;
            motor.Teleport(target.position, target.eulerAngles.y);
            if (motor.TryGetComponent(out PlayerLook look))
            {
                look.ResetPitch();
            }

            Debug.Log($"[Blackout] Teleported the player to {marker.Id} at {target.position}.");
        }
    }
}
