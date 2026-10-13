// Owner: Systems Engineer.
using System;
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// Remembers the last Checkpoint marker the player reached, for respawning. A checkpoint is reached within
    /// <see cref="reachRadius"/>; escape checkpoints only count after CureTaken (docs/reference section 8).
    /// Markers live in the Environment scene, so they are found at runtime, and looked for again until found
    /// (an additive scene may load after the player).
    /// </summary>
    public class CheckpointTracker : MonoBehaviour
    {
        private const float SearchInterval = 1f;

        [Tooltip("Metres from a Checkpoint marker at which it becomes the respawn point.")]
        [SerializeField, Min(0.5f)] private float reachRadius = 6f;
        [Tooltip("CureTaken.asset in Assets/ScriptableObjects/Events: switches to the escape checkpoints.")]
        [SerializeField] private VoidEventChannel cureTaken;

        private readonly List<Marker> checkpoints = new List<Marker>();
        private float nextSearchTime;
        private Vector3 spawnPosition;
        private float spawnYaw;

        /// <summary>Raised when a new checkpoint becomes the respawn point (the HUD shows a toast).</summary>
        public event Action<Marker> CheckpointReached;

        /// <summary>The respawn checkpoint, or null before any is reached (respawn at the spawn point).</summary>
        public Marker Current { get; private set; }
        public bool IsCureTaken { get; private set; }

        private void Awake()
        {
            spawnPosition = transform.position;
            spawnYaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            if (cureTaken != null)
            {
                cureTaken.OnRaised += OnCureTaken;
            }
        }

        private void OnDisable()
        {
            if (cureTaken != null)
            {
                cureTaken.OnRaised -= OnCureTaken;
            }
        }

        private void OnCureTaken()
        {
            IsCureTaken = true;
        }

        private void Update()
        {
            if (checkpoints.Count == 0 && Time.time >= nextSearchTime)
            {
                FindCheckpoints();
            }

            Vector3 position = transform.position;
            for (int i = 0; i < checkpoints.Count; i++)
            {
                Marker marker = checkpoints[i];
                if (marker == null || marker == Current)
                {
                    continue; // null: its scene was unloaded
                }

                if (PlayerHealthRules.IsCheckpointEligible(marker.Id, IsCureTaken)
                    && PlayerHealthRules.InReach(position, marker.transform.position, reachRadius))
                {
                    SetCheckpoint(marker);
                    return;
                }
            }
        }

        /// <summary>Makes a checkpoint the respawn point. Public so the MissionDirector (Y6) can set it.</summary>
        public void SetCheckpoint(Marker marker)
        {
            Current = marker;
            CheckpointReached?.Invoke(marker);
        }

        /// <summary>Where to respawn: the current checkpoint, or the spawn point if none has been reached.</summary>
        public void GetRespawnPose(out Vector3 position, out float yaw)
        {
            if (Current != null)
            {
                position = Current.transform.position;
                yaw = Current.transform.eulerAngles.y;
                return;
            }

            position = spawnPosition;
            yaw = spawnYaw;
        }

        private void FindCheckpoints()
        {
            nextSearchTime = Time.time + SearchInterval;
            foreach (Marker marker in FindObjectsByType<Marker>(FindObjectsSortMode.None))
            {
                if (marker.Type == MarkerType.Checkpoint)
                {
                    checkpoints.Add(marker);
                }
            }
        }
    }
}
