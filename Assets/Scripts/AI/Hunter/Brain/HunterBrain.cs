// Owner: Systems Engineer (Hunter AI).
using System;
using System.Globalization;
using System.Text;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// The Hunter's decision layer (docs/reference/systems-engineer.md section 15). Every think tick (10 Hz, from
    /// AgentRoot) it senses: its eyes (<see cref="HunterVision"/>, with camouflage) and its ears (the
    /// <see cref="HearingSystem"/> beside it). It writes what it sensed into the tree's blackboard
    /// (<see cref="HunterFacts"/>), ticks the <see cref="HunterTree"/> and returns the AgentIntent the tree built. It
    /// never moves, animates or shoots: AgentRoot's mover, animator and shooter do that, including the shooter's
    /// reaction delay and aim error. It also takes the Commander's orders and alert, and reports sightings to the
    /// squad Blackboard.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HunterBrain : MonoBehaviour, IAgentBrain, IDirectiveReceiver, IAlertReceiver, IDebugInfo
    {
        [Tooltip("Every number of the Hunter's eyes and tree. Empty falls back to the defaults (and logs a warning).")]
        [SerializeField] private HunterConfig config;
        [Tooltip("The Hunter's ears. Empty uses the HearingSystem on this GameObject; without one it hears nothing.")]
        [SerializeField] private HearingSystem hearing;

        private static readonly Func<float> DefaultClock = () => Time.time;

        private Func<float> clock = DefaultClock;
        private IAgentContext context;
        private ISightCheck sight;
        private IHunterWorld world;
        private HunterVision vision;
        private HunterTree tree;
        private HunterConfig fallbackConfig;
        private float rangeMultiplier = 1f;

        private Transform trackedPlayer;
        private Health playerHealth;
        private Vector3 lastPlayerPosition;
        private bool hasLastPlayerPosition;

        private bool hasOrder;
        private Directive order;
        private float orderReceivedAt = float.NegativeInfinity;

        // Debug text (H4): rebuilt at most once per tick, and only when asked.
        private readonly StringBuilder debugBuilder = new StringBuilder(200);
        private string debugText = "Hunter [BT] (not started)";
        private int tickCount;
        private int textTick = -1;

        public HunterConfig Config => config;
        public HunterVision Vision => vision;
        public HearingSystem Hearing => hearing;
        /// <summary>The root branch that ran on the last tick (Attack, Investigate, Roam...), or "Idle".</summary>
        public string ActiveBranch => tree != null ? tree.ActiveBranch : "Idle";
        /// <summary>The intent returned by the last tick (for the gizmos).</summary>
        public AgentIntent LastIntent { get; private set; }
        /// <summary>The tree, for the debug text and the gizmos. Null before Initialize.</summary>
        public HunterTree Tree => tree;

        // Tests (and later the Agent Controller) plug these in before Initialize.
        public void SetConfig(HunterConfig newConfig) => config = newConfig;
        public void SetHearing(HearingSystem newHearing) => hearing = newHearing;
        public void SetClock(Func<float> newClock) => clock = newClock ?? DefaultClock;
        public void SetSight(ISightCheck newSight) => sight = newSight;
        public void SetWorld(IHunterWorld newWorld) => world = newWorld;

        public void Initialize(IAgentContext newContext)
        {
            context = newContext;
            if (config == null)
            {
                Debug.LogWarning($"{name}: no HunterConfig assigned, using the built-in defaults.", this);
                fallbackConfig = ScriptableObject.CreateInstance<HunterConfig>();
                fallbackConfig.hideFlags = HideFlags.HideAndDontSave;
                config = fallbackConfig;
            }

            if (hearing == null)
            {
                hearing = GetComponent<HearingSystem>();
            }

            sight ??= new RaycastSightCheck(config.sightBlockers);
            world ??= new HunterWorld(context, config);
            vision = new HunterVision(config) { RangeMultiplier = rangeMultiplier };
            tree = new HunterTree(config, world);
            trackedPlayer = null;
            playerHealth = null;
            hasLastPlayerPosition = false;
        }

        public AgentIntent Tick(float deltaTime)
        {
            if (context == null || tree == null || context.Self == null)
            {
                return AgentIntent.Idle;
            }

            float now = clock();
            Transform self = context.Self;
            LocalBlackboard facts = tree.Facts;

            See(self, facts, now, deltaTime);
            facts.Set(HunterFacts.Now, now);
            facts.Set(HunterFacts.Self, self.position);
            facts.Set(HunterFacts.CanSeePlayer, vision.CanSee);
            facts.Set(HunterFacts.PlayerDetected, vision.Detected);
            facts.Set(HunterFacts.LastSeenTime, vision.LastSeenTime);
            facts.Set(HunterFacts.LastSeenPosition, vision.LastSeenPosition);
            facts.Set(HunterFacts.RangeMultiplier, rangeMultiplier);

            bool heard = hearing != null && hearing.HasNoise;
            facts.Set(HunterFacts.HasNoise, heard);
            if (heard)
            {
                facts.Set(HunterFacts.HeardNoiseType, hearing.NoiseType);
                facts.Set(HunterFacts.NoisePosition, hearing.NoisePosition);
                facts.Set(HunterFacts.NoiseLoudness, hearing.HeardLoudness);
                facts.Set(HunterFacts.NoiseHeardAt, now - hearing.TimeSinceNoise);
            }

            facts.Set(HunterFacts.HasOrder, hasOrder);
            if (hasOrder)
            {
                facts.Set(HunterFacts.Order, order);
                facts.Set(HunterFacts.OrderReceivedAt, orderReceivedAt);
            }

            LastIntent = tree.Tick(deltaTime);
            tickCount++;
            return LastIntent;
        }

        /// <summary>
        /// Four lines (docs/reference/systems-engineer.md section 16): the running branch path, the eyes, the ears and
        /// the Commander's order. AgentRoot publishes it to the squad Blackboard every 0.5 s. It is rebuilt at most once
        /// per think tick, so asking every frame allocates at most 10 strings a second. If something parses this text,
        /// read only the first line.
        /// </summary>
        public string GetDebugText()
        {
            if (tree == null || tickCount == 0 || textTick == tickCount)
            {
                return debugText;
            }

            float now = clock();
            StringBuilder text = debugBuilder.Clear();
            text.Append("Hunter [BT] ");
            if (!BehaviourTreeDebug.AppendRunningPath(tree.Root, text))
            {
                text.Append("Idle");
            }

            text.Append("\neyes: sees ").Append(vision.CanSee ? "yes" : "no")
                .Append(", detected ").Append(vision.Detected ? "yes" : "no")
                .Append(", awareness ").Append(Number(vision.Awareness));
            text.Append(float.IsNegativeInfinity(vision.LastSeenTime)
                ? ", never seen"
                : ", last seen " + Number(now - vision.LastSeenTime) + " s ago");

            text.Append("\nears: ");
            if (hearing != null && hearing.HasNoise)
            {
                int walls = hearing.NoiseWalls;
                text.Append(hearing.NoiseType.ToString()).Append(' ').Append(Number(hearing.HeardLoudness))
                    .Append(" heard ").Append(Number(hearing.TimeSinceNoise)).Append(" s ago through ")
                    .Append(walls).Append(walls == 1 ? " wall" : " walls");
            }
            else
            {
                text.Append(hearing != null ? "nothing" : "no HearingSystem");
            }

            text.Append("\norder: ");
            if (hasOrder)
            {
                bool done = tree.Facts.Get(HunterFacts.OrderHandledAt, float.NegativeInfinity) >= orderReceivedAt;
                text.Append(order.type.ToString()).Append(" (").Append(Number(order.position.x)).Append(", ")
                    .Append(Number(order.position.z)).Append(done ? ") done" : ")");
            }
            else
            {
                text.Append("none");
            }

            debugText = text.ToString();
            textTick = tickCount;
            return debugText;
        }

        private static string Number(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>The Commander's order. Kept until the tree completes it; a newer order replaces it.</summary>
        public void OnDirective(Directive directive)
        {
            order = directive;
            hasOrder = true;
            orderReceivedAt = clock();
        }

        /// <summary>The Commander's alert: sight and attack range x visionMultiplier (1.4 by default).</summary>
        public void OnAlert(AgentAlert alert)
        {
            rangeMultiplier = alert.visionMultiplier;
            if (vision != null)
            {
                vision.RangeMultiplier = rangeMultiplier;
            }
        }

        private void See(Transform self, LocalBlackboard facts, float now, float deltaTime)
        {
            Transform player = context.Player;
            if (player != trackedPlayer)
            {
                trackedPlayer = player;
                playerHealth = player != null ? player.GetComponentInParent<Health>() : null;
                hasLastPlayerPosition = false;
            }

            if (player == null)
            {
                vision.Update(self.position, self.forward, self.position, false, 0f, sight, now, deltaTime);
                facts.Set(HunterFacts.PlayerPosition, self.position);
                return;
            }

            Vector3 position = player.position;
            // Speed from the movement since the last tick. On the first tick there is no last position, so count the
            // player as walking (no camouflage, no sprint bonus) rather than guessing either way.
            float speed = hasLastPlayerPosition && deltaTime > 0f
                ? CoverMath.FlatDistance(position, lastPlayerPosition) / deltaTime
                : (config.stillSpeed + config.sprintSpeed) * 0.5f;
            lastPlayerPosition = position;
            hasLastPlayerPosition = true;

            bool alive = playerHealth == null || playerHealth.IsAlive;
            vision.Update(self.position, self.forward, position, alive, speed, sight, now, deltaTime);
            facts.Set(HunterFacts.PlayerPosition, position);

            // A fresh sighting goes to the squad, as the prototype's reportSighting does.
            if (vision.Detected && vision.LastSeenTime == now && context.Blackboard != null)
            {
                context.Blackboard.lastKnownPlayerPos = vision.LastSeenPosition;
                context.Blackboard.lastKnownPlayerTime = now;
            }
        }

        /// <summary>
        /// Selected Hunter only (docs/reference/systems-engineer.md section 16): the vision cone coloured by state, the
        /// current target (move target, and the look target while aiming), the last sighting and the route. Drawn only
        /// once the brain has started; the noise rings are drawn by the <see cref="HearingSystem"/>.
        /// </summary>
        private void OnDrawGizmosSelected() => DrawGizmos(UnityGizmoPainter.Instance);

        /// <summary>The brain's gizmos, drawn with <paramref name="painter"/> (tests record them).</summary>
        public void DrawGizmos(IGizmoPainter painter)
        {
            if (vision == null || context == null || context.Self == null)
            {
                return;
            }

            Transform self = context.Self;
            Vector3 floor = self.position + Vector3.up * 0.1f;
            Vector3 eye = self.position + Vector3.up * config.eyeHeight;

            // Vision cone: the two edges and the arc at the sight range, and the close-sense circle, in the state colour.
            painter.Color = HunterGizmos.ForBranch(ActiveBranch);
            float half = config.fieldOfViewDegrees * 0.5f * Mathf.Deg2Rad;
            float facing = Mathf.Atan2(self.forward.x, self.forward.z);
            painter.Line(floor, floor + HunterGizmos.Direction(facing - half) * vision.Range);
            painter.Line(floor, floor + HunterGizmos.Direction(facing + half) * vision.Range);
            HunterGizmos.Arc(painter, floor, self.forward, config.fieldOfViewDegrees, vision.Range);
            HunterGizmos.Circle(painter, floor, config.closeSenseRange);

            // Current target.
            AgentIntent intent = LastIntent;
            if (intent.moveSpeed != MoveSpeed.Stop)
            {
                painter.Color = intent.moveSpeed == MoveSpeed.Run ? HunterGizmos.Running : HunterGizmos.Walking;
                painter.Line(floor, intent.moveTarget + Vector3.up * 0.1f);
                painter.WireSphere(intent.moveTarget, 0.3f);
            }

            if (intent.aim && intent.hasLookTarget)
            {
                painter.Color = HunterGizmos.Combat;
                painter.Line(eye, intent.lookTarget);
                painter.WireSphere(intent.lookTarget, 0.15f);
            }

            // Last sighting, while it is still searchable.
            if (vision.HasSighting && clock() - vision.LastSeenTime < config.searchMemorySeconds)
            {
                painter.Color = Color.yellow;
                painter.WireCube(vision.LastSeenPosition + Vector3.up * 0.9f, new Vector3(0.5f, 1.8f, 0.5f));
            }

            // Route, in order and closed.
            int count = world != null ? world.WaypointCount : 0;
            painter.Color = HunterGizmos.Route;
            for (int i = 0; i < count; i++)
            {
                Vector3 from = world.GetWaypoint(i) + Vector3.up * 0.2f;
                painter.Line(from, world.GetWaypoint((i + 1) % count) + Vector3.up * 0.2f);
                painter.WireSphere(from, 0.25f);
            }
        }

        private void OnDestroy()
        {
            if (fallbackConfig == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(fallbackConfig);
            }
            else
            {
                DestroyImmediate(fallbackConfig);
            }
        }
    }
}
