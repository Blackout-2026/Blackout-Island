# Security Patrol structure, perception and viva notes (S1-S2)

Owner: World Builder, as assigned in the [build playbook](../UNITY_BUILD_PLAYBOOK.md),
section 2. Reference: [Agent Controller notes](../reference/agent-controller.md),
sections 3, 4, 5 and 6.1. Runtime code is in
`Assets/Scripts/AI/SecurityPatrol`, namespace and assembly
`Blackout.AI.SecurityPatrol`. Its only explicit assembly dependency is `Blackout.Shared`.

S1 establishes the FSM structure and intent templates. S2 adds testable vision
and hearing as separate C# objects. Automatic patrol/combat/search behaviour
and brain integration follow in S3; overlay text, gizmos and tuning are S4.

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

## Current state outputs (S1-S2)

| State | Movement intent | Look / aim / fire |
| --- | --- | --- |
| Patrol | Stop | No look target, no aim, no fire; waypoint behaviour follows in S3. |
| Investigate | Walk to the supplied target | No explicit look target, no aim, no fire. |
| Pursue | Run to the supplied target | No explicit look target, no aim, no fire. |
| Attack | Stop | Look at the supplied target, aim, fire false. |
| Search | Walk to the supplied target | Look at the target, aim, fire false. |

All five states still keep fire false. A `CanAttack` request is a structural
signal, not proof of visibility or range. S3 must add line-of-sight, range and
reaction guards before enabling the fire intent. The brain does not yet read
the S2 perception objects. Waypoint routes, arrival handling, search timers,
strafe selection, alert multipliers and Commander directive handling remain
later work. Config values are not read by these state templates yet.

## The current transition table

This matches `SecurityPatrolTransitions.cs`. The table records legal edges;
perception conditions and timers will generate the triggers in S3. Table row
order does not establish perception priority.

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

`SecurityPatrolConfig` defines the tuning type. S1-S2 do not create a saved config
asset or attach this brain to the existing agent prefab. A host passes its
perception fields to the `Perception` constructor; the initialization example
below shows the mapping.

| Field | Default | Source and purpose for later steps |
| --- | --- | --- |
| visionRange | 22 m | Reference patrol vision range. |
| visionAngle | About 111.73 degrees, full cone | Reference 1.95 radians converted with `Mathf.Rad2Deg`; a half-angle cone check must halve this value. |
| hearingRange | 65 m | Unity listener distance cap, chosen from the largest listed noise radius (sniper). The prototype instead uses each event's radius and a patrol hearing multiplier of 1.0; the cap is an explicit Unity simplification. |
| hearingThreshold | 0.04 | Normalized perceived loudness must be strictly greater than this prototype threshold. |
| attackRange | 22 m | Reference FSM engagement distance; weapon maximum range is separately 34 m. |
| walkSpeed | 1.55 m/s | Reference patrol walk speed. |
| runSpeed | 4.1 m/s | Reference patrol run speed. |
| searchTime | 14 s | Reference calm search duration; alert search is 30 s and follows in S3. |
| reactionDelay | 0.6 s | Reference engagement delay before shooting. |
| aimError | 0.034 radians | Reference base weapon spread, about 1.95 degrees, before contextual modifiers. |

The Inspector range/minimum attributes guide editing. `Perception` validates
its constructor settings: finite nonnegative ranges, full angle 0..360 degrees,
threshold 0..1, and a supplied raycast. Its settings are cached at construction;
later edits to the config do not retune an existing perception object. S3 must
handle alert tuning explicitly. `AgentIntent` carries a Walk/Run enum,
not a numeric speed or spread. Existing `AgentStatsApplier` configures movement
and weapon executors separately. Changing this config does not currently change
those executors, and S3 integration must keep their tuning consistent without
applying the reaction delay twice.

Use metres, +Y up and +Z north. The [level layout](../level-layout.md) supersedes
prototype route coordinates: prototype `(x,y,z)` becomes Unity `(x,y,-z)`.
Keep targets in Unity world coordinates.

## Perception outputs (S2)

`Perception` is a plain C# class, with no `MonoBehaviour`, Transform, clock or
scene search. Tests pass positions and a fake `IPerceptionRaycast`; runtime can
use `PhysicsPerceptionRaycast`. The existing brain remains independent until S3
connects these outputs to its transition requests.

| Output | Meaning |
| --- | --- |
| CanSeePlayer | Result of the most recent `UpdateVision` call, refreshed every call. |
| HasLastKnownPosition | Distinguishes no observation from a valid position at the world origin. |
| LastKnownPosition | Last confirmed sight point, or a remembered noise position when the player is not currently visible. A sound gives an investigation target, not proof the player is there. |
| HeardNoise | A pending audible noise exists since the last `ClearHeardNoise`. |
| NoisePosition | Position of the strongest pending noise; newest wins equal strength. |
| NoiseLoudness | Normalized perceived loudness of that pending noise. |

`ClearHeardNoise()` clears the pending flag, noise position and noise loudness,
while retaining the last-known position. `Forget()` also clears sight and all
last-known-position memory, for a reset or respawn. Neither method changes any
scene object or executes an intent.

### Vision and geometry

`UpdateVision(eyePosition, forward, playerPosition)` performs these checks:

1. Test the full 3D eye-to-target distance against the vision range.
2. Project facing and target direction onto XZ and compare their normalized dot
   product with `cos(fullAngle / 2)`. Facing need not be normalized. A zero
   horizontal forward cannot see; zero range disables sight.
3. Ask `IPerceptionRaycast.IsBlocked(eyePosition, playerPosition)` only for a
   candidate inside the range and cone. A clear ray updates the sight result
   and last-known position. A failed check preserves the earlier position.

Range and cone boundaries are inclusive. Range squared and the half-angle
cosine are cached. The Unity adapter casts at most one ray to the supplied
target point, with a caller-supplied mask and `QueryTriggerInteraction.Ignore`.
Use Level and Interactable for blocking walls and closed doors, excluding Player
and Enemy. A wall beyond the target cannot block the finite ray. Actual
occlusion depends on colliders and their assigned layers; the adapter does not
inspect visual transparency. Supply an eye origin outside blocking colliders.

The host chooses the player's sight point and the agent eye height; the class
does not add height offsets itself. S2 keeps the prototype's horizontal cone
and 3D range, but uses immediate visibility of one supplied point. Its awareness
meter, head/chest fallback and 2.2 m cone bypass are not implemented in this slice.

### Hearing formula and memory

The Systems Engineer's existing `NoiseEvent.loudness` contract already means
**radius in metres**. It is not a normalized volume or a value in decibels.
`HearNoise(listenerPosition, noise)` uses 3D distance and this formula:

```text
perceived = max(0, 1 - distance / noise.loudness)
audible   = hearingRange > 0
            and distance <= hearingRange
            and perceived > hearingThreshold
```

Zero, negative or nonfinite event radii are ignored. At radius 10 m, a noise
9.5 m away has perceived loudness 0.05 and is heard; at 9.7 m it is 0.03 and is
ignored by the default 0.04 threshold. The 65 m listener cap is separate from
each event's radius: a very loud event beyond the cap is still ignored. A quiet
event cannot reach 65 m merely because the listener cap permits that distance.

The strongest audible noise since the last clear wins. Equal strength replaces
the old noise with the newer position. `HearNoise` returns true when it stores
that event, false for an inaudible or weaker event. Hearing never changes
`CanSeePlayer`. A current sighting keeps priority for `LastKnownPosition`, while
the noise remains separately available through `NoisePosition`.

This S2 hearing uses open-space distance falloff with the patrol multiplier 1.0.
It has no wall attenuation, path flood, timestamps, decay or eight-second noise
history. Pending noise persists until explicitly cleared; the host must clear
it after every decision, including ticks when the player is visible.

### Channel lifetime and S3 integration

`PerceptionNoiseListener` subscribes to `NoiseEventChannel.OnRaised` once in its
constructor and forwards events using its supplied `Position`. With an owner
GameObject, it ignores that root and child sources, including its own weapon.
Null-source world noises are allowed. It caches the callback and owner Transform;
`Dispose()` unsubscribes and is safe to call repeatedly.

Initialization sketch for the future S3 host, whose config, channel, root and
world position are supplied here. Construct these outside the tick loop:

```csharp
var raycast = new PhysicsPerceptionRaycast(LayerMask.GetMask("Level", "Interactable"));
var senses = new Perception(config.visionRange, config.visionAngle, raycast,
    config.hearingRange, config.hearingThreshold);
var listener = new PerceptionNoiseListener(senses, noiseChannel, agentPosition, agentRoot);
```

The host must then:

1. Keep `listener.Position` current as the agent moves; it is a supplied world
   position, not a live Transform binding. Hearing uses the latest supplied value.
2. Refresh vision before choosing a transition. Prefer a current sighting;
   otherwise use `NoisePosition` for a pending noise and retain
   `LastKnownPosition` for later search behaviour.
3. Read the result and call `ClearHeardNoise()` after each decision, including
   ticks spent seeing the player. Clearing before reading loses pending events.
4. Dispose the listener on disable/death and before replacing it. Use `Forget()`
   for sensing resets/respawns and create a fresh listener when listening resumes.

The constructor needs a perception object and a channel. Supply the existing
`Assets/ScriptableObjects/Events/Noise.asset`; this step creates no replacement
channel, scene component or prefab wiring. Startup/subscription can allocate;
vision checks, event delivery and noise clearing are the measured hot paths.

## Verification and Editor checks

The EditMode assembly is `Blackout.AI.SecurityPatrol.Tests`:

| Fixture | Passed | Coverage |
| --- | --- | --- |
| SecurityPatrolBrainTests | 12 | S1 initialization, intent isolation, request timing/rejection, reset, death and warmed transitions. |
| PerceptionVisionTests | 13 | Inside/outside the cone, range boundary, horizontal facing/3D range, blocked memory, disabled vision and warmed checks. |
| PhysicsPerceptionRaycastTests | 3 | Real Level wall moved out of the ray, ignored triggers/layers and walls beyond the target. |
| PerceptionHearingTests | 16 | Below/above/exact threshold, event radius, listener cap, invalid settings, strongest noise, sight priority and reset. |
| PerceptionNoiseListenerTests | 7 | Initial/updated position, own-source filtering, unsubscribe, independent listeners and warmed channel events. |

Verified with Unity 6000.3.25f1 on 2026-10-20: batch exit 0, **51 passed, 0 failed,
0 skipped**, duration 0.4413188 s, and no C# compiler errors or warnings. Tests use
position inputs, fake contexts/queries and temporary GameObjects/channel
instances; no gameplay scene or prefab is saved. All eight new S2 `.meta` files
were generated by Unity batch imports.

After warm-up, each of these measured zero managed bytes on the calling thread:
128 FSM cycles containing 1,152 accepted transitions; 1,024 vision checks with a
fake raycast; and 1,024 channel raise/clear cycles. These measurements exclude
startup/subscription, Unity physics internals and movement/weapon executors.

Latest evidence is outside Git at
`%TEMP%/blackout-s2-hearing-tests-20261020-212625.log` and matching `.xml`.
The earlier vision chunk passed 28/28 at
`%TEMP%/blackout-s2-vision-tests-approved-20261020-210858.log` and matching `.xml`.
Its initial sandboxed run could not maintain the licence-client connection;
that task's batch process was stopped and the approved retry passed. Successful
runs still logged an unavailable licensing access token. These are focused
EditMode results; integrated gameplay and the whole-project suite were not run.

Before opening the PR:

1. Open **Window > General > Test Runner**, select **EditMode**, and run
   `Blackout.AI.SecurityPatrol.Tests`; expect 51 passes, including all four
   perception fixtures above.
2. Check that **Assets > Create > Blackout > Security Patrol Config** exists.
   Inspect a temporary config's Vision Range 22, Vision Angle about 111.73,
   Hearing Range 65 and Hearing Threshold 0.04; do not commit that temporary asset.
3. In an unsaved scene, add **Security Patrol Brain** to a temporary empty
   GameObject and inspect its Config field. Discard the scene afterward. The
   component alone does not initialize, move or shoot.
4. Confirm the Console has no new C# errors and the staged files belong to this
   step. Shared contracts, agent prefabs/scenes, project settings and prototype
   remain outside this change.

No textures, models, audio or replacement placeholders are required for S1-S2.
For later integration, ask the Agent Controller: "After S3, wire
SecurityPatrolBrain into Assets/Prefabs/Agents/SecurityPatrol.prefab using your
AgentRoot host; coordinate the owned config and executor movement/weapon tuning."

## Five-line viva summary

1. Does: supplies an intent-only patrol FSM plus testable vision, geometry occlusion and channel-based hearing.
2. Design: position inputs, an injected raycast and a disposable listener separate sensing from action execution.
3. Alternative: rejected querying Transforms, Physics and the noise channel separately inside every state, which would duplicate sensing and complicate tests.
4. Number: 0.04 is the prototype's strict normalized hearing threshold; a 10 m-radius noise at 9.5 m yields 0.05 and is heard.
5. Weakness: one-ray vision and distance-only hearing simplify the prototype; S3 must still connect sensing to automatic transitions and manage its lifecycle.
