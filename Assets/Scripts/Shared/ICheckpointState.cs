// Owner: Systems Engineer. Shared contract for the solo checkpoint recovery flow.
namespace Blackout.Shared
{
    /// <summary>In-memory gameplay state. Capture must return an independent value, not live mutable data.</summary>
    public interface ICheckpointState
    {
        object CaptureCheckpoint();
        void RestoreCheckpoint(object state);
    }

    /// <summary>Restore the roster after world flags, doors and player equipment have been restored.</summary>
    public interface ICheckpointRoster : ICheckpointState { }

    /// <summary>The roster owns this instance and its children, rather than the world save retaining them.</summary>
    public interface ICheckpointRosterMember { }

    /// <summary>A runtime pickup created after the save must disappear when that save is restored.</summary>
    public interface ICheckpointSpawnedObject : ICheckpointState
    {
        void DiscardForCheckpoint();
    }
}
