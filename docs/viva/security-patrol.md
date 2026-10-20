# Security Patrol structure and viva notes (S1)

Owner: World Builder, as assigned in the [build playbook](../UNITY_BUILD_PLAYBOOK.md),
section 2. Reference: [Agent Controller notes](../reference/agent-controller.md),
sections 3, 5 and 6.1. Runtime code is in
`Assets/Scripts/AI/SecurityPatrol`, namespace and assembly
`Blackout.AI.SecurityPatrol`. Its only explicit assembly dependency is `Blackout.Shared`.

S1 establishes the FSM structure and intent templates. Perception is S2; automatic
patrol/combat/search behaviour is S3; overlay text, gizmos and tuning are S4.
These notes describe the current structure and can be expanded in those steps.

## How a tick works

`SecurityPatrolBrain` is a `MonoBehaviour` implementing `IAgentBrain`. The host
initializes it with an `IAgentContext`, then calls `Tick(deltaTime)` at its think
rate. The existing `AgentRoot` defaults to 0.1 seconds (10 Hz); this brain has no
`Update` method or independent clock.

1. `Initialize` validates the context and its Self Transform, creates five state
   objects on first use, and enters Patrol. Reinitializing exits the old state,
   clears pending work and reuses the state objects.
2. `TryRequestTransition(trigger, target)` checks the single legal-transition
   table. It copies the supplied world target and queues the destination state.
   The first accepted request wins until the next tick. Invalid requests do not
   occupy that slot. S3 must check higher-priority conditions first.
3. `Tick` exits the old state, enters the queued state, and returns the active
   state's `AgentIntent`. At most one transition occurs per tick.
4. The host passes that intent to separate movement, facing, animation and
   shooting components. The brain never executes those actions.

Before initialization, with a destroyed Self, or with a supplied Health that is
dead, `Tick` returns idle. `CurrentState` is null before initialization. A queued
request is not applied while the brain returns idle for a dead agent.

`State` is an abstract C# class with `Enter`, `Tick` and `Exit`. Enter stores the
target, Tick returns an intent, and Exit clears the target. Each brain owns its
five cached state instances. Keep the `SecurityPatrolState` enum order aligned
with the state array in `Initialize` when extending the code.

## State outputs in S1

| State | Movement intent | Look / aim / fire |
| --- | --- | --- |
| Patrol | Stop | No look target, no aim, no fire; waypoint behaviour follows in S3. |
| Investigate | Walk to the supplied target | No explicit look target, no aim, no fire. |
| Pursue | Run to the supplied target | No explicit look target, no aim, no fire. |
| Attack | Stop | Look at the supplied target, aim, fire false. |
| Search | Walk to the supplied target | Look at the target, aim, fire false. |

All five states keep fire false in S1. A `CanAttack` request is a structural
signal, not proof of visibility or range. S3 must add line-of-sight, range and
reaction guards before enabling the fire intent. No perception, waypoint route,
arrival handling, search timer, strafe selection, alert multiplier, or Commander
directive handling is implemented here. Config values are not read by the S1
state templates yet.

## The current transition table

This matches `SecurityPatrolTransitions.cs`. The table records legal edges;
reference conditions, awareness thresholds and timers will generate the triggers
in later steps. Table row order does not establish perception priority.

| From | Trigger | To |
| --- | --- | --- |
| Patrol | HeardNoise | Investigate |
| Patrol | DetectedPlayer | Pursue |
| Patrol | CanAttack | Attack |
| Investigate | DetectedPlayer | Pursue |
| Investigate | CanAttack | Attack |
| Investigate | InvestigationComplete | Patrol |
| Pursue | CanAttack | Attack |
| Pursue | LostPlayer | Search |
| Attack | CannotAttack | Pursue |
| Search | DetectedPlayer | Pursue |
| Search | CanAttack | Attack |
| Search | SearchComplete | Patrol |

Any unlisted pair, unknown trigger, uninitialized request or second pending
request returns false. Accepted requests change state at the next Tick, not
inside `TryRequestTransition`. The target is copied by value; it does not track
the player automatically. S3 will extend the table and state data for the
reference's alert and directive paths.

## Configuration defaults and units

`SecurityPatrolConfig` defines the tuning type. S1 does not create a saved config
asset or attach this brain to the existing agent prefab.

| Field | Default | Source and purpose for later steps |
| --- | --- | --- |
| visionRange | 22 m | Reference patrol vision range. |
| visionAngle | About 111.73 degrees, full cone | Reference 1.95 radians converted with `Mathf.Rad2Deg`; a half-angle cone check must halve this value. |
| hearingRange | 65 m | Provisional Unity hearing cap, chosen from the largest listed noise radius (sniper). The prototype has no fixed patrol hearing range: it uses each noise radius and a hearing multiplier of 1.0. Revisit in S2. |
| attackRange | 22 m | Reference FSM engagement distance; weapon maximum range is separately 34 m. |
| walkSpeed | 1.55 m/s | Reference patrol walk speed. |
| runSpeed | 4.1 m/s | Reference patrol run speed. |
| searchTime | 14 s | Reference calm search duration; alert search is 30 s and follows in S3. |
| reactionDelay | 0.6 s | Reference engagement delay before shooting. |
| aimError | 0.034 radians | Reference base weapon spread, about 1.95 degrees, before contextual modifiers. |

The Inspector range/minimum attributes guide editing; they do not validate
arbitrary values assigned by other code. `AgentIntent` carries a Walk/Run enum,
not a numeric speed or spread. Existing `AgentStatsApplier` configures movement
and weapon executors separately. Changing this config does not currently change
those executors, and S3 integration must keep their tuning consistent without
applying the reaction delay twice.

Use metres, +Y up and +Z north. The [level layout](../level-layout.md) supersedes
prototype route coordinates: prototype `(x,y,z)` becomes Unity `(x,y,-z)`.
Keep targets in Unity world coordinates.

## Verification and Editor checks

The EditMode assembly is `Blackout.AI.SecurityPatrol.Tests`. Its 12 tests cover
initialization, intent isolation, request timing/rejection, return from an
investigation, stale aim flags, pending-request reset, destroyed Self, death,
and allocations during repeated state changes. Tests use an `IAgentContext`
fake and temporary GameObjects; no gameplay scene or prefab is saved.

Verified with Unity 6000.3.25f1 on 2026-10-20: batch exit 0, 12 passed, 0 failed,
0 skipped, and no C# compiler errors or warnings. After eight warm-up cycles,
128 cycles containing 1,152 accepted transitions allocated zero managed bytes
on the calling thread. This measurement covers the structural brain, not the
cost of future perception or the movement/weapon executors.

Evidence is outside Git at `%TEMP%/blackout-s1-fsm-tests-20261020-203036.log`
and the matching `.xml`. Unity logged an unavailable licensing access token;
the test runner still reported success. Commit 1's batch import/compile also
exited 0 and generated its metadata. No integrated gameplay test is claimed.

Before opening the PR:

1. Open **Window > General > Test Runner**, select **EditMode**, and run
   `Blackout.AI.SecurityPatrol.Tests`; expect 12 passes.
2. Check that **Assets > Create > Blackout > Security Patrol Config** exists.
3. In an unsaved scene, add **Security Patrol Brain** to a temporary empty
   GameObject and inspect its Config field. Discard the scene afterward. The
   component alone does not initialize, move or shoot.
4. Confirm the Console has no new C# errors and the staged files belong to this
   step. Shared contracts, agent prefabs/scenes, project settings and prototype
   remain outside this change.

No textures, models, audio or replacement placeholders are required for S1.
For later integration, ask the Agent Controller: "After S3, wire
SecurityPatrolBrain into Assets/Prefabs/Agents/SecurityPatrol.prefab using your
AgentRoot host; coordinate the owned config and executor movement/weapon tuning."

## Five-line viva summary

1. Does: provides five FSM states, one transition table, intent outputs and tuning data.
2. Design: cached states separate decisions from the mover, animation and shooter.
3. Alternative: a behaviour tree is useful for nested reactive tasks; this small patrol sequence is easy to trace with explicit FSM edges.
4. Number: 22 m is the reference attack decision range, separate from the weapon's 34 m maximum.
5. Weakness: S1 still needs S2 perception and S3 automatic behaviour and combat guards before it is a working patrol.
