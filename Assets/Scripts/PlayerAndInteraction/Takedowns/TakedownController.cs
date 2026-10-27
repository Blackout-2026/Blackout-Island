using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using UnityEngine;

namespace Blackout.Takedowns
{
    /// <summary>Opt-in cinematic control. Missing paired assets leave the normal knife path untouched.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class TakedownController : MonoBehaviour, ICheckpointState
    {
        public enum Phase { Idle, Aligning, Playing, Returning }
        [SerializeField] private TakedownProfile profile;
        [SerializeField] private TakedownProfile frontalProfile;
        [SerializeField] private GameObject cinematicActorPrefab;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private WeaponController weapons;
        [SerializeField] private ViewmodelHands hands;
        [SerializeField] private Camera playerCamera;
        [Tooltip("Motor, look, camera effects, interaction, scope and weapon simulation. Input stays on for pause/release.")]
        [SerializeField] private Behaviour[] suspendDuringTakedown = new Behaviour[0];

        private const int WorldMask = 1 << PhysicsLayers.Default | 1 << PhysicsLayers.Level | 1 << PhysicsLayers.Interactable;
        private static readonly float[] StagingAngles = { 0f, 45f, -45f, 90f, -90f };
        private readonly Collider[] nearby = new Collider[32];
        private readonly TakedownCamera cameraMove = new TakedownCamera();
        private readonly TakedownPlayback playback = new TakedownPlayback();
        private static TakedownController current;
        private Health health;
        private CharacterController capsule;
        private bool[] enabledBefore;
        private ITakedownTarget target;
        private Component targetComponent;
        private GameObject actorRoot;
        private TakedownActor actor;
        private TakedownProfile activeProfile;
        private int stabSounds;
        private Vector3 playerStart, playerGoal;
        private Quaternion playerStartRotation, victimStartRotation, pairRotation;
        private float elapsed, duration, alignmentDuration, savedScale, appliedScale, savedFixedDelta;
        private bool impactApplied, soundPlayed, waitingForRelease;
        private CursorLockMode savedCursor;
        private bool savedCursorVisible;

        public Phase State { get; private set; }
        public bool IsBusy => State != Phase.Idle;
        public bool WaitingForRelease => waitingForRelease;
        public int ImpactCount { get; private set; }
        public int KickCount { get; private set; }
        public TakedownStyle Style { get; private set; }
        public string LastResult { get; private set; }
        public TakedownProfile Profile { get => profile; set { Cancel(); profile = value; } }
        public TakedownProfile FrontalProfile { get => frontalProfile; set { Cancel(); frontalProfile = value; } }
        public GameObject CinematicBody => actorRoot;
        public string SetupStatus
        {
            get
            {
                if (profile == null) return "Assign a takedown profile.";
                string missing = profile.MissingRequirement;
                if (missing != null) return missing;
                if (cinematicActorPrefab == null) return "Assign a full-body cinematic actor prefab.";
                var body = cinematicActorPrefab.GetComponentInChildren<TakedownActor>(true);
                if (body == null || body.GetComponent<Animator>() == null) return "The full-body prefab needs TakedownActor on its Animator object.";
                return Compatible(body.GetComponent<Animator>(), profile.playerAttack) ? "Ready" : "The player clip needs a valid Humanoid avatar.";
            }
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            capsule = GetComponent<CharacterController>();
            enabledBefore = new bool[suspendDuringTakedown.Length];
        }

        private void OnEnable() { if (health != null) health.OnDamaged += OnPlayerDamaged; }
        private void OnDisable() { if (health != null) health.OnDamaged -= OnPlayerDamaged; Cancel(); }
        private void OnDestroy() { Cancel(); if (actorRoot != null) Destroy(actorRoot); }
        private void OnPlayerDamaged(DamageInfo _)
        {
            // A frontal counter starts under gunfire. Nonlethal hits still reduce Health,
            // but keep the cinematic running; player death must release control.
            if (IsBusy && (Style != TakedownStyle.Frontal || !health.IsAlive))
                Abort(health.IsAlive ? "Interrupted by player damage." : "Interrupted by player death.");
        }
        public object CaptureCheckpoint() => null; // Cinematic ownership is transient, never checkpoint progress.
        public void RestoreCheckpoint(object _) => Cancel();
        [ContextMenu("Validate Takedown Setup")]
        private void ValidateSetup() => Debug.Log("[Takedown] " + SetupStatus, this);

        public bool TryBegin()
        {
            if (!isActiveAndEnabled || IsBusy || current != null) return Reject("Another takedown owns control, or this controller is disabled.");
            if (waitingForRelease) return Reject("Release attack before starting another takedown.");
            string setup = SetupStatus;
            if (setup != "Ready") return Reject(setup);
            if (health == null || !health.IsAlive || Time.timeScale <= 0f || input == null || !input.isActiveAndEnabled)
                return Reject("Player is dead, paused or has disabled input.");
            if (motor == null || !motor.enabled || !motor.Grounded || motor.Stance != Stance.Standing)
                return Reject("Stand upright on the ground to start a takedown.");
            if (weapons == null || !weapons.CanStartTakedown) return Reject("Equip a ready knife and wait for its cooldown.");
            if (hands == null || playerCamera == null || capsule == null || !capsule.enabled)
                return Reject("Player hands, camera or enabled CharacterController are missing.");
            LastResult = "No eligible living enemy within range and in front of the player.";

            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.8f, profile.range + 0.5f,
                nearby, 1 << PhysicsLayers.Enemy | 1 << PhysicsLayers.Hit, QueryTriggerInteraction.Ignore);
            ITakedownTarget chosen = null;
            TakedownProfile chosenProfile = null;
            float best = profile.range * profile.range;
            Vector3 goal = default;
            Quaternion facing = default;
            for (int i = 0; i < count; i++)
            {
                var candidate = nearby[i].GetComponentInParent<ITakedownTarget>();
                if (candidate == null || !candidate.Available) continue;
                Vector3 delta = candidate.Root.position - transform.position;
                float distance = delta.sqrMagnitude;
                if (distance > best || distance < 0.01f || Mathf.Abs(delta.y) > 0.35f) continue;
                Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up).normalized;
                if (Vector3.Dot(transform.forward, flat) < 0.5f) continue;
                float enemyFacing = Vector3.Dot(Vector3.ProjectOnPlane(candidate.Root.forward, Vector3.up).normalized, -flat);
                TakedownProfile settings;
                if (enemyFacing <= -0.5f) settings = profile;
                else if (candidate is ITakedownCombatTarget)
                {
                    settings = frontalProfile;
                    if (settings == null || settings.MissingRequirement != null)
                    { Reject("Frontal kick/stab clips are missing or invalid."); continue; }
                }
                else { Reject("This enemy has no frontal takedown adapter."); continue; }
                Vector3 staging = settings.style == TakedownStyle.Frontal ? -flat : flat;
                if (!TryStage(candidate, staging, settings, out Vector3 candidateGoal, out Quaternion candidateFacing)) continue;
                chosen = candidate;
                chosenProfile = settings;
                best = distance;
                goal = candidateGoal;
                facing = candidateFacing;
            }
            if (chosen == null) return false;
            if (!EnsureActor() || !Compatible(actor.Animator, chosenProfile.playerAttack)
                || !Compatible(chosen.Animator, chosenProfile.victimReaction)) return Reject("The paired clips and actor avatars are incompatible.");
            if (!chosen.TryReserve(this)) return Reject("The enemy is no longer available.");

            current = this;
            target = chosen;
            activeProfile = chosenProfile;
            Style = activeProfile.style;
            if (Style == TakedownStyle.Frontal) ((ITakedownCombatTarget)target).PrepareFrontal(this);
            targetComponent = chosen as Component;
            playerStart = transform.position;
            playerGoal = goal;
            alignmentDuration = Mathf.Max(activeProfile.alignmentTime, Vector3.Distance(playerStart, playerGoal) / 3f);
            playerStartRotation = transform.rotation;
            victimStartRotation = target.Root.rotation;
            pairRotation = facing;
            savedCursor = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            savedScale = Time.timeScale;
            savedFixedDelta = Time.fixedDeltaTime;
            appliedScale = savedScale;
            impactApplied = soundPlayed = false;
            ImpactCount = 0;
            KickCount = stabSounds = 0;
            elapsed = 0f;
            State = Phase.Aligning;
            weapons.StartTakedownCooldown();
            motor.StopMotion();
            for (int i = 0; i < suspendDuringTakedown.Length; i++)
                if (suspendDuringTakedown[i] != null)
                {
                    enabledBefore[i] = suspendDuringTakedown[i].enabled;
                    suspendDuringTakedown[i].enabled = false;
                }
            hands.SetCinematicHidden(true);
            cameraMove.Begin(playerCamera); // Scope has now restored its normal FOV.
            actor.Owner = this;
            actorRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
            actorRoot.SetActive(true);
            LastResult = "Aligning";
            return true;
        }

        private bool Reject(string reason) { LastResult = reason; return false; }

        private bool TryStage(ITakedownTarget candidate, Vector3 approach, TakedownProfile settings, out Vector3 goal, out Quaternion facing)
        {
            // Keep the approach angle when it fits. On uneven ground a small turn can stage the
            // pair across the slope instead of forcing its authored motion straight up the hill.
            foreach (float angle in StagingAngles)
                if (CanStage(candidate, Quaternion.Euler(0f, angle, 0f) * approach, settings, out goal, out facing)) return true;
            goal = default;
            facing = default;
            return false;
        }

        private bool CanStage(ITakedownTarget candidate, Vector3 direction, TakedownProfile settings, out Vector3 goal, out Quaternion facing)
        {
            facing = Quaternion.LookRotation(direction, Vector3.up);
            goal = candidate.Root.position + facing * settings.playerOffset;
            if (Physics.Linecast(playerCamera.transform.position, candidate.Root.position + Vector3.up,
                out RaycastHit sight, WorldMask, QueryTriggerInteraction.Ignore)) return Reject("View blocked by " + sight.collider.name);
            if (!Physics.Raycast(goal + Vector3.up * 0.45f, Vector3.down, out RaycastHit floor, 0.9f, WorldMask,
                QueryTriggerInteraction.Ignore) || Vector3.Dot(floor.normal, Vector3.up) < 0.7f) return Reject("No suitable floor at the alignment position.");
            goal.y = floor.point.y;
            if (Mathf.Abs(goal.y - transform.position.y) > 0.35f) return Reject("Player and alignment floor heights differ by more than 0.35m.");
            float radius = Mathf.Max(0.1f, capsule.radius - 0.02f);
            Vector3 bottom = Vector3.up * (capsule.radius + 0.03f);
            Vector3 top = Vector3.up * (capsule.height - capsule.radius);
            if (Physics.CheckCapsule(goal + bottom, goal + top, radius, WorldMask, QueryTriggerInteraction.Ignore)) return Reject("The alignment capsule overlaps scenery.");
            Vector3 travel = goal - transform.position;
            if (travel.magnitude >= 0.01f && Physics.CapsuleCast(transform.position + bottom, transform.position + top,
                radius, travel.normalized, travel.magnitude, WorldMask, QueryTriggerInteraction.Ignore)) return Reject("Scenery blocks the player alignment path.");
            // Imported paired clips can move inside their visual rig while gameplay roots stay fixed.
            // Reserve a wide, level corridor for that motion before taking control.
            if (settings.forwardClearance > 0f)
            {
                Vector3 end = candidate.Root.position + direction * settings.forwardClearance;
                Vector3 corridor = end - goal;
                int steps = Mathf.Max(1, Mathf.CeilToInt(corridor.magnitude / 0.4f));
                for (int step = 0; step <= steps; step++)
                {
                    Vector3 point = goal + corridor * (step / (float)steps);
                    if (!Physics.Raycast(point + Vector3.up * 0.4f, Vector3.down, out RaycastHit ground, 0.8f,
                        WorldMask, QueryTriggerInteraction.Ignore)) return Reject("Animation corridor has no floor.");
                    if (Mathf.Abs(ground.point.y - goal.y) > 0.35f || Vector3.Dot(ground.normal, Vector3.up) < 0.7f)
                        return Reject("Animation corridor needs gentler ground (maximum height variation 0.35m).");
                    // Place each clearance capsule on its sampled floor. A horizontal sweep at the
                    // starting height mistakes a gentle uphill terrain surface for a wall.
                    if (Physics.CheckCapsule(ground.point + Vector3.up * 0.73f, ground.point + Vector3.up * 1.8f,
                        0.7f, WorldMask, QueryTriggerInteraction.Ignore)) return Reject("Animation corridor overlaps scenery.");
                }
            }
            return true;
        }

        private bool EnsureActor()
        {
            if (actorRoot != null) return actor != null && actor.Animator != null;
            actorRoot = Instantiate(cinematicActorPrefab, transform);
            actorRoot.name = "CinematicTakedownBody";
            actor = actorRoot.GetComponentInChildren<TakedownActor>(true);
            actorRoot.SetActive(false);
            return actor != null && actor.Animator != null;
        }

        private static bool Compatible(Animator animator, AnimationClip clip) => animator != null
            && (!clip.humanMotion || (animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman));

        private void LateUpdate()
        {
            if (!IsBusy)
            {
                if (waitingForRelease && input != null && !input.FireHeld) waitingForRelease = false;
                return;
            }
            if (health == null || !health.IsAlive || activeProfile == null || actorRoot == null || actor == null || !actorRoot.activeInHierarchy
                || playerCamera == null || targetComponent == null || target.Health == null || !target.IsReservedBy(this) || !input.isActiveAndEnabled
                || (State != Phase.Returning && !impactApplied && !target.Health.IsAlive)) { Abort("Player, actor, camera, input or reserved enemy became unavailable."); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // Pause freezes staging, native Animator playback and camera motion together.
            elapsed += dt;
            if (State == Phase.Aligning)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / alignmentDuration));
                capsule.Move(Vector3.Lerp(playerStart, playerGoal, t) - transform.position);
                transform.rotation = Quaternion.Slerp(playerStartRotation, pairRotation * Quaternion.Euler(0f, activeProfile.playerYaw, 0f), t);
                target.Root.rotation = Quaternion.Slerp(victimStartRotation, pairRotation, t);
                actorRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
                cameraMove.Frame(target.Root.position, pairRotation, activeProfile, t, 0f);
                if (t >= 1f)
                {
                    Vector3 error = transform.position - playerGoal;
                    float verticalTolerance = Mathf.Max(0.08f, capsule.skinWidth + 0.02f);
                    // The capsule rests above terrain by its skin width. That is normal contact,
                    // not a blocked alignment; horizontal displacement must still reach the goal.
                    if (new Vector2(error.x, error.z).sqrMagnitude > 0.08f * 0.08f || Mathf.Abs(error.y) > verticalTolerance)
                    {
                        Abort($"Alignment did not reach its goal: actual {transform.position:F3}, goal {playerGoal:F3}.");
                        return;
                    }
                    State = Phase.Playing;
                    elapsed = 0f;
                    duration = activeProfile.Duration;
                    appliedScale = savedScale * activeProfile.timeScale;
                    Time.timeScale = appliedScale;
                    Time.fixedDeltaTime = savedFixedDelta * activeProfile.timeScale;
                    playback.Begin(actor.Animator, target.Animator, activeProfile);
                }
            }
            else if (State == Phase.Playing)
            {
                playback.Tick(elapsed, dt);
                cameraMove.Frame(target.Root.position, pairRotation, activeProfile, 1f, Mathf.Clamp01(elapsed / duration));
                if (elapsed >= duration + 0.1f)
                {
                    LastResult = impactApplied ? "Completed" : ImpactCount > 0 ? "Sequence ended without a confirmed finishing hit" : "No confirmed impact; no damage applied";
                    State = Phase.Returning;
                    elapsed = 0f;
                    cameraMove.BeginReturn();
                }
            }
            else
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / activeProfile.cameraReturnTime));
                cameraMove.Return(t);
                if (t >= 1f) Finish();
            }
        }

        public void ApplyImpact()
        {
            if (Style != TakedownStyle.Rear || State != Phase.Playing || impactApplied || targetComponent == null || !target.IsReservedBy(this)
                || !target.Health.IsAlive || target.Health.IsInvulnerable) return;
            impactApplied = true; // Set before damage callbacks: one death, pickup and kill report per session.
            ImpactCount++;
            weapons.ApplyTakedownDamage(target.Health, target.Root.position + Vector3.up);
        }

        public void PlayAttackSound()
        {
            if (State != Phase.Playing || soundPlayed) return;
            soundPlayed = true;
            weapons.PublishTakedownNoise(); // Existing noise -> pooled knife audio, and AI hearing.
        }

        public void KickWeapon()
        {
            if (Style != TakedownStyle.Frontal || State != Phase.Playing || KickCount != 0 || targetComponent == null || !target.IsReservedBy(this)) return;
            KickCount = 1;
            ((ITakedownCombatTarget)target).KickWeapon(this, transform.right + Vector3.up * 0.4f);
            playback.React(false);
            weapons.PublishTakedownNoise();
        }

        public void ApplyStab(int index)
        {
            if (Style != TakedownStyle.Frontal || State != Phase.Playing || KickCount != 1 || index != ImpactCount + 1 || index > 2
                || targetComponent == null || !target.IsReservedBy(this) || !target.Health.IsAlive || target.Health.IsInvulnerable) return;
            ImpactCount = index;
            impactApplied = index == 2;
            playback.React(impactApplied);
            weapons.ApplyTakedownDamage(target.Health, target.Root.position + Vector3.up, index == 1 ? target.Health.Current * 0.5f : target.Health.Current);
        }

        public void PlayStabSound(int index)
        {
            if (Style != TakedownStyle.Frontal || State != Phase.Playing || index != stabSounds + 1 || index > 2) return;
            stabSounds = index;
            weapons.PublishTakedownNoise();
        }

        public void Cancel()
        {
            if (!IsBusy) return;
            LastResult = "Interrupted";
            Finish();
        }

        private void Abort(string reason) { Cancel(); LastResult = reason; }

        private void Finish()
        {
            if (!IsBusy) return;
            bool collapseFinished = State == Phase.Returning;
            State = Phase.Idle;
            if (actor != null) actor.Owner = null;
            bool victimDead = targetComponent != null && !target.Health.IsAlive;
            playback.End(victimDead && collapseFinished);
            cameraMove.Restore();
            if (actorRoot != null) actorRoot.SetActive(false);
            if (targetComponent != null) target.Release(this);
            target = null;
            targetComponent = null;
            if (Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = savedScale;
            Time.fixedDeltaTime = savedFixedDelta;
            for (int i = 0; i < suspendDuringTakedown.Length; i++)
                if (suspendDuringTakedown[i] != null) suspendDuringTakedown[i].enabled = enabledBefore[i];
            if (motor != null) motor.StopMotion();
            if (hands != null) hands.SetCinematicHidden(false);
            Cursor.lockState = Time.timeScale <= 0f ? CursorLockMode.None : savedCursor;
            Cursor.visible = Time.timeScale <= 0f || savedCursorVisible;
            waitingForRelease = input != null && input.FireHeld; // A release during playback already satisfies the next-press rule.
            if (current == this) current = null;
        }
    }
}
