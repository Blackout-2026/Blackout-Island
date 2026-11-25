using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>Pause the common executors, rather than changing the FSM, BT, utility or GOAP brain.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class AgentTakedownTarget : MonoBehaviour, ITakedownCombatTarget
    {
        [SerializeField] private bool eligible = true;
        [SerializeField] private Animator actorAnimator;
        [SerializeField] private AgentRoot agent;
        [SerializeField] private PathFollower mover;
        [SerializeField] private AgentShooter shooter;
        [SerializeField] private AnimatorDriver driver;
        [SerializeField] private Renderer[] carriedWeapon = new Renderer[0];
        [SerializeField] private Transform disarmWeapon;
        private Health health;
        private Object owner;
        private Behaviour[] suspended;
        private bool[] enabledBefore;
        private bool[] weaponVisibleBefore;
        private Transform weaponParent;
        private Vector3 weaponPosition, weaponScale;
        private Quaternion weaponRotation;
        private Rigidbody kickedBody;
        private BoxCollider kickedCollider;
        private int weaponLayer;
        private bool frontalPrepared;

        public bool IsFiring => shooter != null && shooter.isActiveAndEnabled && shooter.IsEngaging;
        public bool WeaponKicked { get; private set; }

        public Transform Root => transform;
        public Health Health => health != null ? health : GetComponent<Health>();
        public Animator Animator => actorAnimator;
        public bool Available => isActiveAndEnabled && eligible && owner == null && Health.IsAlive
            && !Health.IsInvulnerable && actorAnimator != null && actorAnimator.isActiveAndEnabled;

        private void Awake()
        {
            health = GetComponent<Health>();
            suspended = new Behaviour[] { agent, mover, shooter, driver };
            enabledBefore = new bool[suspended.Length];
            weaponVisibleBefore = new bool[carriedWeapon.Length];
        }

        private void OnEnable() { if (health != null) health.OnDied += HideDeadWeapon; }
        private void HideDeadWeapon()
        {
            // The normal death callback creates the pickup. Do not leave a second kicked gun visible.
            if (owner != null)
                foreach (Renderer part in carriedWeapon) if (part != null) part.enabled = false;
        }

        public bool TryReserve(Object claimant)
        {
            if (claimant == null || !Available) return false;
            owner = claimant;
            mover?.StopImmediately();
            shooter?.CeaseFire();
            for (int i = 0; i < carriedWeapon.Length; i++)
                if (carriedWeapon[i] != null)
                {
                    weaponVisibleBefore[i] = carriedWeapon[i].enabled;
                    carriedWeapon[i].enabled = false;
                }
            for (int i = 0; i < suspended.Length; i++)
                if (suspended[i] != null)
                {
                    enabledBefore[i] = suspended[i].enabled;
                    suspended[i].enabled = false;
                }
            return true;
        }

        public bool IsReservedBy(Object claimant) => owner != null && owner == claimant;

        public void PrepareFrontal(Object claimant)
        {
            if (!IsReservedBy(claimant)) return;
            frontalPrepared = true;
            for (int i = 0; i < carriedWeapon.Length; i++)
                if (carriedWeapon[i] != null) carriedWeapon[i].enabled = weaponVisibleBefore[i];
        }

        public void KickWeapon(Object claimant, Vector3 direction)
        {
            if (!IsReservedBy(claimant) || !frontalPrepared || WeaponKicked) return;
            WeaponKicked = true;
            if (disarmWeapon == null || disarmWeapon.GetComponent<Rigidbody>() != null) return;
            weaponParent = disarmWeapon.parent;
            weaponPosition = disarmWeapon.localPosition;
            weaponRotation = disarmWeapon.localRotation;
            weaponScale = disarmWeapon.localScale;
            weaponLayer = disarmWeapon.gameObject.layer;
            Bounds bounds = new Bounds(disarmWeapon.position, Vector3.one * 0.1f);
            foreach (Renderer part in carriedWeapon) if (part != null) bounds.Encapsulate(part.bounds);
            disarmWeapon.SetParent(null, true); // Runtime visual only; original prefab nesting is restored on release.
            disarmWeapon.gameObject.layer = PhysicsLayers.Ragdoll;
            kickedCollider = disarmWeapon.gameObject.AddComponent<BoxCollider>();
            kickedCollider.center = disarmWeapon.InverseTransformPoint(bounds.center);
            Vector3 scale = disarmWeapon.lossyScale;
            kickedCollider.size = new Vector3(bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)), bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
            kickedBody = disarmWeapon.gameObject.AddComponent<Rigidbody>();
            kickedBody.mass = 0.8f;
            kickedBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            kickedBody.linearVelocity = direction.normalized * 3f + Vector3.up;
            kickedBody.angularVelocity = new Vector3(3f, 5f, 2f);
        }

        private void RestoreWeapon()
        {
            if (kickedBody != null)
            {
                kickedBody.linearVelocity = kickedBody.angularVelocity = Vector3.zero;
                kickedBody.isKinematic = true;
                kickedBody.detectCollisions = false;
                Destroy(kickedBody);
            }
            if (kickedCollider != null) Destroy(kickedCollider);
            if (weaponParent != null && disarmWeapon != null)
            {
                disarmWeapon.SetParent(weaponParent, false);
                disarmWeapon.SetLocalPositionAndRotation(weaponPosition, weaponRotation);
                disarmWeapon.localScale = weaponScale;
                disarmWeapon.gameObject.layer = weaponLayer;
            }
            else if (kickedBody != null && disarmWeapon != null) Destroy(disarmWeapon.gameObject);
            kickedBody = null; kickedCollider = null; weaponParent = null;
            WeaponKicked = frontalPrepared = false;
        }

        public void Release(Object claimant)
        {
            if (!IsReservedBy(claimant)) return;
            mover?.StopImmediately(); // Rebase facing and velocity to the aligned root before resuming.
            shooter?.CeaseFire();
            RestoreWeapon();
            if (actorAnimator != null && actorAnimator.enabled && actorAnimator.runtimeAnimatorController != null && !Health.IsAlive)
            {
                // Health/AgentRoot already handled death, drops, registry removal and the fade clock.
                // Resume the corpse at the end of the normal death state instead of replaying its fall.
                actorAnimator.ResetTrigger("Hit");
                actorAnimator.ResetTrigger("Shoot");
                actorAnimator.SetBool("Dead", true);
                int death = UnityEngine.Animator.StringToHash("Base Layer.Death");
                if (actorAnimator.HasState(0, death)) actorAnimator.Play(death, 0, 1f);
                actorAnimator.Update(0f);
            }
            owner = null;
            if (Health.IsAlive)
                for (int i = 0; i < carriedWeapon.Length; i++)
                    if (carriedWeapon[i] != null) carriedWeapon[i].enabled = weaponVisibleBefore[i];
            if (!Health.IsAlive)
                foreach (Renderer part in carriedWeapon) if (part != null) part.enabled = false;
            for (int i = 0; i < suspended.Length; i++)
                if (suspended[i] != null) suspended[i].enabled = enabledBefore[i];
        }

        private void OnDisable()
        {
            if (health != null) health.OnDied -= HideDeadWeapon;
            if (owner != null) Release(owner);
        }
    }
}
