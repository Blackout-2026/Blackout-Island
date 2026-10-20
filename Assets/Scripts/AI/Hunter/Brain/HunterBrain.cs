// Owner: Systems Engineer (Hunter AI).
using System;
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
    public sealed class HunterBrain : MonoBehaviour, IAgentBrain, IDirectiveReceiver, IAlertReceiver
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

        public HunterConfig Config => config;
        public HunterVision Vision => vision;
        public HearingSystem Hearing => hearing;
        /// <summary>The root branch that ran on the last tick (for the H4 debug text).</summary>
        public string ActiveBranch => tree != null ? tree.ActiveBranch : "Idle";

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

            return tree.Tick(deltaTime);
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
