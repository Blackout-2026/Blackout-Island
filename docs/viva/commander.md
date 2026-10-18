# Commander coordination, debugging and viva (G3-G4)

Owner: Agent Controller. Handoff recipients: World Builder (Security Patrol),
Systems Engineer (Hunter), Core Developer (Tactical Guard).

The Commander sends data through `Blackout.Shared.IDirectiveReceiver`:
`void OnDirective(Directive directive)`. Each receiving brain implements that
interface and maps the data into its own decision architecture. Reference only
`Blackout.Shared`; do not reference `CommanderBrain`, the GOAP planner, or another
brain's assembly. Return an `AgentIntent` from the normal think tick; OnDirective
must not move a Transform, call a mover, animate, or fire a weapon.

## Meaning and lifetime

| Directive | Shared meaning | Position / radius |
| --- | --- | --- |
| Converge | Approach the last known player position; this is not proof of visibility. | Target position; Commander currently sends radius 0. |
| SearchZone | Search the assigned area using the receiving brain's own search behavior. | Area centre and search radius, in metres. |
| Regroup | Rally at the supplied point using the brain's own movement behavior. | Rally position and arrival radius, in metres. |

The latest accepted call replaces the previous order, including its position and
radius. Do not deduplicate by directive type: repeated Converge orders may carry
a moved target. Identical repeats are harmless; receiving them must not restart
the brain's search timers or produce an immediate shot. `Directive` has no order
ID or timestamp, so do not invent comparisons against missing fields. Ignore
unknown enum values while preserving the last valid order.

Keep local combat, survival and perception rules. An order can be retained while
a higher-priority local behavior runs; the owner decides when to resume or finish
it. Arrival or an owner-defined search timeout may complete an order. Commander
death does not cancel existing orders: no more calls arrive, and the receiving
brain can continue or finish its last one normally.

## Tiny DirectiveHandling sample

This complete sample is a plain C# mailbox, not a component or a replacement
brain. It depends only on the shared contract. Copy its fields and OnDirective
logic into your brain, or keep it as a private helper. The brain itself must
implement IDirectiveReceiver so AgentRoot can register it.

```csharp
using Blackout.Shared;

public sealed class DirectiveHandling : IDirectiveReceiver
{
    public bool HasOrder { get; private set; }
    public Directive Order { get; private set; }

    public void OnDirective(Directive directive)
    {
        switch (directive.type)
        {
            case DirectiveType.Converge:
            case DirectiveType.SearchZone:
            case DirectiveType.Regroup:
                Order = directive;
                HasOrder = true;
                break;
        }
    }

    public void CompleteOrder() => HasOrder = false;
}
```

If using the helper, add this inside the receiving brain:

```csharp
private readonly DirectiveHandling orders = new DirectiveHandling();
public void OnDirective(Directive directive) => orders.OnDirective(directive);
```

Read `orders.HasOrder` and `orders.Order` in the normal think tick. The following
tables are mappings into each owner's existing architecture, not new shared
state names or code that should be pasted into another owner's files.

## World Builder: Security Patrol / FSM

| Incoming order | Map to local state | Local action |
| --- | --- | --- |
| Converge | Pursue | Use the order position as the pursuit target when direct player perception does not take priority. |
| SearchZone | Search | Use the order centre and radius for search-point selection. |
| Regroup | Investigate | Investigate the rally position rather than changing the patrol route. |

In the FSM's transition evaluation, check detection/combat first, then the cached
order. Keep the target with the transition; never resolve it from the player's
Transform merely because Converge was received. Repeated orders update the target
without resetting the current state's elapsed time unless the owner deliberately
starts a different state or a different search area.

## Systems Engineer: Hunter / behaviour tree

| Incoming order | Map to local branch / leaf | Local action |
| --- | --- | --- |
| Converge | FollowOrders / MoveToDirective | Run toward the supplied position; complete the order on arrival. |
| SearchZone | FollowOrders / MoveToDirective | Run to locally chosen search points inside the supplied area; keep searching until the owner's completion rule. |
| Regroup | FollowOrders / MoveToDirective | Walk toward the rally position. |

`HasDirective` reads the cached order. Preserve the prototype's reactive selector
priority: Engage, HuntLastSeen, noise reactions, FollowOrders, then Prowl. A higher
branch may interrupt FollowOrders without clearing the order. When it ends,
re-evaluate the cached order rather than calling the Commander. Abort/reset any
running movement leaf using the Hunter's existing tree lifecycle.

## Core Developer: Tactical Guard / utility AI

| Incoming order | Map to local decision data | Local action |
| --- | --- | --- |
| Converge | New guarded post | Set the post to the supplied position. |
| SearchZone | New guarded post | Set the post to the area centre. |
| Regroup | New guarded post | Set the post to the rally position. |

The prototype treats all three orders as post updates. Keep scoring Attack,
MoveToCover, Reposition and HoldPost normally; do not force a score winner just
because an order arrived. The current Unity brain exposes `SetPost(Vector3)`.
The owner should invalidate an obsolete cover claim when changing posts. Using
the SearchZone radius for a wider scan would be an additional owner decision;
the prototype's post-update mapping does not use that radius.

## Existing G2 delivery and owner acceptance checks

AgentRoot registers its brain's IDirectiveReceiver with the shared Blackboard.
The Commander sends only to living other agents. Blackboard retains each latest
directive, and AgentRoot replays it when replacing a brain. There is no need to
find the Commander, subscribe directly to it, or change shared signatures.

Each owner should check all three order types, a repeated type with a new target,
and an interruption by their own combat behavior. Confirm that receiving an order
alone does not enable firing, and that Commander death does not clear the order.
These samples describe the handoff; they do not claim the other owners' runtime
brains already implement it. G3's scene-free fake-receiver harness demonstrates
the mappings independently of their scenes and AI assemblies.

The G2 alert is a separate contract: `IAlertReceiver.OnAlert(AgentAlert)` supplies
the vision multiplier. Directive handling does not apply or stack alert buffs.

## Running the scene-free handoff harness

Open **Window > General > Test Runner > EditMode** and run the assembly
`Blackout.AI.Commander.Coordination.Tests`. No scene needs to be loaded or saved.
The source is `Assets/Scripts/AI/Commander/Tests/Coordination/`.

| Fixture | What it demonstrates |
| --- | --- |
| DirectiveMappingTests | All nine receiver/type combinations, payloads and movement intents; the assembly references no game assembly except Blackout.Shared. |
| DirectiveLifetimeTests | Moved targets, repeated orders, unknown enum values, local interruptions, owner completion and retention without further deliveries. |

The fake Patrol and Hunter have a manually controlled local-combat flag. The
fake Guard has a manually selected utility decision; it does not implement the
real scorer. Their combat paths return Idle rather than inventing perception or
shooting. The Hunter's search point is deterministically halfway along the zone's
positive X radius; this only demonstrates radius handling, not a NavMesh search.
The examples count active execution time and pause it during local interruptions.
Actual brain owners retain responsibility for their own timer and completion rules.

No GameObjects, MonoBehaviours, Health components, Commander instances or scene
assets are created by this harness. Retention without future deliveries illustrates
the receiving side of Commander death; the actual death/lifecycle wiring is covered
by G2 integration and PlayMode tests. Passing these fake tests does not prove that
the other owners' runtime brains have accepted the handoff.

For a batch run, close the Editor first and use the project path for your clone:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath '<repo>' -runTests -testPlatform EditMode `
  -testFilter Blackout.AI.Commander.Coordination.Tests `
  -testResults "$env:TEMP\blackout-g3-tests.xml" -logFile "$env:TEMP\blackout-g3.log"
```

Use a normal local terminal if a restricted sandbox prevents the Editor from
connecting to its Licensing Client. The Test Runner exits the batch process when
finished; the test command deliberately omits `-quit` so it can finish running.

## G4: why GOAP fits the Commander

The Commander chooses an order sequence from changing squad knowledge. For
example, a located player and an unassembled squad require Regroup followed by
Converge; an already assembled squad can Converge immediately. GOAP expresses
these dependencies as preconditions and predicted effects. Adding a new order
means describing its transition and cost, rather than wiring every combination
into explicit FSM transitions.

An FSM is still a good fit for the Patrol's local Patrol/Pursue/Search states.
For the Commander, a flat FSM would duplicate the conditions for squad readiness,
player knowledge and goal changes across transitions. We chose a small handwritten
GOAP planner so the dependency sequence and its cost can be inspected in a viva.
This adds search overhead; with the current three actions, an FSM would also be
practical. The choice favors explainable action composition and future orders.

CommanderBrain selects PlayerLocated while the sighting is missing or at least
6 s old, otherwise PlayerNeutralized. It monitors until cureTaken or an alert.
The planner searches world states using A*, with accumulated cost g and a lower
bound h based on unmet goal keys scaled by the cheapest useful cost per fact.
The execution layer waits for observations: a predicted effect is not an actual
sighting or a player kill.

## Costs and a worked plan

The defaults come from `prototype/src/agents/commander_goap.js` and are stored in
`Assets/Scripts/AI/Commander/Config/CommanderConfig.asset`. Costs are dimensionless
planning preferences. Durations, radii and reissue intervals are separate config
fields.

| Action | Preconditions in the current config | Predicted effect | Cost |
| --- | --- | --- | --- |
| OrderRegroup | squadAssembled=false | squadAssembled=true | 2 |
| OrderSearchZone | playerLocated=false | playerLocated=true | 3 |
| OrderConverge | playerLocated=true, squadAssembled=true | playerNeutralized=true | 2 |

We kept the reference's relative weights rather than claiming a measured optimum.
The design interpretation is that broad search deserves a higher planning cost
than a focused coordination order. A search becomes unavoidable when no sighting
exists; its higher cost does not make an inapplicable Converge legal. CureTaken
activates the alert through its event handler, so current plan costs cover the
three orders shown above.

Worked example: the alert is active, three other agents are alive, and the
Commander has a fresh sighting. The six observed facts are:

```text
playerLocated=true, cureTaken=true, generatorDisabled=false,
agentsAlive=3, squadAssembled=false, playerNeutralized=false
Goal: PlayerNeutralized
```

| Step | Why the action applies | Predicted state change | Accumulated cost |
| --- | --- | --- | --- |
| OrderRegroup | The squad is unassembled. | squadAssembled becomes true. | 2 |
| OrderConverge | The player is located and the predicted squad is assembled. | playerNeutralized becomes true. | 4 |

The stored plan initially reads:

```text
Plan: [OrderRegroup(cost=2)] -> OrderConverge(cost=2) | totalCost=4 | remainingCost=4
```

Regroup sends directives and waits for arrivals or its configured timeout. When
it completes, the retained sequence can show remainingCost=2. On the next think
tick, the changed squad fact causes a replan; the new stored sequence may contain
only Converge with totalCost=2. If the squad had already been assembled at the
start, Converge alone would cost 2. TotalCost describes the currently stored plan,
not accumulated mission expenditure.

With an unknown player, the actual brain chooses PlayerLocated and plans Search
for cost 3 first. A later sighting changes the goal and starts a new neutralization
plan. Do not describe Search + Regroup + Converge as the brain's guaranteed initial
plan. A satisfied goal can have an empty plan and cost 0; an unreachable goal also
has an empty plan, with the brain state distinguishing NoPlan from completion.

## Why share a Blackboard

Perception and mission events publish facts to one Blackboard, so every brain can
use the same last known player position, alert state and event flags. The agent
registry supplies living members and cached directive receivers. The Commander
does not need to find a Hunter component or understand the Patrol's state enum.
Recipients implement the shared interface and keep their own decision rules.

This reduces dependencies between the four AI assemblies and makes coordination
testable with fake receivers. The Blackboard still contains cached Transform and
Health references for roster queries; the separation is between brain types and
their decision architectures. Shared knowledge can become stale, so the sighting
timestamp and the six-second freshness check remain part of the decision.

## Reading G4 diagnostics

`CommanderBrain.GetDebugText()` implements IDebugInfo. AgentRoot publishes it to
its Blackboard state entry on the existing state-update interval (default 0.5 s).
Consumers of IDebugInfo can request the same text without ticking the brain.

The report contains:

- Current goal and the brain state; a direct diagnostic request reports Dead even
  after normal think ticks have stopped.
- The stored action list, each action's configured cost, brackets around the active
  index, totalCost and remainingCost.
- Six observed GOAP facts labelled **last think**, followed by the current raw
  Blackboard flags, roster count, last known position/time and sighting age.
- Active speed, vision and aim-error multipliers, plus published agent-state
  summaries. Each summary uses its first line, up to 240 characters, preventing
  the Commander's own published report from nesting inside subsequent reports.

Unknown facts before initialization are labelled unknown. A never-seen timestamp
is shown as never, with unknown sighting age. Reading debug text does not refresh
observations, replan or issue a directive. Formatting allocates only when requested;
stable think ticks retain their allocation-free behavior checked by the brain tests.
This API supplies text to debug consumers; G4 does not add a separate HUD window.

## Directive scene gizmos

The separate CommanderDirectiveGizmos component is assigned on Commander.prefab.
If rebuilding setup, use **Blackout > Agents > Set Up Commander Debug (G4)**.
It reuses the existing setup tool and preserves existing config tuning.

Enable **Gizmos** in the Scene view during Play mode:

| Directive | Visualization |
| --- | --- |
| Converge | Cyan line from recipient to target; a minimum 0.25 m wire target marker. |
| SearchZone | Yellow line and a wire sphere using the assigned search radius. |
| Regroup | Magenta line and radius sphere, with a 0.5 m square center marker. |

Each living recipient's latest retained directive has one visualization. Reissued
orders replace its target; there is no history trail. Recipients sharing a target
have separate source lines, although their target spheres can overlap. Dead,
unissued, self-directed and malformed entries are skipped. Finite non-negative
radii below 0.25 m use that minimum visual marker size; the stored directive radius
is preserved. Surviving recipients' markers remain after Commander death while
the Commander object and its gizmo component still exist.

The renderer reads the Blackboard, uses value structs and indexed roster access,
and restores Gizmos color/matrix after drawing. It has no Update method and does
not deliver directives, tick a brain, move agents or allocate scene objects.

## Known weakness

The discrete squadAssembled fact simplifies physical coordination. As inherited
from the reference, Regroup can complete after 12 s if at least two other agents
are still alive, even if they have not reached the rally point. That can permit
Converge while the squad is still spread out. A future improvement would require
arrival evidence or mark regroup as failed on timeout. The current behavior is
visible in the Blackboard diagnostics and explicitly covered by a brain test.

The other owners' receiver implementations remain a separate integration handoff;
the fake coordination tests do not establish their runtime adoption.

## Validation and Editor demonstration

The G4 implementation was compiled and checked in Unity 6000.3.25f1:

| Suite | Latest G4 batch result | What it verifies |
| --- | --- | --- |
| Blackout.AI.Commander.Tests | 60 passed, 0 failed | Planner/brain regressions, debug formatting, costs, snapshot labels, bounded publication and directive geometry. |
| Blackout.Agents.Tests | 250 passed, 0 failed | Agent integration and prefab setup, including preservation of serialized assets on a setup rerun. |

Logs and XML results were written under the system temporary directory, outside
the repo. These batch checks validate data and wiring. Scene appearance still
requires visual inspection; this note does not claim it has been inspected.

Before the PR or viva:

1. Open Window > General > Test Runner > EditMode. Run the Commander suite and
   confirm 60 passes; run the Agents suite and confirm 250 passes.
2. Open Commander.prefab and confirm CommanderBrain has a config and
   CommanderDirectiveGizmos is enabled.
3. Start the mission with Agents loaded and a living Commander. Enable Scene
   Gizmos. Raise the existing CureTaken event asset through its Raise context menu.
   With no fresh sighting, expect yellow search areas for registered living units.
4. Use CommanderBrainTests.Regroup_IssuesOnceAndWaitsForTwoUnitsAtMidpoint to
   inspect the deterministic Regroup-to-Converge scenario. The cost-report test
   Plan_ListsActionCostsAndDistinguishesTotalFromRemainingCost checks the worked
   example's text. Real receivers still need their owners' directive handlers.
5. Kill the Commander during the mission. Confirm no new orders arrive and
   surviving units' retained markers remain while the Commander object exists.
   Check the Console for unexpected exceptions or compilation errors.

Five-line viva rehearsal:

1. The Commander uses shared facts to plan and issue squad orders, with readable
   costs and retained-order scene gizmos for inspection.
2. GOAP composes applicable actions into a sequence; an FSM would require explicit
   transitions for each combination of knowledge and readiness.
3. Shared interfaces and the Blackboard avoid dependencies on the other brains'
   concrete classes, allowing scene-free receiver tests.
4. Regroup 2 plus Converge 2 gives a plan cost of 4, preserving the reference's
   weights; a ready squad needs only Converge for 2.
5. The regroup timeout can label a surviving squad assembled before it physically
   arrives, which is a known weakness of this abstraction.

## Sources and scope

Mappings: `docs/reference/agent-controller.md` sections 6.1-6.3, checked against
the read-only prototype's `security_patrol_fsm.js`, `hunter_bt.js`, and
`tactical_guard_utility.js`. Delivery contract: `Assets/Scripts/Shared/Agents/Directive.cs`.
Runtime integration: `Assets/Scripts/AI/Commander/README.md`.

G3 covers the coordination contract and fake-receiver harness. G4 adds the current
goal/plan/Blackboard diagnostic text, latest-directive gizmos and the design/cost
discussion above. It does not add a plan-history log. Implementation references:
CommanderBrain.cs, CommanderConfig.cs, CommanderDebugText.cs,
CommanderDirectiveGizmos.cs and GoapPlanner.cs under Assets/Scripts/AI/Commander.
