// Owner: Systems Engineer (Hunter AI).
using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// The Hunter's behaviour tree (docs/reference/systems-engineer.md section 15):
    /// <code>
    /// Root ?
    /// ├─ → Attack            [SeesPlayer] [InRange] AimAndFire
    /// ├─ → Pursue            [SeesPlayer] CloseIn
    /// ├─ → Investigate       [HeardPreyNoise] MoveToNoise  Ambush
    /// ├─ → CautiousApproach  [HeardGunfire] ? ( → [GunfireClose] TakeCover | AdvanceByCover )
    /// ├─ → Search            [SawRecently] GoToLastSeen  LookAround
    /// ├─ → FollowOrders      [HasOrder] MoveToOrder
    /// └─ → Roam              NextWaypoint  WalkTo  LookAround
    /// </code>
    /// It reads only its <see cref="HunterFacts"/> and <see cref="IHunterWorld"/>, and its leaves fill one
    /// <see cref="AgentIntent"/> per tick: the Hunter's decisions never touch the Transform, the NavMesh or the gun.
    /// </summary>
    public sealed class HunterTree
    {
        // Heights above the feet that the Hunter looks at: a noise (a gun or a head) and its surroundings.
        private const float NoiseLookHeight = 1.4f;
        private const float AmbushLookHeight = 1.2f;

        private readonly HunterConfig config;
        private readonly IHunterWorld world;
        private readonly BehaviourTree tree;
        private readonly LocalBlackboard facts;
        private readonly Selector root;
        private AgentIntent intent;
        private int waypointIndex;

        /// <summary>Progress of one action between ticks, cleared when the action is interrupted or done.</summary>
        private sealed class Leg
        {
            public float StartedAt = -1f;
            public float WaitUntil = -1f;
            public bool HasTarget;
            public Vector3 Target;
            public int Step;
            public float Angle;
            /// <summary>The order this leg is working on (its OrderReceivedAt), so a newer order starts over.</summary>
            public float Stamp = float.NegativeInfinity;

            public void Clear()
            {
                StartedAt = -1f;
                WaitUntil = -1f;
                HasTarget = false;
                Step = 0;
                Stamp = float.NegativeInfinity;
            }
        }

        public HunterTree(HunterConfig config, IHunterWorld world, LocalBlackboard blackboard = null)
        {
            this.config = config ? config : throw new ArgumentNullException(nameof(config));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            root = Build();
            tree = new BehaviourTree(root, new BtContext(blackboard ?? new LocalBlackboard()));
            facts = tree.Blackboard;
        }

        public LocalBlackboard Facts => facts;
        public Node Root => root;

        /// <summary>The root branch that ran on the last tick (Attack, Investigate, Roam...), or "Idle".</summary>
        public string ActiveBranch { get; private set; } = "Idle";

        /// <summary>One decision. The brain writes the facts first; the result is what the leaves asked for.</summary>
        public AgentIntent Tick(float deltaTime)
        {
            intent = AgentIntent.Idle;
            tree.Tick(deltaTime);
            ActiveBranch = "Idle";
            for (int i = 0; i < root.Children.Count; i++)
            {
                NodeStatus? status = root.Children[i].LastStatus;
                if (status == NodeStatus.Running || status == NodeStatus.Success)
                {
                    ActiveBranch = root.Children[i].Name;
                    break;
                }
            }

            return intent;
        }

        private Selector Build()
        {
            var aim = new Leg();
            var closeIn = new Leg();
            var toNoise = new Leg();
            var ambush = new Leg();
            var cover = new Leg();
            var advance = new Leg();
            var toLastSeen = new Leg();
            var searchLook = new Leg();
            var order = new Leg();
            var walk = new Leg();
            var roamLook = new Leg();

            return new Selector("Root",
                new Sequence("Attack",
                    new Condition("SeesPlayer", SeesPlayer),
                    new Condition("InRange", InRange),
                    new ActionNode("AimAndFire", AimAndFire, aim.Clear)),
                new Sequence("Pursue",
                    new Condition("SeesPlayer", SeesPlayer),
                    new ActionNode("CloseIn", c => CloseIn(c, closeIn), closeIn.Clear)),
                new Sequence("Investigate",
                    new Condition("HeardPreyNoise", HeardPreyNoise),
                    new ActionNode("MoveToNoise", c => MoveToNoise(c, toNoise), toNoise.Clear),
                    new ActionNode("Ambush", c => Ambush(c, ambush), ambush.Clear)),
                new Sequence("CautiousApproach",
                    new Condition("HeardGunfire", HeardGunfire),
                    new Selector("GunfireTactic",
                        new Sequence("CloseFire",
                            new Condition("GunfireClose", GunfireClose),
                            new ActionNode("TakeCover", c => TakeCover(c, cover), cover.Clear)),
                        new ActionNode("AdvanceByCover", c => AdvanceByCover(c, advance), advance.Clear))),
                new Sequence("Search",
                    new Condition("SawRecently", SawRecently),
                    new ActionNode("GoToLastSeen", c => GoToLastSeen(c, toLastSeen), toLastSeen.Clear),
                    new ActionNode("LookAround", c => SearchLookAround(c, searchLook), searchLook.Clear)),
                new Sequence("FollowOrders",
                    new Condition("HasOrder", HasOrder),
                    new ActionNode("MoveToOrder", c => MoveToOrder(c, order), order.Clear)),
                new Sequence("Roam",
                    new ActionNode("NextWaypoint", NextWaypoint),
                    new ActionNode("WalkTo", c => WalkToWaypoint(c, walk), walk.Clear),
                    new ActionNode("LookAround", c => RoamLookAround(c, roamLook), roamLook.Clear)));
        }

        // ---- facts --------------------------------------------------------------------------------------------

        private float Now => facts.Get(HunterFacts.Now);
        private Vector3 Self => facts.Get(HunterFacts.Self);
        private Vector3 Player => facts.Get(HunterFacts.PlayerPosition);
        private Vector3 Noise => facts.Get(HunterFacts.NoisePosition);
        private NoiseType HeardType => facts.Get(HunterFacts.HeardNoiseType);
        private float LastSeenTime => facts.Get(HunterFacts.LastSeenTime, float.NegativeInfinity);
        private float NoiseHeardAt => facts.Get(HunterFacts.NoiseHeardAt, float.NegativeInfinity);

        private static bool IsGunfire(NoiseType type)
        {
            return type == NoiseType.Gunshot || type == NoiseType.Generator || type == NoiseType.Alarm;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        // ---- conditions ---------------------------------------------------------------------------------------

        private bool SeesPlayer(BtContext c)
        {
            return facts.Get(HunterFacts.PlayerDetected) && Now - LastSeenTime < config.seenRecentlySeconds;
        }

        private bool InRange(BtContext c)
        {
            float range = config.attackRange * facts.Get(HunterFacts.RangeMultiplier, 1f);
            return facts.Get(HunterFacts.CanSeePlayer) && Vector3.Distance(Self, Player) < range;
        }

        /// <summary>A new footstep, sprint, door, crate or melee noise, loud enough and recent enough.</summary>
        private bool HeardPreyNoise(BtContext c)
        {
            return IsNewNoise(config.preyNoiseThreshold, config.preyNoiseSeconds) && !IsGunfire(HeardType);
        }

        /// <summary>A new gunshot, generator or alarm, loud enough and recent enough.</summary>
        private bool HeardGunfire(BtContext c)
        {
            return IsNewNoise(config.gunfireThreshold, config.gunfireSeconds) && IsGunfire(HeardType);
        }

        private bool IsNewNoise(float threshold, float seconds)
        {
            float heardAt = NoiseHeardAt;
            return facts.Get(HunterFacts.HasNoise)
                   && facts.Get(HunterFacts.NoiseLoudness) > threshold
                   && Now - heardAt < seconds
                   && heardAt > facts.Get(HunterFacts.NoiseHandledAt, float.NegativeInfinity);
        }

        private bool GunfireClose(BtContext c)
        {
            return FlatDistance(Self, Noise) < config.gunfireCloseDistance;
        }

        private bool SawRecently(BtContext c)
        {
            float seenAt = LastSeenTime;
            return !facts.Get(HunterFacts.CanSeePlayer)
                   && Now - seenAt < config.searchMemorySeconds
                   && seenAt > facts.Get(HunterFacts.SightingHandledAt, float.NegativeInfinity);
        }

        private bool HasOrder(BtContext c)
        {
            return facts.Get(HunterFacts.HasOrder)
                   && facts.Get(HunterFacts.OrderReceivedAt, float.NegativeInfinity)
                   > facts.Get(HunterFacts.OrderHandledAt, float.NegativeInfinity);
        }

        // ---- leaves -------------------------------------------------------------------------------------------

        /// <summary>Stand, aim and fire at the chest. The shooter adds the reaction delay and the aim error.</summary>
        private NodeStatus AimAndFire(BtContext c)
        {
            intent.moveSpeed = MoveSpeed.Stop;
            intent.aim = true;
            intent.fire = true;
            LookAt(Player, config.aimHeight);
            return facts.Get(HunterFacts.CanSeePlayer) ? NodeStatus.Running : NodeStatus.Failure;
        }

        private NodeStatus CloseIn(BtContext c, Leg leg)
        {
            return MoveTo(c, leg, Player, MoveSpeed.Run, config.closeInDistance, true);
        }

        /// <summary>Walk to a footstep (the prey is unaware); run to a sprint (the prey is getting away).</summary>
        private NodeStatus MoveToNoise(BtContext c, Leg leg)
        {
            MoveSpeed speed = HeardType == NoiseType.Sprint ? MoveSpeed.Run : MoveSpeed.Walk;
            return MoveTo(c, leg, Noise, speed, config.investigateArriveDistance, true);
        }

        private NodeStatus Ambush(BtContext c, Leg leg)
        {
            Hold(Noise, AmbushLookHeight);
            float seconds = HeardType == NoiseType.Sprint ? config.sprintAmbushSeconds : config.ambushSeconds;
            if (!Waited(c, leg, seconds))
            {
                return NodeStatus.Running;
            }

            facts.Set(HunterFacts.NoiseHandledAt, NoiseHeardAt);
            return NodeStatus.Success;
        }

        /// <summary>Gunfire within 16 m: run behind the nearest cover, then hold there aiming at the noise.</summary>
        private NodeStatus TakeCover(BtContext c, Leg leg)
        {
            Vector3 noise = Noise;
            if (!leg.HasTarget)
            {
                leg.Target = world.TryFindCover(Self, noise, config.coverSearchRadius, out Vector3 point) ? point : Self;
                leg.HasTarget = true;
            }

            NodeStatus moving = MoveTo(c, leg, leg.Target, MoveSpeed.Run, config.coverArriveDistance, true);
            LookAt(noise, NoiseLookHeight);
            if (moving != NodeStatus.Success)
            {
                return moving;
            }

            Hold(noise, NoiseLookHeight);
            if (!Waited(c, leg, config.holdCoverSeconds))
            {
                return NodeStatus.Running;
            }

            facts.Set(HunterFacts.NoiseHandledAt, NoiseHeardAt);
            return NodeStatus.Success;
        }

        /// <summary>
        /// Gunfire further away: walk from cover to cover towards it, pausing at each. With no cover nearer the
        /// noise, walk straight in. Within 16 m <see cref="TakeCover"/> wins, which ends the advance.
        /// </summary>
        private NodeStatus AdvanceByCover(BtContext c, Leg leg)
        {
            Vector3 noise = Noise;
            if (leg.WaitUntil >= 0f)
            {
                Hold(noise, NoiseLookHeight);
                if (c.Time < leg.WaitUntil)
                {
                    return NodeStatus.Running;
                }

                leg.Clear();
            }

            if (!leg.HasTarget)
            {
                bool found = world.TryFindAdvanceCover(Self, noise, config.advanceCoverRange, config.advanceMinGain,
                    out Vector3 point);
                leg.Target = found ? point : noise;
                leg.Step = found ? 1 : 0;
                leg.HasTarget = true;
            }

            NodeStatus moving = MoveTo(c, leg, leg.Target, MoveSpeed.Walk, config.coverArriveDistance, true);
            LookAt(noise, NoiseLookHeight);
            if (moving == NodeStatus.Success && leg.Step == 1)
            {
                Hold(noise, NoiseLookHeight);
                leg.WaitUntil = c.Time + config.advancePauseSeconds;
                return NodeStatus.Running;
            }

            return moving;
        }

        private NodeStatus GoToLastSeen(BtContext c, Leg leg)
        {
            return MoveTo(c, leg, facts.Get(HunterFacts.LastSeenPosition), MoveSpeed.Run, config.searchArriveDistance,
                true);
        }

        private NodeStatus SearchLookAround(BtContext c, Leg leg)
        {
            LookAround(c);
            if (!Waited(c, leg, config.searchLookAroundSeconds))
            {
                return NodeStatus.Running;
            }

            facts.Set(HunterFacts.SightingHandledAt, LastSeenTime);
            return NodeStatus.Success;
        }

        /// <summary>
        /// Converge runs to the point, Regroup walks there, SearchZone runs to a few points inside the radius. The
        /// order is complete on arrival. A higher branch can interrupt it; the order stays until it is complete.
        /// </summary>
        private NodeStatus MoveToOrder(BtContext c, Leg leg)
        {
            Directive order = facts.Get(HunterFacts.Order);
            float receivedAt = facts.Get(HunterFacts.OrderReceivedAt, float.NegativeInfinity);
            if (leg.Stamp != receivedAt)
            {
                // A newer order (a moved target, a new search area) starts over instead of mixing with the old one.
                leg.Clear();
                leg.Stamp = receivedAt;
            }

            if (order.type != DirectiveType.SearchZone)
            {
                MoveSpeed speed = order.type == DirectiveType.Regroup ? MoveSpeed.Walk : MoveSpeed.Run;
                NodeStatus status = MoveTo(c, leg, order.position, speed, config.orderArriveDistance, true);
                if (status == NodeStatus.Success)
                {
                    CompleteOrder();
                }

                return status;
            }

            while (leg.Step < config.searchZonePoints)
            {
                if (!leg.HasTarget)
                {
                    if (leg.Step == 0)
                    {
                        leg.Angle = world.Random01() * 360f;
                    }

                    leg.Target = SearchPoint(order, leg.Angle + leg.Step * 360f / config.searchZonePoints);
                    leg.HasTarget = true;
                    leg.StartedAt = -1f;
                }

                if (MoveTo(c, leg, leg.Target, MoveSpeed.Run, config.orderArriveDistance, true) == NodeStatus.Running)
                {
                    return NodeStatus.Running;
                }

                // Reached (or gave up on) this point: on to the next one in the same tick.
                leg.Step++;
                leg.HasTarget = false;
            }

            CompleteOrder();
            return NodeStatus.Success;
        }

        private static Vector3 SearchPoint(Directive order, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float distance = Mathf.Max(0f, order.radius) * 0.6f;
            return order.position + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * distance;
        }

        private void CompleteOrder()
        {
            facts.Set(HunterFacts.OrderHandledAt, facts.Get(HunterFacts.OrderReceivedAt, float.NegativeInfinity));
        }

        /// <summary>The next forest waypoint: one or two along the route, at random, as in the prototype.</summary>
        private NodeStatus NextWaypoint(BtContext c)
        {
            int count = world.WaypointCount;
            if (count == 0)
            {
                return NodeStatus.Failure;
            }

            waypointIndex = (waypointIndex + 1 + (world.Random01() < 0.5f ? 0 : 1)) % count;
            return NodeStatus.Success;
        }

        private NodeStatus WalkToWaypoint(BtContext c, Leg leg)
        {
            return MoveTo(c, leg, world.GetWaypoint(waypointIndex), MoveSpeed.Walk, config.waypointArriveDistance,
                false);
        }

        private NodeStatus RoamLookAround(BtContext c, Leg leg)
        {
            if (leg.WaitUntil < 0f)
            {
                leg.WaitUntil = c.Time + Mathf.Lerp(config.lookAroundMinSeconds, config.lookAroundMaxSeconds,
                    world.Random01());
            }

            LookAround(c);
            return c.Time >= leg.WaitUntil ? NodeStatus.Success : NodeStatus.Running;
        }

        // ---- helpers ------------------------------------------------------------------------------------------

        /// <summary>Success within <paramref name="arrive"/> metres (flat); Failure after the move timeout.</summary>
        private NodeStatus MoveTo(BtContext c, Leg leg, Vector3 target, MoveSpeed speed, float arrive, bool aim)
        {
            if (FlatDistance(Self, target) <= arrive)
            {
                return NodeStatus.Success;
            }

            if (leg.StartedAt < 0f)
            {
                leg.StartedAt = c.Time;
            }

            if (c.Time - leg.StartedAt > config.moveTimeoutSeconds)
            {
                return NodeStatus.Failure;
            }

            intent.moveTarget = target;
            intent.moveSpeed = speed;
            intent.aim = aim;
            return NodeStatus.Running;
        }

        private bool Waited(BtContext c, Leg leg, float seconds)
        {
            if (leg.WaitUntil < 0f)
            {
                leg.WaitUntil = c.Time + seconds;
            }

            return c.Time >= leg.WaitUntil;
        }

        /// <summary>Stand still, aim, and face a point.</summary>
        private void Hold(Vector3 point, float height)
        {
            intent.moveSpeed = MoveSpeed.Stop;
            intent.aim = true;
            LookAt(point, height);
        }

        private void LookAt(Vector3 point, float height)
        {
            intent.hasLookTarget = true;
            intent.lookTarget = point + Vector3.up * height;
        }

        /// <summary>Stand and sweep the gaze around, as the prototype's look-around does.</summary>
        private void LookAround(BtContext c)
        {
            intent.moveSpeed = MoveSpeed.Stop;
            intent.hasLookTarget = true;
            float t = c.Time * 0.8f;
            intent.lookTarget = Self + new Vector3(Mathf.Sin(t) * 5f, NoiseLookHeight, Mathf.Cos(t) * 5f);
        }
    }
}
