// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Interaction
{
    /// <summary>
    /// On an enemy that carries a gun (the Security Patrol, the Hunter, the Commander): when its Health raises OnDied it leaves a WeaponDropPickup on the ground
    /// beside the body (step L1). Health raises OnDied once per death, so one death leaves one pickup, and the subscription follows OnEnable and OnDisable, so a
    /// disabled or restarted agent never listens twice. What the pickup gives is decided when it is taken, not here.
    /// MC1 also drops an optional stored medkit on the other side. A robot uses the medkit field only;
    /// the existing class and weapon field names remain so serialized references keep working.
    /// F2 adds the body's ammunition (2 SMG magazines and 1 sniper magazine, see AmmoPickup) in front of every body, whether or not it had a gun.
    /// </summary>
    public class EnemyWeaponDrop : MonoBehaviour
    {
        [Tooltip("WeaponDrop.prefab in Assets/Prefabs/Interactables.")]
        [SerializeField] private GameObject dropPrefab;
        [Tooltip("Optional stored medkit, dropped on the opposite side from the gun.")]
        [SerializeField] private GameObject medkitPrefab;
        [Tooltip("AmmoDrop.prefab: the body's magazines, left at its head.")]
        [SerializeField] private GameObject ammoPrefab;
        [Tooltip("Metres to the side of the body (the agent's right), so the pickup does not lie under the fallen enemy.")]
        [SerializeField, Min(0f)] private float sideOffset = 0.6f;
        [Tooltip("The ground is found with a ray down from the agent; the pickup lies on what it hits.")]
        [SerializeField] private LayerMask groundMask = 1 << PhysicsLayers.Default | 1 << PhysicsLayers.Level;

        private Health health;

        /// <summary>The last pickup this enemy left, for tests and the debug view.</summary>
        public GameObject LastDrop { get; private set; }
        public GameObject LastMedkitDrop { get; private set; }
        public GameObject LastAmmoDrop { get; private set; }

        private void OnEnable()
        {
            if (health != null) health.OnDied -= OnDied;
            health = GetComponentInParent<Health>();
            if (health != null)
            {
                health.OnDied += OnDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDied -= OnDied;
                health = null;
            }
        }

        private void OnDied()
        {
            if (dropPrefab != null) LastDrop = SpawnPickup(dropPrefab, sideOffset);
            if (medkitPrefab != null) LastMedkitDrop = SpawnPickup(medkitPrefab, -sideOffset);
            if (ammoPrefab != null) LastAmmoDrop = SpawnPickup(ammoPrefab, 0f, sideOffset);
        }

        private GameObject SpawnPickup(GameObject prefab, float offset, float forward = 0f)
        {
            Vector3 position = transform.position + transform.right * offset + transform.forward * forward;
            if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 4f, groundMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
            }

            return Instantiate(prefab, position + Vector3.up * 0.05f, Quaternion.Euler(0f, transform.eulerAngles.y + 90f, 0f));
        }
    }
}
