// Owner: Systems Engineer.
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// Everything the weapons share, made once by WeaponController: where to aim from, what the rays may hit,
    /// the pooled effects, the noise channel, and how the player is moving (for spread). Weapons report hits
    /// through it, so the controller can raise one hit-marker event whichever weapon hit.
    /// </summary>
    public sealed class WeaponContext
    {
        private readonly NoiseEventChannel noiseChannel;
        private readonly Action<bool> onHit;

        public WeaponContext(Transform aim, GameObject owner, LayerMask hitMask, NoiseEventChannel noiseChannel,
            TracerPool tracers, ImpactEffects impacts, Action<bool> onHit)
        {
            Aim = aim;
            Owner = owner;
            HitMask = hitMask;
            Tracers = tracers;
            Impacts = impacts;
            this.noiseChannel = noiseChannel;
            this.onHit = onHit;
        }

        /// <summary>The camera: shots start at the eye and go where the crosshair points.</summary>
        public Transform Aim { get; }
        /// <summary>The player root, used as the damage and noise source.</summary>
        public GameObject Owner { get; }
        public LayerMask HitMask { get; }
        /// <summary>May be null (no tracers).</summary>
        public TracerPool Tracers { get; }
        /// <summary>May be null (no impact sparks).</summary>
        public ImpactEffects Impacts { get; }

        /// <summary>Speed / walk speed this frame, set by the controller.</summary>
        public float MoveFraction { get; set; }
        public bool Grounded { get; set; } = true;

        /// <summary>Debug: told about every pellet (the Stress Test's missed-hit check). Null in normal play.</summary>
        public Action<PelletTrace> PelletTraced { get; set; }

        /// <summary>True if the collider belongs to the shooter, so a weapon never hurts its owner.</summary>
        public bool IsOwn(Collider collider)
        {
            return collider.transform.IsChildOf(Owner.transform);
        }

        /// <summary>Applies damage and raises the hit marker. Returns true if this hit killed the target.</summary>
        public bool ApplyDamage(IDamageable target, DamageInfo info)
        {
            bool wasAlive = target.IsAlive;
            target.TakeDamage(info);
            bool killed = wasAlive && !target.IsAlive;
            onHit?.Invoke(killed);
            return killed;
        }

        public void PublishNoise(float radius, NoiseType type)
        {
            if (noiseChannel != null && radius > 0f)
            {
                noiseChannel.Raise(new NoiseEvent(Owner.transform.position, radius, type, Owner));
            }
        }
    }
}
