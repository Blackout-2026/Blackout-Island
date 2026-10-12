// Owner: Agent Controller.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Fires an agent's weapon: the shared executor every agent type uses through AgentIntent.fire (A4). The
    /// brain only sets intent.fire/aim/lookTarget; this decides whether a shot is actually allowed this frame
    /// (reaction delay, facing, range — "canSee" is the brain's own job, already true by the time fire is set)
    /// and resolves it (aim error, hitscan raycast, damage, Gunshot noise, tracer). Ports agent_base.js's
    /// shared shooting logic (docs/reference/agent-controller.md 5.1); see ShotMath for the two terms
    /// (player speed, crouch) left out because this shooter never sees the player directly.
    /// </summary>
    public class AgentShooter : MonoBehaviour, IAgentShooter
    {
        [SerializeField] private AgentWeaponConfig config = new AgentWeaponConfig();
        [Tooltip("Where shots originate. Empty uses this transform plus Eye Height.")]
        [SerializeField] private Transform muzzle;
        [Min(0f)] [SerializeField] private float eyeHeight = 1.62f;
        [SerializeField] private LayerMask hitMask = ~0;
        [Tooltip("Optional: raises a Gunshot NoiseEvent on every shot.")]
        [SerializeField] private NoiseEventChannel gunfireChannel;
        [Tooltip("Optional: shows a pooled tracer line on every shot.")]
        [SerializeField] private AgentTracerPool tracerPool;

        private AgentIntent currentIntent;
        private bool engaging;
        private float engagedSeconds;
        private float shotTimer;
        private int burstShotsFired;

        public AgentWeaponConfig Config => config;

        /// <summary>
        /// Overrides the Inspector-assigned fields. The real prefab never calls this (its serialized values
        /// stand); tests use it to inject a fake channel/mask/muzzle without needing a prefab. Any parameter
        /// left at its default keeps the current value.
        /// </summary>
        public void Configure(AgentWeaponConfig newConfig = null, NoiseEventChannel newGunfireChannel = null,
            AgentTracerPool newTracerPool = null, Transform newMuzzle = null, LayerMask? newHitMask = null)
        {
            if (newConfig != null)
            {
                config = newConfig;
            }
            if (newGunfireChannel != null)
            {
                gunfireChannel = newGunfireChannel;
            }
            if (newTracerPool != null)
            {
                tracerPool = newTracerPool;
            }
            if (newMuzzle != null)
            {
                muzzle = newMuzzle;
            }
            if (newHitMask.HasValue)
            {
                hitMask = newHitMask.Value;
            }
        }
        /// <summary>Set by whatever applies the alert buff (hook only; nothing wires this yet — A4 does not add the buff system). ×0.9 aim error while true, matching the heightened multiplier in agent-controller.md section 8.</summary>
        public bool Heightened { get; set; }
        /// <summary>Shots actually fired (not just attempted). For tests and the future debug overlay.</summary>
        public int ShotsFired { get; private set; }

        public void Apply(in AgentIntent intent)
        {
            currentIntent = intent;
            if (intent.fire)
            {
                if (!engaging)
                {
                    engaging = true;
                    engagedSeconds = 0f;
                    shotTimer = 0f;
                    burstShotsFired = 0;
                }
            }
            else
            {
                CeaseFire();
            }
        }

        public void CeaseFire()
        {
            engaging = false;
            engagedSeconds = 0f;
            shotTimer = 0f;
            burstShotsFired = 0;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>One frame of firing logic. Update calls it every frame; public so tests can drive time.</summary>
        public void Tick(float dt)
        {
            if (!engaging)
            {
                return;
            }

            engagedSeconds += dt;
            shotTimer -= dt;
            if (shotTimer > 0f || engagedSeconds < config.reactionDelay)
            {
                return;
            }

            if (!CanFireAt(currentIntent, out Vector3 origin, out Vector3 direction, out float distance))
            {
                return;
            }

            FireOneShot(origin, direction, distance);

            burstShotsFired++;
            ShotsFired++;
            shotTimer = ShotMath.NextShotDelay(burstShotsFired, config.burstCount, config.burstInterval, config.burstPause, Random.value);
            if (burstShotsFired >= config.burstCount)
            {
                burstShotsFired = 0;
            }
        }

        private bool CanFireAt(in AgentIntent intent, out Vector3 origin, out Vector3 direction, out float distance)
        {
            origin = MuzzlePosition();
            Vector3 toTarget = intent.lookTarget - origin;
            distance = toTarget.magnitude;
            direction = distance > 1e-4f ? toTarget / distance : transform.forward;
            if (distance <= 1e-4f || distance > config.range)
            {
                return false;
            }

            // agent-controller.md 5.1: facing the player, forward . direction >= 0.85 (about a 31.8 degree half-cone).
            return Vector3.Dot(transform.forward, direction) >= config.facingDotThreshold;
        }

        private Vector3 MuzzlePosition()
        {
            return muzzle != null ? muzzle.position : transform.position + Vector3.up * eyeHeight;
        }

        private void FireOneShot(Vector3 origin, Vector3 direction, float distance)
        {
            float error = ShotMath.AimError(config.spread, distance, engagedSeconds, Heightened);
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right; // direction is ~vertical: pick an arbitrary perpendicular
            Vector3 up = Vector3.Cross(direction, right);
            Vector3 shotDirection = ShotMath.PerturbedDirection(direction, right, up, error, Random.value, Random.value);

            // agent-controller.md 5.1: the ray starts a little in front of the muzzle so the shooter's own geometry cannot self-block it.
            Vector3 rayOrigin = origin + shotDirection * config.muzzleForwardOffset;
            Vector3 end = rayOrigin + shotDirection * config.range;

            if (Physics.Raycast(rayOrigin, shotDirection, out RaycastHit hit, config.range, hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    target.TakeDamage(new DamageInfo(config.damage, hit.point, shotDirection, gameObject, DamageType.Bullet));
                }
            }

            tracerPool?.Show(rayOrigin, end);
            gunfireChannel?.Raise(new NoiseEvent(transform.position, config.noiseLoudness, NoiseType.Gunshot, gameObject));
        }
    }
}
