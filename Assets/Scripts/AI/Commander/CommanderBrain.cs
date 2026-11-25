// Owner: Agent Controller (Commander AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Commander
{
    /// <summary>Injectable sight query so cover and firing decisions have deterministic tests.</summary>
    public interface ICommanderSight
    {
        bool Clear(Vector3 from, Vector3 to);
    }

    public sealed class PhysicsCommanderSight : ICommanderSight
    {
        private readonly LayerMask blockers;

        public PhysicsCommanderSight(LayerMask blockers) => this.blockers = blockers;

        public bool Clear(Vector3 from, Vector3 to) =>
            !Physics.Linecast(from, to, blockers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Plans squad orders with GOAP and chooses cover/peek/pistol intents when threatened.
    /// The shared mover, AnimatorDriver and AgentShooter execute those intents.
    /// AgentRoot supplies think ticks; the lifecycle integration supplies cure notifications.
    /// </summary>
    public sealed class CommanderBrain : MonoBehaviour, IAgentBrain, IDebugInfo
    {
        [SerializeField] private CommanderConfig config;
        [SerializeField] private LayerMask sightBlockers =
            (1 << PhysicsLayers.Default) | (1 << PhysicsLayers.Level) | (1 << PhysicsLayers.Interactable);
        [SerializeField] private Transform pistolMuzzle;

        private enum CombatPhase { None, MovingToCover, Hiding, MovingToPeek, Shooting, Flanking }

        private static readonly Func<float> DefaultClock = () => Time.time;
        private readonly GoapPlanner planner = new GoapPlanner();
        private readonly WorldState world = new WorldState();
        private readonly GoapGoal locateGoal = new GoapGoal(
            new WorldState().Set(CommanderFacts.PlayerLocated, true));
        private readonly GoapGoal neutralizeGoal = new GoapGoal(
            new WorldState().Set(CommanderFacts.PlayerNeutralized, true));
        private IAgentContext context;
        private Func<float> clock = DefaultClock;
        private GoapAction[] actions;
        private IReadOnlyList<GoapAction> plan = Array.Empty<GoapAction>();
        private WorldState plannedFrom;
        private int step;
        private float actionStarted;
        private float lastIssued;
        private float retryAt;
        private Vector3 rally;
        private Vector3 post;
        private bool forceReplan;
        private bool regroupIssued;
        private Blackboard subscribedBoard;
        private ICommanderSight sight;
        private Marker cover;
        private CombatPhase combatPhase;
        private Vector3 peekTarget;
        private bool peekRight;
        private float phaseUntil;
        private float lastOwnSighting;
        private Vector3 flankTarget;
        private float flankUntil;
        private bool flankRight;

        public string State { get; private set; } = "Uninitialized";
        public string GoalName { get; private set; } = "none (monitoring)";
        public string CurrentAction => step < plan.Count ? plan[step].Name : null;
        public IReadOnlyList<GoapAction> CurrentPlan => plan;
        /// <summary>A diagnostic snapshot. Do not request this from a per-frame executor.</summary>
        public WorldState WorldState => world.Clone();
        public int PlanVersion { get; private set; }
        public CommanderConfig Config => config;
        public Marker CombatCover => cover;
        /// <summary>Live diagnostic source. Gizmo readers must not mutate it or tick the brain.</summary>
        public Blackboard DebugBlackboard => context?.Blackboard;

        // Set these before Initialize. The clock seam keeps all timing tests deterministic.
        public void SetConfig(CommanderConfig value) => config = value;
        public void SetClock(Func<float> value) => clock = value ?? DefaultClock;
        public void SetSight(ICommanderSight value) => sight = value;
        public void SetMuzzle(Transform value) => pistolMuzzle = value;

        public void Initialize(IAgentContext newContext)
        {
            if (newContext == null || newContext.Blackboard == null || newContext.Self == null)
                throw new ArgumentException("Commander needs a context, Self and Blackboard.", nameof(newContext));
            if (config == null)
                throw new InvalidOperationException("Assign a CommanderConfig before initialization.");

            Unsubscribe();
            context = newContext;
            actions = config.BuildActions();
            post = context.Self.position;
            plan = Array.Empty<GoapAction>();
            plannedFrom = null;
            step = 0;
            PlanVersion = 0;
            retryAt = float.NegativeInfinity;
            forceReplan = true;
            GoalName = "none (monitoring)";
            State = "Monitor";
            combatPhase = CombatPhase.None;
            cover = null;
            lastOwnSighting = float.NegativeInfinity;
            flankUntil = float.NegativeInfinity;
            sight = sight ?? new PhysicsCommanderSight(sightBlockers);
            Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (context == null || subscribedBoard != null || !isActiveAndEnabled)
                return;
            subscribedBoard = context.Blackboard;
            subscribedBoard.CureTakenReported += NotifyCureTaken;
            if (subscribedBoard.cureTaken)
                NotifyCureTaken();
        }

        private void Unsubscribe()
        {
            if (subscribedBoard == null)
                return;
            subscribedBoard.CureTakenReported -= NotifyCureTaken;
            subscribedBoard = null;
        }

        /// <summary>Called synchronously by the cure integration, without waiting for the next think tick.</summary>
        public void NotifyCureTaken()
        {
            if (!IsAlive)
                return;
            context.Blackboard.cureTaken = true;
            ActivateAlert();
            forceReplan = true;
            Tick(0f);
        }

        public AgentIntent Tick(float deltaTime)
        {
            if (context == null)
                return AgentIntent.Idle;
            if (!IsAlive)
            {
                State = "Dead";
                plan = Array.Empty<GoapAction>();
                return AgentIntent.Idle;
            }

            float now = clock();
            Blackboard board = context.Blackboard;
            int living = CountLiving();
            Observe(now, living);
            AgentIntent combatIntent = ThinkCombat(now, out bool combatActive);
            if (!board.cureTaken && board.alertLevel <= 0)
            {
                State = combatActive ? CombatState() : "Monitor";
                GoalName = "none (monitoring)";
                plan = Array.Empty<GoapAction>();
                plannedFrom = null;
                return combatActive ? combatIntent : IdleIntent();
            }
            if (board.cureTaken)
                ActivateAlert();

            bool neutralized = board.playerNeutralized;
            bool located = ReadBool(CommanderFacts.PlayerLocated);
            string goalName = located || neutralized ? "PlayerNeutralized" : "PlayerLocated";
            bool changed = plannedFrom == null || !world.Equals(plannedFrom);
            if (forceReplan || changed || goalName != GoalName ||
                (CurrentAction != null && !plan[step].IsApplicable(world)) ||
                (CurrentAction == null && now >= retryAt && !neutralized))
            {
                GoalName = goalName;
                plan = living == 0 ? Array.Empty<GoapAction>() :
                    planner.Plan(world, located || neutralized ? neutralizeGoal : locateGoal, actions);
                plannedFrom = world.Clone();
                step = 0;
                PlanVersion++;
                forceReplan = false;
                retryAt = float.PositiveInfinity;
                StartAction(now);
            }

            if (neutralized)
                State = "PlayerNeutralized";
            else if (CurrentAction == null)
                State = living == 0 ? "NoLivingSquad" : "NoPlan";
            else
                Execute(now, living);

            if (combatActive)
            {
                State = CombatState();
                return combatIntent;
            }
            return IdleIntent();
        }

        private bool IsAlive => isActiveAndEnabled && context != null && context.Self != null &&
            context.Health != null && context.Health.IsAlive;

        private void ActivateAlert()
        {
            context.Blackboard.BroadcastAlert(new AgentAlert(config.alertSpeedMultiplier,
                config.alertVisionMultiplier, config.alertAimErrorMultiplier));
        }

        private void Observe(float now, int living)
        {
            Blackboard board = context.Blackboard;
            float age = now - board.lastKnownPlayerTime;
            world.Set(CommanderFacts.PlayerLocated, age >= 0f && age < config.sightingMemory)
                .Set(CommanderFacts.CureTaken, board.cureTaken)
                .Set(CommanderFacts.GeneratorDisabled, board.generatorDisabled)
                .Set(CommanderFacts.AgentsAlive, living)
                .Set(CommanderFacts.SquadAssembled, board.squadAssembled && living >= config.minimumSquadSize)
                .Set(CommanderFacts.PlayerNeutralized, board.playerNeutralized);
        }

        private bool ReadBool(string key)
        {
            world.TryGetBool(key, out bool value);
            return value;
        }

        private int CountLiving()
        {
            int count = 0;
            IReadOnlyList<BlackboardAgent> agents = context.Blackboard.Agents;
            for (int i = 0; i < agents.Count; i++)
                if (IsOtherLiving(agents[i]))
                    count++;
            return count;
        }

        private bool IsOtherLiving(BlackboardAgent agent) =>
            agent.IsAlive && agent.Transform != context.Self;

        private void StartAction(float now)
        {
            actionStarted = now;
            lastIssued = float.NegativeInfinity;
            regroupIssued = false;
            if (CurrentAction == CommanderConfig.OrderRegroup)
                rally = (KnownPosition() + context.Self.position) * 0.5f;
        }

        private void Execute(float now, int living)
        {
            State = CurrentAction;
            switch (CurrentAction)
            {
                case CommanderConfig.OrderConverge:
                    if (now - lastIssued >= config.convergeInterval)
                        Issue(DirectiveType.Converge, KnownPosition(), 0f, now, living);
                    break;
                case CommanderConfig.OrderSearchZone:
                    if (now - actionStarted >= config.searchTimeout)
                        FailAction(now);
                    else if (now - lastIssued >= config.searchInterval)
                        Issue(DirectiveType.SearchZone, KnownPosition(), config.searchRadius, now, living);
                    break;
                case CommanderConfig.OrderRegroup:
                    if (!regroupIssued)
                    {
                        Issue(DirectiveType.Regroup, rally, config.regroupRadius, now, living);
                        regroupIssued = true;
                    }
                    if (living >= config.minimumSquadSize &&
                        CountAtRally() >= config.minimumSquadSize)
                    {
                        // Only observed arrivals establish squad readiness. A timeout is failure,
                        // never evidence that distant survivors reached the rally point.
                        context.Blackboard.squadAssembled = true;
                        world.Set(CommanderFacts.SquadAssembled, true);
                        step++;
                        StartAction(now);
                    }
                    else if (now - actionStarted >= config.regroupTimeout)
                        FailAction(now);
                    break;
            }
        }

        private int CountAtRally()
        {
            int count = 0;
            float radiusSquared = config.regroupRadius * config.regroupRadius;
            IReadOnlyList<BlackboardAgent> agents = context.Blackboard.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                if (!IsOtherLiving(agents[i]))
                    continue;
                Vector3 offset = agents[i].Transform.position - rally;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radiusSquared)
                    count++;
            }
            return count;
        }

        private void FailAction(float now)
        {
            plan = Array.Empty<GoapAction>();
            State = "ReplanPending";
            retryAt = now + config.searchInterval;
        }

        private void Issue(DirectiveType type, Vector3 center, float radius, float now, int living)
        {
            lastIssued = now;
            int index = 0;
            IReadOnlyList<BlackboardAgent> agents = context.Blackboard.Agents;
            for (int i = 0; i < agents.Count && IsAlive; i++)
            {
                BlackboardAgent agent = agents[i];
                if (!IsOtherLiving(agent))
                    continue;
                Vector3 target = center;
                if (type == DirectiveType.SearchZone)
                {
                    float angle = 2f * Mathf.PI * index / living;
                    target += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * config.searchRingRadius;
                }
                index++;
                agent.Issue(new Directive(type, target, radius));
            }
        }

        private Vector3 KnownPosition()
        {
            float time = context.Blackboard.lastKnownPlayerTime;
            if (!float.IsInfinity(time) && !float.IsNaN(time))
                return context.Blackboard.lastKnownPlayerPos;
            IReadOnlyList<Marker> sockets = context.GetMarkers(MarkerType.CureSocket);
            if (sockets != null && sockets.Count > 0 && sockets[0] != null)
                return sockets[0].transform.position;
            return post;
        }

        private AgentIntent IdleIntent()
        {
            return new AgentIntent
            {
                moveSpeed = MoveSpeed.Stop,
                hasLookTarget = true,
                lookTarget = KnownPosition()
            };
        }

        private AgentIntent ThinkCombat(float now, out bool active)
        {
            active = false;
            Transform player = context.Player;
            if (player == null || context.Blackboard.playerNeutralized)
            {
                ResetCombat();
                return AgentIntent.Idle;
            }

            Vector3 self = context.Self.position;
            Vector3 playerPos = player.position;
            Vector3 chest = playerPos + Vector3.up * config.playerChestHeight;
            float distance = Vector3.Distance(self, playerPos);
            bool visible = distance <= config.sightRange &&
                sight.Clear(self + Vector3.up * config.eyeHeight, chest);
            bool clearShot = visible && (pistolMuzzle == null || sight.Clear(pistolMuzzle.position, chest));
            if (visible)
            {
                lastOwnSighting = now;
                context.Blackboard.lastKnownPlayerPos = playerPos;
                context.Blackboard.lastKnownPlayerTime = now;
            }
            if (!visible && now - lastOwnSighting > config.combatMemory)
            {
                ResetCombat();
                return AgentIntent.Idle;
            }

            active = true;
            Vector3 look = visible ? chest : context.Blackboard.lastKnownPlayerPos + Vector3.up * config.playerChestHeight;
            if (cover != null && !HiddenAt(cover.transform.position, playerPos))
            {
                cover = null;
                combatPhase = CombatPhase.None;
            }
            if (cover == null && visible)
            {
                cover = PickCover(self, playerPos);
                if (cover != null) combatPhase = CombatPhase.MovingToCover;
            }

            if (cover != null)
            {
                Vector3 point = cover.transform.position;
                if (combatPhase == CombatPhase.MovingToCover)
                {
                    if (FlatDistance(self, point) > config.coverArrivalRadius)
                        return MoveIntent(point, MoveSpeed.Run, look);
                    combatPhase = CombatPhase.Hiding;
                    phaseUntil = now + HideDuration();
                }
                if (combatPhase == CombatPhase.Hiding)
                {
                    if (now < phaseUntil)
                        return HoldIntent(look, false, false);
                    if (!ChoosePeek(point, playerPos, chest, out peekTarget))
                    {
                        phaseUntil = now + HideDuration();
                        return HoldIntent(look, false, false);
                    }
                    combatPhase = CombatPhase.MovingToPeek;
                }
                if (combatPhase == CombatPhase.MovingToPeek)
                {
                    if (FlatDistance(self, peekTarget) > config.peekArrivalRadius)
                        return MoveIntent(peekTarget, MoveSpeed.Walk, look);
                    combatPhase = CombatPhase.Shooting;
                    phaseUntil = now + config.peekSeconds;
                }
                if (combatPhase == CombatPhase.Shooting)
                {
                    if (now < phaseUntil)
                        return HoldIntent(look, visible, clearShot && distance <= config.pistolRange);
                    combatPhase = CombatPhase.MovingToCover;
                    return MoveIntent(point, MoveSpeed.Walk, look);
                }
            }

            // A room with no usable CoverPoint still has a fighting Commander.
            if (visible && !clearShot)
            {
                // The eye may see over furniture while the lower pistol cannot.
                // Move sideways to find an unobstructed lane before spending a shot.
                if (now >= flankUntil || FlatDistance(self, flankTarget) <= config.peekArrivalRadius)
                {
                    if (!ChooseFireLane(self, playerPos, chest, out flankTarget))
                        flankTarget = playerPos - (playerPos - self).normalized * config.coverPlayerStandoff;
                    flankUntil = now + 2.5f;
                }
                combatPhase = CombatPhase.Flanking;
                return MoveIntent(flankTarget, MoveSpeed.Walk, look);
            }
            if (visible && distance <= config.pistolRange)
            {
                combatPhase = CombatPhase.None;
                return HoldIntent(look, true, clearShot);
            }
            if (visible)
            {
                Vector3 approach = playerPos - (playerPos - self).normalized * (config.pistolRange - 1f);
                return MoveIntent(approach, MoveSpeed.Run, look);
            }
            if (FlatDistance(self, context.Blackboard.lastKnownPlayerPos) > config.coverArrivalRadius)
                return MoveIntent(context.Blackboard.lastKnownPlayerPos, MoveSpeed.Walk, look);
            return HoldIntent(look, visible, false);
        }

        private Marker PickCover(Vector3 self, Vector3 player)
        {
            IReadOnlyList<Marker> markers = context.GetMarkers(MarkerType.CoverPoint);
            if (markers == null) return null;
            Marker best = null;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < markers.Count; i++)
            {
                Marker candidate = markers[i];
                if (candidate == null) continue;
                Vector3 point = candidate.transform.position;
                float travel = FlatDistance(self, point);
                if (travel > config.coverSearchRadius ||
                    FlatDistance(point, player) < config.coverPlayerStandoff ||
                    !HiddenAt(point, player)) continue;
                float score = travel + 0.25f * FlatDistance(point, post);
                if (score >= bestScore) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        private bool HiddenAt(Vector3 point, Vector3 player) =>
            !sight.Clear(player + Vector3.up * config.eyeHeight,
                point + Vector3.up * config.coverArrivalRadius);

        private bool ChoosePeek(Vector3 point, Vector3 player, Vector3 chest, out Vector3 chosen)
        {
            Vector3 toward = player - point;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.01f)
            {
                chosen = point;
                return false;
            }
            Vector3 side = Vector3.Cross(Vector3.up, toward.normalized) * config.peekOffset;
            peekRight = !peekRight;
            Vector3 first = point + (peekRight ? side : -side);
            Vector3 second = point - (peekRight ? side : -side);
            if (sight.Clear(first + Vector3.up * config.eyeHeight, chest))
            {
                chosen = first;
                return true;
            }
            if (sight.Clear(second + Vector3.up * config.eyeHeight, chest))
            {
                chosen = second;
                return true;
            }
            chosen = point;
            return false;
        }

        private bool ChooseFireLane(Vector3 self, Vector3 player, Vector3 chest, out Vector3 chosen)
        {
            Vector3 toward = player - self;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.01f)
            {
                chosen = self;
                return false;
            }
            Vector3 side = Vector3.Cross(Vector3.up, toward.normalized);
            float height = pistolMuzzle != null ? pistolMuzzle.position.y - self.y : config.eyeHeight;
            flankRight = !flankRight;
            for (int step = 0; step < 2; step++)
            {
                float offset = step == 0 ? 2f : 3.5f;
                for (int direction = 0; direction < 2; direction++)
                {
                    Vector3 candidate = self + side * (direction == 0 ?
                        (flankRight ? offset : -offset) : (flankRight ? -offset : offset));
                    if (FlatDistance(candidate, player) >= config.coverPlayerStandoff &&
                        sight.Clear(candidate + Vector3.up * height, chest))
                    {
                        chosen = candidate;
                        return true;
                    }
                }
            }
            chosen = self;
            return false;
        }

        private float HideDuration() => context.Health.Current / context.Health.Max <= config.lowHealthFraction
            ? config.lowHealthHideSeconds : config.hideSeconds;

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 delta = a - b;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static AgentIntent MoveIntent(Vector3 target, MoveSpeed speed, Vector3 look) => new AgentIntent
        {
            moveTarget = target, moveSpeed = speed, hasLookTarget = true, lookTarget = look
        };

        private static AgentIntent HoldIntent(Vector3 look, bool aim, bool fire) => new AgentIntent
        {
            moveSpeed = MoveSpeed.Stop, hasLookTarget = true, lookTarget = look, aim = aim, fire = fire
        };

        private string CombatState() => combatPhase == CombatPhase.None ? "PistolAttack" : "Combat" + combatPhase;

        private void ResetCombat()
        {
            cover = null;
            combatPhase = CombatPhase.None;
            lastOwnSighting = float.NegativeInfinity;
            flankUntil = float.NegativeInfinity;
        }

        public string GetDebugText()
        {
            // AgentRoot may stop ticking after death; diagnostics must still describe the actual health.
            string displayState = context?.Health != null && !context.Health.IsAlive ? "Dead" :
                !isActiveAndEnabled ? "Disabled" : State;
            string report = CommanderDebugText.Format(GoalName, displayState, plan, step,
                world, context?.Blackboard, clock());
            string combat = float.IsNegativeInfinity(lastOwnSighting) ? "Inactive" : CombatState();
            return report + "\nCombat: " + combat + " | cover=" + (cover != null ? cover.name : "none") +
                " | health=" + (context?.Health != null ? context.Health.Current.ToString("F0") : "-");
        }

        [ContextMenu("Log Commander Plan")]
        private void LogCommanderPlan()
        {
            Debug.Log(GetDebugText(), this);
        }
    }
}
