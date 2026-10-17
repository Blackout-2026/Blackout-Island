// Owner: Core Developer (Tactical Guard AI).
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>
    /// The Tactical Guard's decision layer (utility AI). Each tick it measures a <see cref="GuardSituation"/>, lets the
    /// <see cref="GuardActionScorer"/> pick Attack, MoveToCover, Reposition or HoldPost, and returns the matching
    /// AgentIntent. It reads the world and the Blackboard but never moves, animates or shoots; AgentRoot does that.
    /// </summary>
    public class TacticalGuardBrain : MonoBehaviour, IAgentBrain, IDebugInfo
    {
        [Tooltip("All weights and curves. Empty falls back to the built-in defaults (and logs a warning).")]
        [SerializeField] private TacticalGuardConfig config;
        [Tooltip("Layers that block the guard's sight. Empty (nothing) means the Level layer.")]
        [SerializeField] private LayerMask sightBlockers;

        private static readonly Func<float> DefaultClock = () => Time.time;

        private IAgentContext context;
        private GuardActionScorer scorer;
        private CoverSelector selector;
        private IGuardSensor sensor;
        private CoverClaims claims;
        private Func<float> clock = DefaultClock;
        private TacticalGuardConfig fallbackConfig;
        private Health subscribedHealth;

        private Vector3 post;
        private float lastDamageTime = float.NegativeInfinity;
        private Marker heldCover;

        /// <summary>The action chosen on the last tick.</summary>
        public GuardActionType CurrentAction => scorer != null ? scorer.CurrentAction : GuardActionType.HoldPost;
        /// <summary>The place this guard defends. Starts at its AgentSpawn marker; a Directive may move it.</summary>
        public Vector3 Post => post;
        /// <summary>The cover point this guard has claimed, or null.</summary>
        public Marker HeldCover => heldCover;
        public TacticalGuardConfig Config => config;

        // Tests (and later the Agent Controller) plug these in before Initialize.
        public void SetConfig(TacticalGuardConfig newConfig) => config = newConfig;
        public void SetSensor(IGuardSensor newSensor) => sensor = newSensor;
        public void SetClaims(CoverClaims newClaims) => claims = newClaims;
        public void SetClock(Func<float> newClock) => clock = newClock ?? DefaultClock;
        public void SetPost(Vector3 newPost) => post = newPost;

        public void Initialize(IAgentContext newContext)
        {
            Unsubscribe();
            ReleaseCover();

            context = newContext;
            if (config == null)
            {
                Debug.LogWarning($"{name}: no TacticalGuardConfig assigned, using the built-in defaults.", this);
                fallbackConfig = ScriptableObject.CreateInstance<TacticalGuardConfig>();
                fallbackConfig.hideFlags = HideFlags.HideAndDontSave;
                config = fallbackConfig;
            }

            sensor = sensor ?? new RaycastGuardSensor(sightBlockers.value != 0 ? sightBlockers : (LayerMask)LayerMask.GetMask("Level"));
            claims = claims ?? CoverClaims.Shared;
            scorer = new GuardActionScorer(config);
            selector = new CoverSelector(config, sensor, claims);

            post = FindPost();
            lastDamageTime = float.NegativeInfinity;

            if (context.Health != null)
            {
                subscribedHealth = context.Health;
                subscribedHealth.OnDamaged += HandleDamaged;
                subscribedHealth.OnDied += HandleDied;
            }
        }

        public AgentIntent Tick(float deltaTime)
        {
            if (context == null || scorer == null || context.Self == null)
            {
                return AgentIntent.Idle;
            }

            float now = clock();
            Vector3 self = context.Self.position;
            Transform player = context.Player;
            Blackboard board = context.Blackboard;

            GuardSituation situation = Sense(self, player, board, now, out Vector3 playerPos, out Vector3 playerChest);
            GuardActionType action = scorer.Choose(situation);

            // Cover is only held while fighting from it or running to it; going back to a post or searching frees it.
            if (action == GuardActionType.Reposition || action == GuardActionType.HoldPost)
            {
                ReleaseCover();
            }

            bool hasLastKnown = board != null && !float.IsNegativeInfinity(board.lastKnownPlayerTime);
            Vector3 lastKnown = hasLastKnown ? board.lastKnownPlayerPos : self;

            switch (action)
            {
                case GuardActionType.Attack:
                    return GuardIntentBuilder.Attack(playerChest);

                case GuardActionType.MoveToCover:
                    if (heldCover != null)
                    {
                        Vector3 coverPoint = heldCover.transform.position;
                        return GuardIntentBuilder.MoveToCover(coverPoint, GuardMath.FlatDistance(self, coverPoint),
                            situation.canSeePlayer, situation.canSeePlayer ? playerChest : lastKnown,
                            situation.canSeePlayer || hasLastKnown, config);
                    }
                    break;

                case GuardActionType.Reposition:
                    float limit = board != null && board.alertLevel > 0 ? config.repositionLimitAlert : config.repositionLimit;
                    Vector3 target = GuardMath.RepositionTarget(post, lastKnown, limit);
                    return GuardIntentBuilder.Reposition(target, GuardMath.FlatDistance(self, target), lastKnown, hasLastKnown, config);
            }

            return GuardIntentBuilder.HoldPost(post, GuardMath.FlatDistance(self, post), now, config);
        }

        public string GetDebugText()
        {
            if (scorer == null)
            {
                return "TacticalGuard (not started)";
            }
            return heldCover != null
                ? $"TacticalGuard {scorer.CurrentAction} (cover {heldCover.Id})"
                : $"TacticalGuard {scorer.CurrentAction}";
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ReleaseCover();
            if (fallbackConfig != null)
            {
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

        // Builds the situation the scorer sees. Also records a sighting on the Blackboard so the rest of the squad knows.
        private GuardSituation Sense(Vector3 self, Transform player, Blackboard board, float now,
            out Vector3 playerPos, out Vector3 playerChest)
        {
            playerPos = self;
            playerChest = self;
            bool canSee = false;
            float distance = float.MaxValue;
            bool aimedAt = false;

            if (player != null)
            {
                playerPos = player.position;
                playerChest = playerPos + Vector3.up * config.playerChestHeight;
                distance = Vector3.Distance(self, playerPos);
                canSee = distance <= config.sightRange
                      && sensor.HasLineOfSight(self + Vector3.up * config.guardEyeHeight, playerChest);
                aimedAt = canSee && IsLookingAt(player, self, playerPos);
            }

            if (canSee && board != null)
            {
                board.lastKnownPlayerPos = playerPos;
                board.lastKnownPlayerTime = now;
            }

            float knowledge = board == null ? 0f : GuardMath.Freshness(now - board.lastKnownPlayerTime, config.knowledgeWindow);
            float recentDamage = GuardMath.Freshness(now - lastDamageTime, config.damageWindow);
            float threat = GuardMath.Threat(canSee, recentDamage, aimedAt, board != null && board.alertLevel > 0, config);

            Health health = context.Health;
            return new GuardSituation
            {
                canSeePlayer = canSee,
                distanceToPlayer = distance,
                knowledge = knowledge,
                threat = threat,
                healthFraction = health != null && health.Max > 0f ? health.Current / health.Max : 1f,
                distanceFromPost = GuardMath.FlatDistance(self, post),
                coverQuality = player != null && threat > 0f ? FindCover(self, playerPos) : 0f
            };
        }

        // Keeps the claimed cover while it still hides the guard; otherwise releases it and claims the best free one.
        private float FindCover(Vector3 self, Vector3 playerPos)
        {
            if (heldCover != null)
            {
                float held = selector.Evaluate(heldCover, self, playerPos, post);
                if (held > 0f)
                {
                    return held;
                }
                ReleaseCover();
            }

            Marker pick = selector.Pick(context.GetMarkers(MarkerType.CoverPoint), self, playerPos, post, this, out float quality);
            if (pick != null && claims.TryClaim(pick, this))
            {
                heldCover = pick;
                return quality;
            }
            return 0f;
        }

        // True if the player's horizontal facing points at the guard (camera forward . direction to guard > aimedDot).
        private bool IsLookingAt(Transform player, Vector3 self, Vector3 playerPos)
        {
            Vector3 forward = player.forward;
            forward.y = 0f;
            Vector3 toGuard = self - playerPos;
            toGuard.y = 0f;
            if (forward.sqrMagnitude < 0.0001f || toGuard.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            return Vector3.Dot(forward.normalized, toGuard.normalized) > config.aimedDot;
        }

        private Vector3 FindPost()
        {
            var spawns = context.GetMarkers(MarkerType.AgentSpawn, context.Self.name);
            return spawns.Count > 0 ? spawns[0].transform.position : context.Self.position;
        }

        private void HandleDamaged(DamageInfo info)
        {
            lastDamageTime = clock();
        }

        private void HandleDied()
        {
            ReleaseCover();
        }

        private void ReleaseCover()
        {
            claims?.Release(this);
            heldCover = null;
        }

        private void Unsubscribe()
        {
            if (subscribedHealth != null)
            {
                subscribedHealth.OnDamaged -= HandleDamaged;
                subscribedHealth.OnDied -= HandleDied;
            }
            subscribedHealth = null;
        }
    }
}
