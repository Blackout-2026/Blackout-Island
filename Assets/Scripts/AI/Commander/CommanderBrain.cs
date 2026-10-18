// Owner: Agent Controller (Commander AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Commander
{
    /// <summary>
    /// Plans squad orders, then waits for observed results. Never moves a Transform or fires a weapon.
    /// AgentRoot supplies think ticks; the lifecycle integration supplies cure notifications.
    /// </summary>
    public sealed class CommanderBrain : MonoBehaviour, IAgentBrain, IDebugInfo
    {
        [SerializeField] private CommanderConfig config;

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

        public string State { get; private set; } = "Uninitialized";
        public string GoalName { get; private set; } = "none (monitoring)";
        public string CurrentAction => step < plan.Count ? plan[step].Name : null;
        public IReadOnlyList<GoapAction> CurrentPlan => plan;
        /// <summary>A diagnostic snapshot. Do not request this from a per-frame executor.</summary>
        public WorldState WorldState => world.Clone();
        public int PlanVersion { get; private set; }
        public CommanderConfig Config => config;

        // Set these before Initialize. The clock seam keeps all timing tests deterministic.
        public void SetConfig(CommanderConfig value) => config = value;
        public void SetClock(Func<float> value) => clock = value ?? DefaultClock;

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
            if (!board.cureTaken && board.alertLevel <= 0)
            {
                State = "Monitor";
                GoalName = "none (monitoring)";
                plan = Array.Empty<GoapAction>();
                plannedFrom = null;
                return IdleIntent();
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
                        (CountAtRally() >= config.minimumSquadSize || now - actionStarted >= config.regroupTimeout))
                    {
                        // The reference permits regroup completion after 12 s if enough units survive.
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

        public string GetDebugText()
        {
            // AgentRoot may stop ticking after death; diagnostics must still describe the actual health.
            string displayState = context?.Health != null && !context.Health.IsAlive ? "Dead" :
                !isActiveAndEnabled ? "Disabled" : State;
            return CommanderDebugText.Format(GoalName, displayState, plan, step,
                world, context?.Blackboard, clock());
        }
    }
}
