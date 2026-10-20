// Owner: World Builder (Security Patrol AI).
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>
    /// Bridges the shared Noise channel to one patrol's plain C# perception.
    /// The host supplies the initial position, updates Position as it moves, and disposes this
    /// listener on disable/death. No scene search or delegate allocation occurs per event.
    /// </summary>
    public sealed class PerceptionNoiseListener : IDisposable
    {
        private readonly Perception perception;
        private readonly NoiseEventChannel channel;
        private readonly GameObject owner;
        private readonly Transform ownerTransform;
        private readonly Action<NoiseEvent> onNoise;
        private bool disposed;

        public Vector3 Position { get; set; }

        public PerceptionNoiseListener(Perception perception, NoiseEventChannel channel,
            Vector3 position, GameObject owner = null)
        {
            this.perception = perception ?? throw new ArgumentNullException(nameof(perception));
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));

            this.channel = channel;
            this.owner = owner;
            ownerTransform = owner != null ? owner.transform : null;
            Position = position;
            onNoise = OnNoise;
            channel.OnRaised += onNoise;
        }

        private void OnNoise(NoiseEvent noise)
        {
            if (owner != null && noise.source != null &&
                (noise.source == owner || noise.source.transform.IsChildOf(ownerTransform)))
                return;

            perception.HearNoise(Position, noise);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (channel != null)
                channel.OnRaised -= onNoise;
            disposed = true;
        }
    }
}
