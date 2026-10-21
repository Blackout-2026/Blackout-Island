// Owner: Systems Engineer.
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// One in-memory save on the player. CheckpointTracker requests a save; LateUpdate captures after all
    /// synchronous objective/cure listeners have finished. Additive scenes and their lighting stay loaded.
    /// Each system owns its small snapshot; this class does not know the four brains or their assemblies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheckpointWorld : MonoBehaviour
    {
        private sealed class Entry
        {
            public MonoBehaviour Component;
            public ICheckpointState Owner;
            public object State;
        }

        private readonly List<Entry> saved = new List<Entry>();
        private readonly HashSet<MonoBehaviour> savedObjects = new HashSet<MonoBehaviour>();
        private bool pending;
        private Health playerHealth;
        private object reachedHealth;

        public bool HasSnapshot { get; private set; }

        public void RequestCapture()
        {
            playerHealth = GetComponent<Health>();
            // A fatal hit later in this frame must not save a dead player at the new checkpoint.
            reachedHealth = playerHealth != null ? playerHealth.CaptureCheckpoint() : null;
            pending = true;
        }

        private void LateUpdate()
        {
            if (pending) CaptureNow();
        }

        /// <summary>Also exposed for focused tests. Normal gameplay saves at the end of the checkpoint frame.</summary>
        public void CaptureNow()
        {
            saved.Clear();
            savedObjects.Clear();
            foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(component is ICheckpointState owner) || !component.gameObject.scene.IsValid()) continue;
                // The spawner owns its instances, including their health and any future snapshot components.
                if (component.GetComponentInParent<ICheckpointRosterMember>(true) != null) continue;
                saved.Add(new Entry
                {
                    Component = component, Owner = owner,
                    State = component == playerHealth && pending ? reachedHealth : owner.CaptureCheckpoint()
                });
                savedObjects.Add(component);
            }
            pending = false;
            HasSnapshot = true;
        }

        public bool Restore()
        {
            if (!HasSnapshot) return false;
            pending = false; // death must never replace the saved checkpoint with the failed attempt
            foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (component is ICheckpointSpawnedObject spawned && !savedObjects.Contains(component))
                    spawned.DiscardForCheckpoint();

            RestoreEntries(false);
            RestoreEntries(true);
            Physics.SyncTransforms();
            return true;
        }

        private void RestoreEntries(bool roster)
        {
            foreach (Entry entry in saved)
                if (entry.Component != null && (entry.Owner is ICheckpointRoster) == roster)
                    entry.Owner.RestoreCheckpoint(entry.State);
        }
    }
}
