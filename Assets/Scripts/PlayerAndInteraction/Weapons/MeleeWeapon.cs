// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// The knife. A swing lands after a short delay (0.11 s), then hits the nearest living IDamageable whose
    /// centre is within reach, inside the cone in front of the camera and not behind a wall.
    /// A swing that hits nothing raycasts straight ahead for a spark on the wall. No ammo.
    /// </summary>
    public class MeleeWeapon : MonoBehaviour, IWeapon
    {
        // Enough for a crowd of agents; extra colliders beyond this are ignored.
        private const int MaxOverlaps = 16;
        private const int SparksOnBody = 3;

        [SerializeField] private WeaponData data;

        private readonly Collider[] overlaps = new Collider[MaxOverlaps];
        private WeaponContext context;
        // Seconds until the blade lands; negative when no swing is in progress.
        private float swingTimer = -1f;

        public WeaponData Data => data;

        public void Initialize(WeaponContext weaponContext)
        {
            context = weaponContext;
        }

        public void SetEquipped(bool equipped)
        {
            swingTimer = -1f;
            gameObject.SetActive(equipped);
        }

        public void Fire()
        {
            swingTimer = data.meleeHitDelay;
            context.PublishNoise(data.noiseRadius, NoiseType.Melee);
        }

        public void Tick(float dt)
        {
            if (swingTimer < 0f)
            {
                return;
            }

            swingTimer -= dt;
            if (swingTimer <= 0f)
            {
                swingTimer = -1f;
                ResolveSwing();
            }
        }

        private void ResolveSwing()
        {
            Vector3 origin = context.Aim.position;
            Vector3 forward = context.Aim.forward;
            float reach = data.range + WeaponMath.MeleeReachTolerance;

            IDamageable best = null;
            Vector3 bestPoint = Vector3.zero;
            float bestSqrDistance = float.MaxValue;

            int count = Physics.OverlapSphereNonAlloc(origin, reach, overlaps, context.HitMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = overlaps[i];
                if (context.IsOwn(candidate))
                {
                    continue;
                }

                var target = candidate.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                Vector3 centre = candidate.bounds.center;
                float sqrDistance = (centre - origin).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance
                    || !WeaponMath.InMeleeCone(origin, forward, centre, reach, data.meleeConeAngle)
                    || !HasLineOfSight(origin, centre, target))
                {
                    continue;
                }

                best = target;
                bestPoint = centre;
                bestSqrDistance = sqrDistance;
            }

            if (best != null)
            {
                context.ApplyDamage(best, new DamageInfo(data.damage, bestPoint, forward, context.Owner, DamageType.Melee));
                if (context.Impacts != null)
                {
                    context.Impacts.Play(bestPoint, -forward, SparksOnBody);
                }
            }
            else if (context.Impacts != null
                     && Physics.Raycast(origin, forward, out RaycastHit hit, data.range, context.HitMask,
                         QueryTriggerInteraction.Ignore)
                     && !context.IsOwn(hit.collider))
            {
                context.Impacts.Play(hit.point, hit.normal); // the blade struck a wall
            }
        }

        /// <summary>True if nothing but the target itself is between the eye and the target's centre.</summary>
        private bool HasLineOfSight(Vector3 origin, Vector3 centre, IDamageable target)
        {
            if (!Physics.Linecast(origin, centre, out RaycastHit hit, context.HitMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return ReferenceEquals(hit.collider.GetComponentInParent<IDamageable>(), target);
        }
    }
}
