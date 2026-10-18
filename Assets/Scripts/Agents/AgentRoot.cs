// Owner: Agent Controller.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// The one component that ties an agent together. Every think tick it asks the brain for an AgentIntent and
    /// applies it through the mover, the animator and the shooter. The brain never touches the Transform, the
    /// NavMesh or the Animator, and the three executors never decide anything, so an AI can be swapped or tested
    /// without the visuals and the visuals without an AI. Mirrors the prototype's agent_base.js update order
    /// (think at 10 Hz with a random offset per agent, executors every frame).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class AgentRoot : MonoBehaviour, IAgentInfo
    {
        [Tooltip("Unique name such as Patrol-1. Used on the Blackboard and as the Owner of this agent's markers. Empty uses the object name.")]
        [SerializeField] private string agentName;
        [Tooltip("A component implementing IAgentBrain (FSM, behaviour tree, utility or GOAP). Empty means the agent stands still.")]
        [SerializeField] private MonoBehaviour brainBehaviour;
        [Tooltip("Seconds between brain decisions. The prototype thinks at 10 Hz.")]
        [SerializeField, Min(0.02f)] private float thinkInterval = 0.1f;
        [Tooltip("Seconds between state-text updates on the Blackboard (building the text allocates, so it is slower than thinking).")]
        [SerializeField, Min(0.1f)] private float stateInterval = 0.5f;

        private Health health;
        private IAgentBrain brain;
        private IAgentMover mover;
        private IAgentFacing facing;
        private IAgentAnimator animator;
        private IAgentShooter shooter;
        private AgentRegistry registry;
        private Blackboard alertBoard;
        private PathFollower pathFollower;
        private AgentShooter agentShooter;

        private float thinkTimer;
        private float thinkElapsed;
        private float stateTimer;

        public string AgentName => string.IsNullOrEmpty(agentName) ? name : agentName;
        public Transform Transform => transform;
        public bool IsAlive => health != null && health.IsAlive;
        public IDirectiveReceiver DirectiveReceiver => brain as IDirectiveReceiver;
        public float VisionRangeMultiplier { get; private set; } = 1f;
        /// <summary>The intent most recently applied, for gizmos and the debug overlay.</summary>
        public AgentIntent LastIntent { get; private set; }

        private void Awake()
        {
            Configure(
                brainBehaviour as IAgentBrain ?? GetComponent<IAgentBrain>(),
                GetComponent<IAgentMover>(),
                GetComponentInChildren<IAgentAnimator>(true),
                GetComponentInChildren<IAgentShooter>(true));
        }

        private void Start()
        {
            Initialize(AgentRegistry.Current != null ? AgentRegistry.Current : FindFirstObjectByType<AgentRegistry>());
            thinkTimer = Random.value * thinkInterval; // stagger agents so they do not all think in the same frame
        }

        private void OnDestroy()
        {
            Unsubscribe();
            UnregisterFromRegistry();
        }

        private void OnEnable()
        {
            if (registry != null) BindAlert();
        }

        private void OnDisable() => UnbindAlert();

        /// <summary>
        /// Connects the brain and the three executors (any may be null while their steps are not built yet).
        /// Awake calls it; it is public so tests can plug in fakes.
        /// </summary>
        public void Configure(IAgentBrain newBrain, IAgentMover newMover, IAgentAnimator newAnimator, IAgentShooter newShooter)
        {
            Unsubscribe();
            health = GetComponent<Health>();
            brain = newBrain;
            mover = newMover;
            facing = newMover as IAgentFacing;
            animator = newAnimator;
            shooter = newShooter;
            pathFollower = newMover as PathFollower;
            agentShooter = newShooter as AgentShooter;
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
            if (registry != null)
            {
                registry.Register(this);
                BindAlert();
            }
        }

        /// <summary>Registers with the registry and hands the brain its context. Called once, from Start.</summary>
        public void Initialize(AgentRegistry agentRegistry)
        {
            registry = agentRegistry;
            if (registry == null)
            {
                Debug.LogWarning($"{name}: no AgentRegistry in the loaded scenes, so the brain cannot start.", this);
                brain = null;
                return;
            }

            registry.Register(this);
            brain?.Initialize(registry.CreateContext(transform, health));
            BindAlert();
        }

        /// <summary>
        /// Puts a brain on an agent after it was created, which is how the spawner gives a prefab that has none a
        /// placeholder brain. If the agent has already started, the brain is initialised straight away.
        /// </summary>
        public void SetBrain(IAgentBrain newBrain)
        {
            brain = newBrain;
            if (registry != null)
            {
                registry.Register(this); // refresh the Blackboard's receiver without duplicating the agent
                brain?.Initialize(registry.CreateContext(transform, health));
                if (registry.Blackboard.HasAlertBuff) ApplyAlert(registry.Blackboard.Alert);
                var members = registry.Blackboard.Agents;
                for (int i = 0; i < members.Count; i++)
                    if (members[i].Transform == transform && members[i].HasDirective && IsAlive)
                        DirectiveReceiver?.OnDirective(members[i].LastDirective);
            }
        }

        private void BindAlert()
        {
            UnbindAlert();
            if (!isActiveAndEnabled || registry == null)
                return;
            alertBoard = registry.Blackboard;
            alertBoard.AlertChanged += ApplyAlert;
            if (alertBoard.HasAlertBuff) ApplyAlert(alertBoard.Alert);
        }

        private void UnbindAlert()
        {
            if (alertBoard == null) return;
            alertBoard.AlertChanged -= ApplyAlert;
            alertBoard = null;
        }

        private void ApplyAlert(AgentAlert alert)
        {
            if (!IsAlive) return;
            VisionRangeMultiplier = alert.visionMultiplier;
            if (pathFollower != null) pathFollower.SpeedMultiplier = alert.speedMultiplier;
            if (agentShooter != null)
            {
                agentShooter.Heightened = true;
                agentShooter.HeightenedAimErrorMultiplier = alert.aimErrorMultiplier;
            }
            if (brain is IAlertReceiver receiver) receiver.OnAlert(alert);
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            float dt = Time.deltaTime;
            thinkElapsed += dt;
            thinkTimer -= dt;
            if (thinkTimer <= 0f)
            {
                Think(thinkElapsed);
                thinkElapsed = 0f;
                thinkTimer += thinkInterval;
            }

            stateTimer -= dt;
            if (stateTimer <= 0f)
            {
                stateTimer = stateInterval;
                PublishState();
            }
        }

        /// <summary>One decision: ask the brain for an intent and apply it. Without a brain the agent stands still.</summary>
        public void Think(float deltaTime)
        {
            ApplyIntent(brain != null ? brain.Tick(deltaTime) : AgentIntent.Idle);
        }

        /// <summary>Carries an intent out through the mover, the animator and the shooter. Does nothing once dead.</summary>
        public void ApplyIntent(in AgentIntent intent)
        {
            LastIntent = intent;
            if (!IsAlive)
            {
                return;
            }

            if (mover != null)
            {
                if (intent.moveSpeed == MoveSpeed.Stop)
                {
                    mover.Stop();
                }
                else
                {
                    mover.SetDestination(intent.moveTarget, intent.moveSpeed);
                }
            }

            facing?.SetFacing(intent.hasLookTarget, intent.lookTarget, intent.aim);
            animator?.Apply(intent);
            shooter?.Apply(intent);
        }

        private void PublishState()
        {
            if (registry != null && brain is IDebugInfo info)
            {
                registry.SetState(this, info.GetDebugText());
            }
        }

        private void HandleDamaged(DamageInfo info)
        {
            animator?.PlayHit(info);
        }

        private void HandleDied()
        {
            UnbindAlert();
            mover?.Stop();
            shooter?.CeaseFire();
            animator?.PlayDeath();
            UnregisterFromRegistry();
        }

        // Explicit null check: a registry destroyed during scene unload is Unity-null, which ?. does not detect.
        private void UnregisterFromRegistry()
        {
            if (registry != null)
            {
                registry.Unregister(this);
            }
        }

        private void Unsubscribe()
        {
            UnbindAlert();
            if (health != null)
            {
                health.OnDamaged -= HandleDamaged;
                health.OnDied -= HandleDied;
            }
        }
    }
}
