// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Interaction
{
    /// <summary>
    /// On an enemy that carries a gun (the Security Patrol, the Hunter, the Commander): when its Health raises OnDied it leaves a WeaponDropPickup on the ground
    /// beside the body (step L1). Health raises OnDied once per death, so one death leaves one pickup, and the subscription follows OnEnable and OnDisable, so a
    /// disabled or restarted agent never listens twice. What the pickup gives is decided when it is taken, not here.
    /// </summary>
    public class EnemyWeaponDrop : MonoBehaviour
    {
        [Tooltip("WeaponDrop.prefab in Assets/Prefabs/Interactables.")]
        [SerializeField] private GameObject dropPrefab;
        [Tooltip("Metres to the side of the body (the agent's right), so the pickup does not lie under the fallen enemy.")]
        [SerializeField, Min(0f)] private float sideOffset = 0.6f;
        [Tooltip("The ground is found with a ray down from the agent; the pickup lies on what it hits.")]
        [SerializeField] private LayerMask groundMask = 1 << PhysicsLayers.Default | 1 << PhysicsLayers.Level;

        private Health health;

        /// <summary>The last pickup this enemy left, for tests and the debug view.</summary>
        public GameObject LastDrop { get; private set; }

        private void OnEnable()
        {
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
            if (dropPrefab == null)
            {
                return;
            }

            Vector3 position = transform.position + transform.right * sideOffset;
            if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 4f, groundMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
            }

            LastDrop = Instantiate(dropPrefab, position + Vector3.up * 0.05f, Quaternion.Euler(0f, transform.eulerAngles.y + 90f, 0f));
        }
    }
}
