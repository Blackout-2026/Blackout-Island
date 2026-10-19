// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// A gun that hits instantly along a ray (SMG and rifle). Each pellet gets a random direction inside the
    /// spread cone, raycasts from the camera, and damages the first IDamageable it hits with distance falloff.
    /// Sits on the weapon's GameObject under the WeaponHolder, with the viewmodel as its children.
    /// </summary>
    public class HitscanWeapon : MonoBehaviour, IWeapon
    {
        [SerializeField] private WeaponData data;
        [Tooltip("End of the barrel: where tracers start and the flash appears.")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private MuzzleFlash muzzleFlash;

        private WeaponContext context;
        private float bloom;
        private int shots;

        public WeaponData Data => data;

        public void Initialize(WeaponContext weaponContext)
        {
            context = weaponContext;
        }

        public void SetEquipped(bool equipped)
        {
            gameObject.SetActive(equipped);
        }

        public void Tick(float dt)
        {
            bloom = WeaponMath.RecoverBloom(bloom, dt);
        }

        public void Fire()
        {
            Transform aim = context.Aim;
            float spread = WeaponMath.Spread(data, context.MoveFraction, bloom, context.Grounded);
            shots++;

            for (int i = 0; i < data.pellets; i++)
            {
                Vector3 direction = WeaponMath.SpreadDirection(aim.forward, aim.right, aim.up, spread,
                    Random.value, Random.value);
                FirePellet(aim.position, direction);
            }

            bloom = WeaponMath.AddBloom(bloom, data);
            if (muzzleFlash != null)
            {
                muzzleFlash.Flash(data.muzzleFlashScale);
            }

            context.PublishNoise(data.noiseRadius, NoiseType.Gunshot);
        }

        private void FirePellet(Vector3 origin, Vector3 direction)
        {
            Vector3 end = origin + direction * data.range;
            bool hitSomething = Physics.Raycast(origin, direction, out RaycastHit hit, data.range, context.HitMask,
                QueryTriggerInteraction.Ignore);
            context.PelletTraced?.Invoke(new PelletTrace(new Ray(origin, direction), data.range,
                hitSomething ? hit.collider : null, hitSomething ? hit.distance : data.range));

            if (hitSomething && !context.IsOwn(hit.collider))
            {
                end = hit.point;
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    float damage = data.damage * WeaponMath.FalloffMultiplier(hit.distance, data);
                    context.ApplyDamage(target,
                        new DamageInfo(damage, hit.point, direction, context.Owner, DamageType.Bullet));
                }

                if (context.Impacts != null)
                {
                    context.Impacts.Play(hit.point, hit.normal);
                }
            }

            if (context.Tracers != null && data.tracerEvery > 0 && shots % data.tracerEvery == 0)
            {
                context.Tracers.Show(muzzle != null ? muzzle.position : origin, end);
            }
        }
    }
}
