// Owner: Systems Engineer.
using Blackout.Shared;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Blackout.Interaction
{
    /// <summary>
    /// A sliding door: E opens or closes it, agents open it through <see cref="IDoor.RequestOpen"/>, and a locked
    /// door refuses both. The panel slides sideways into the wall over 1 / speed seconds (eased), and stops
    /// blocking once it is 80 % open. Every open and close publishes a Door noise. While locked, a NavMeshObstacle
    /// carves the doorway so agents path around it; an unlocked door stays walkable so agents can reach it.
    /// A double door (step D1) gives a second panel: it slides the same distance the other way, so the two leaves part.
    /// Optional fail-safe maglock: unlocks and opens itself a few seconds after an event (the lab door after
    /// GeneratorDisabled). Numbers: docs/reference/systems-engineer.md section 10.
    /// </summary>
    public class SlidingDoor : Interactable, IDoor, IDoorCombatLock, ICheckpointState
    {
        private const float PassableOpen = 0.8f;

        [Tooltip("Used in the prompt: \"Open <label>\".")]
        [SerializeField] private string label = "door";
        [Tooltip("The moving panel. It slides along its parent's X axis.")]
        [SerializeField] private Transform panel;
        [Tooltip("Optional second leaf of a double door: it slides the same distance along -X while the panel slides along +X.")]
        [SerializeField] private Transform mirrorPanel;
        [Tooltip("Collider that blocks the doorway while mostly closed.")]
        [SerializeField] private Collider blocker;
        [Tooltip("Metres the panel slides: the width plus 0.05 m.")]
        [SerializeField, Min(0.1f)] private float slideDistance = 1.45f;
        [Tooltip("Open fraction per second (prototype: 1.1, the heavy bunker door 0.45).")]
        [SerializeField, Min(0.01f)] private float speed = 1.1f;
        [Tooltip("Noise radius (m) when it opens or closes (prototype: 14, bunker door 22).")]
        [SerializeField, Min(0f)] private float noiseRadius = 14f;
        [SerializeField] private NoiseEventChannel noiseChannel;

        [Header("Lock")]
        [SerializeField] private bool startLocked;
        [Tooltip("Optional: unlock and open this long after this event (the lab's fail-safe maglock).")]
        [SerializeField] private VoidEventChannel unlockOn;
        [SerializeField, Min(0f)] private float unlockDelay = 2.6f;
        [Tooltip("Carves the NavMesh while the door is locked.")]
        [SerializeField] private NavMeshObstacle lockObstacle;
        [Tooltip("Optional lamp: red while locked, green when unlocked.")]
        [SerializeField] private Renderer lockLamp;
        [SerializeField] private Material lockedLampMaterial;
        [SerializeField] private Material unlockedLampMaterial;

        [Header("Audio hooks")]
        public UnityEvent onOpened = new UnityEvent();
        public UnityEvent onClosed = new UnityEvent();
        [Tooltip("The player pressed E on a locked door.")]
        public UnityEvent onLockedUsed = new UnityEvent();

        private Vector3 closedPosition;
        private Vector3 mirrorClosedPosition;
        private float open;
        private float target;
        private bool locked;
        private bool combatLocked;
        private float unlockTimer = -1f;
        private string openPrompt;
        private string closePrompt;
        private string lockedPrompt;

        public override string Prompt => combatLocked ? "Eliminate the remaining lab defenders" : locked ? lockedPrompt : target > 0.5f ? closePrompt : openPrompt;
        public override string Key => IsLocked ? string.Empty : "E";

        public Vector3 Position => transform.position;
        public bool IsOpen => open > PassableOpen;
        public bool IsLocked => locked || combatLocked;

        private struct Checkpoint
        {
            public float Open, Target, UnlockTimer;
            public bool Locked, CombatLocked;
        }

        public object CaptureCheckpoint() => new Checkpoint
        {
            Open = open, Target = target, UnlockTimer = unlockTimer, Locked = locked, CombatLocked = combatLocked
        };

        public void RestoreCheckpoint(object state)
        {
            var saved = (Checkpoint)state;
            open = saved.Open;
            target = saved.Target;
            unlockTimer = saved.UnlockTimer;
            locked = saved.Locked;
            combatLocked = saved.CombatLocked;
            float slide = slideDistance * Mathf.SmoothStep(0f, 1f, open);
            if (panel != null) panel.localPosition = closedPosition + Vector3.right * slide;
            if (mirrorPanel != null) mirrorPanel.localPosition = mirrorClosedPosition + Vector3.left * slide;
            if (blocker != null) blocker.enabled = IsLocked || open < PassableOpen;
            ApplyLock(); // no opening sounds or noises during restoration
        }

        private void Awake()
        {
            // Built once, so the prompt never allocates.
            openPrompt = "Open " + label;
            closePrompt = "Close " + label;
            lockedPrompt = unlockOn != null ? "Locked — magnetic lock powered" : "Locked";
            if (panel != null)
            {
                closedPosition = panel.localPosition;
            }

            if (mirrorPanel != null)
            {
                mirrorClosedPosition = mirrorPanel.localPosition;
            }

            SetLocked(startLocked);
        }

        private void OnEnable()
        {
            if (unlockOn != null)
            {
                unlockOn.OnRaised += StartUnlockTimer;
            }
        }

        private void OnDisable()
        {
            if (unlockOn != null)
            {
                unlockOn.OnRaised -= StartUnlockTimer;
            }
        }

        public override void Interact(GameObject by)
        {
            if (IsLocked)
            {
                onLockedUsed.Invoke();
                return;
            }

            SetTarget(target > 0.5f ? 0f : 1f, by);
        }

        public bool RequestOpen(GameObject by)
        {
            if (IsLocked || target > 0.5f)
            {
                return false;
            }

            SetTarget(1f, by);
            return true;
        }

        public void SetLocked(bool value)
        {
            locked = value;
            ApplyLock();
        }

        public void SetCombatLocked(bool value)
        {
            if (combatLocked == value) return;
            combatLocked = value;
            if (value)
            {
                // Close immediately so the player cannot slip through while the panel is sliding.
                target = open = 0f;
                if (panel != null) panel.localPosition = closedPosition;
                if (mirrorPanel != null) mirrorPanel.localPosition = mirrorClosedPosition;
                if (blocker != null) blocker.enabled = true;
            }
            ApplyLock();
            if (!value && !IsLocked) RequestOpen(null);
        }

        private void ApplyLock()
        {
            if (lockObstacle != null)
            {
                lockObstacle.enabled = IsLocked;
            }

            if (lockLamp != null)
            {
                Material lamp = IsLocked ? lockedLampMaterial : unlockedLampMaterial;
                if (lamp != null)
                {
                    lockLamp.sharedMaterial = lamp;
                }
            }
        }

        private void StartUnlockTimer()
        {
            unlockTimer = unlockDelay;
        }

        private void SetTarget(float value, GameObject by)
        {
            target = value;
            if (noiseChannel != null && noiseRadius > 0f)
            {
                noiseChannel.Raise(new NoiseEvent(transform.position, noiseRadius, NoiseType.Door, by));
            }

            (value > 0.5f ? onOpened : onClosed).Invoke();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (unlockTimer > 0f)
            {
                unlockTimer -= dt;
                if (unlockTimer <= 0f)
                {
                    SetLocked(false);
                    RequestOpen(null); // the system opens it, not a person
                }
            }

            if (Mathf.Approximately(open, target))
            {
                return;
            }

            open = Mathf.MoveTowards(open, target, speed * dt);
            float slide = slideDistance * Mathf.SmoothStep(0f, 1f, open);
            if (panel != null)
            {
                panel.localPosition = closedPosition + Vector3.right * slide;
            }

            if (mirrorPanel != null)
            {
                mirrorPanel.localPosition = mirrorClosedPosition + Vector3.left * slide;
            }

            if (blocker != null)
            {
                blocker.enabled = open < PassableOpen;
            }
        }
    }
}
