# Commander coordination contract (G3)

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
brains already implement it. G3's scene-free fake-receiver harness will demonstrate
the mappings independently of their scenes and AI assemblies.

The G2 alert is a separate contract: `IAlertReceiver.OnAlert(AgentAlert)` supplies
the vision multiplier. Directive handling does not apply or stack alert buffs.

## Sources and scope

Mappings: `docs/reference/agent-controller.md` sections 6.1-6.3, checked against
the read-only prototype's `security_patrol_fsm.js`, `hunter_bt.js`, and
`tactical_guard_utility.js`. Delivery contract: `Assets/Scripts/Shared/Agents/Directive.cs`.
Runtime integration: `Assets/Scripts/AI/Commander/README.md`.

This G3 note covers coordination. GOAP debug UI, plan-history presentation and
the full Commander viva discussion belong to G4.
