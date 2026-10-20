// Owner: World Builder (Security Patrol AI).
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>Unity-facing sensor host. Returns values; it never chooses states or executes an intent.</summary>
    internal sealed class PatrolSensing : IDisposable
    {
        private readonly IAgentContext context;
        private readonly Perception perception;
        private readonly PerceptionNoiseListener noiseListener;
        private readonly float eyeHeight;
        private readonly float playerSightHeight;
        private Transform trackedPlayer;
        private Health playerHealth;

        public bool PlayerChanged { get; private set; }
        public bool PlayerIsDead { get; private set; }

        public PatrolSensing(IAgentContext context, SecurityPatrolConfig config,
            IPerceptionRaycast raycast, NoiseEventChannel noiseChannel)
        {
            this.context = context;
            eyeHeight = config != null ? config.eyeHeight : 1.62f;
            playerSightHeight = config != null ? config.playerSightHeight : 1.62f;
            perception = new Perception(config != null ? config.visionRange : 22f,
                config != null ? config.visionAngle : 1.95f * Mathf.Rad2Deg, raycast,
                config != null ? config.hearingRange : 65f, config != null ? config.hearingThreshold : 0.04f);
            if (noiseChannel != null)
                noiseListener = new PerceptionNoiseListener(perception, noiseChannel,
                    context.Self.position, context.Self.gameObject) { FollowOwnerPosition = true };
        }

        public PatrolPerceptionInput Read(float rangeMultiplier)
        {
            Transform player = context.Player;
            PlayerChanged = !ReferenceEquals(player, trackedPlayer);
            if (PlayerChanged)
            {
                trackedPlayer = player;
                playerHealth = player != null ? player.GetComponentInParent<Health>() : null;
            }
            PlayerIsDead = (context.Blackboard != null && context.Blackboard.playerNeutralized) ||
                (playerHealth != null && !playerHealth.IsAlive);

            Vector3 feet = player != null ? player.position : default;
            if (player != null && !PlayerIsDead)
                perception.UpdateVision(context.Self.position + Vector3.up * eyeHeight, context.Self.forward,
                    feet + Vector3.up * playerSightHeight, rangeMultiplier);
            else
                perception.ClearVision();

            var input = new PatrolPerceptionInput
            {
                canSeePlayer = perception.CanSeePlayer,
                playerPosition = feet,
                heardNoise = perception.HeardNoise,
                noisePosition = perception.NoisePosition
            };
            perception.ClearHeardNoise();
            return input;
        }

        public void Forget() => perception.Forget();
        public void Dispose() => noiseListener?.Dispose();
    }
}
