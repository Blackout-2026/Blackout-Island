# Security Patrol states, setup and viva notes (S1-S3)

Owner: World Builder (Security Patrol AI), under section 2 of the
[build playbook](../UNITY_BUILD_PLAYBOOK.md). Behaviour and numbers come from the
[Agent Controller reference](../reference/agent-controller.md), sections 3, 5,
6.1 and 8; waypoint positions follow the [level layout](../level-layout.md).
The user reported Agent Controller approval for the S3 executor and prefab edits.

S3 connects the five-state FSM to sensing, waypoints, combat and world alerts,
and wires the existing Security Patrol prefab. Overlay text, gizmos and further
tuning remain S4. The existing humanoid capsule is still a visual placeholder.

## Decision and execution boundary

[SecurityPatrolBrain](../../Assets/Scripts/AI/SecurityPatrol/SecurityPatrolBrain.cs)
implements `IAgentBrain` and `IAlertReceiver`. Its runtime assembly,
`Blackout.AI.SecurityPatrol`, references only `Blackout.Shared`. It has no
`Update` method: the existing `AgentRoot` calls `Tick(deltaTime)` at its default
10 Hz think rate, then passes the returned `AgentIntent` to movement, facing,
animation and shooting executors. The brain never moves a Transform or fires a shot.

1. `Initialize(context)` caches five state objects and the agent's waypoint
   route, creates the sensing host, and enters Patrol. Reinitialization resets
   route progress, timers, pending requests and player memory.
2. Each runtime tick refreshes alert range, samples sight and pending noise,
   and passes a value-type `PatrolPerceptionInput` to `Tick(dt, input)`.
3. Confirmed sight outranks noise and state completion. The brain advances the
   current state's timer, evaluates guards, applies at most one transition,
   and returns that state's intent. A confirmed sighting also publishes the
   player's feet position and `Time.time` to the shared Blackboard.
4. `TryRequestTransition(trigger, target)` remains an explicit test/control seam:
   the first legal request queues a copied world position and takes priority
   over automatic transitions on the next tick. An accepted request alone does
   not authorize firing; sight and range are still required.

Each cached state has `Enter`, `Advance`, `Tick` and `Exit`. State changes reuse
objects; they do not allocate a new state. `Tick` returns idle before initialization,
while disabled, with a destroyed Self, or after the agent dies. Tests can inject
an `IPerceptionRaycast` at initialization or supply `Tick(dt, input)` directly;
the latter deliberately bypasses sensor validation.

## State behaviour and transition guards

| State | Intent and completion |
| --- | --- |
| Patrol | Walk the owned waypoint loop. Within 0.9 m, stop and sweep the look target for 2.2 s before advancing. No route means idle. Audible noise starts Investigate; sight starts Pursue or Attack. |
| Investigate | Walk to the copied noise position. Within 1.5 m, stop and look for 3.5 s; leaving that radius resets the look timer. Complete to Patrol when calm, or Search at the noise position during a cure/Blackboard alarm. Sight interrupts either case. |
| Pursue | Run to the last confirmed player feet position, updating it only while visible. When unseen, enter Search on arrival within 1.5 m, or when sight has been lost for more than 2.5 s and Pursue has lasted at least 4 s. |
| Attack | Stop, aim at feet plus the 1.116 m chest offset, and request fire only with current sight and distance within attackRange. Lost sight stops fire immediately; after 1 s it returns to Pursue. A visible player beyond attackRange times 1.15 also causes Pursue. |
| Search | Walk and aim toward the last known position; within 1.2 m, stop and sweep the look target. Give up to Patrol after searchTime, measured from entry including travel. Sight interrupts the timeout. |

Visible players within engagement range enter Attack directly; more distant
visible players enter Pursue. Between attackRange and its 1.15 exit boundary,
Attack holds its state but withholds fire. Hidden live player positions and
noises do not redirect a pursuit. A later noise does not retarget an existing
investigation. Search always finishes after its selected duration, including
while an alarm remains active; this is the requested bounded search loop.

The single [transition table](../../Assets/Scripts/AI/SecurityPatrol/SecurityPatrolTransitions.cs)
contains these 13 legal edges. Row order does not establish sensing priority.

| From | Trigger | To |
| --- | --- | --- |
| Patrol | HeardNoise | Investigate |
| Patrol | DetectedPlayer | Pursue |
| Patrol | CanAttack | Attack |
| Investigate | DetectedPlayer | Pursue |
| Investigate | CanAttack | Attack |
| Investigate | InvestigationComplete | Patrol |
| Investigate | InvestigationAlerted | Search |
| Pursue | CanAttack | Attack |
| Pursue | LostPlayer | Search |
| Attack | CannotAttack | Pursue |
| Search | DetectedPlayer | Pursue |
| Search | CanAttack | Attack |
| Search | SearchComplete | Patrol |

Unknown/unlisted triggers, uninitialized/dead/disabled requests and a second
pending request are rejected. Keep the state enum order aligned with the cached
state array when extending this implementation.

Routes use `Waypoint` markers with Owner exactly equal to the initialized
agent's name, such as `Patrol-1`. Sort by ordinal marker Id, using padded names
such as `Waypoint_Patrol-1_01`, and wrap to the first point. Patrol-3's return
leg is already encoded as a four-point loop in the layout. Route progress
survives a temporary investigation/chase; a replaced marker list or changed
count rebuilds the cache and restarts it. In-place owner/Id edits with the same
list/count require reinitialization. Use metres, +Y up and +Z north; prototype
`(x,y,z)` maps to Unity `(x,y,-z)`, with yaw negated. Player inputs use feet/root
positions; sensor and aim heights are applied separately.

## Saved tuning and shooting

The saved [config asset](../../Assets/Scripts/AI/SecurityPatrol/Config/SecurityPatrolConfig.asset)
uses these defaults from the reference unless a Unity choice is identified.

| Field | Default | Meaning |
| --- | --- | --- |
| eyeHeight | 1.62 m | Agent sight-ray origin above feet. |
| playerSightHeight | 1.62 m | Player sight-ray target above feet; one eye-to-eye ray. |
| visionRange | 22 m | Calm sight distance. |
| visionAngle | 111.726776 degrees | Full cone: reference 1.95 radians converted to degrees. |
| hearingRange | 65 m | Unity cap chosen from the largest reference noise radius, the sniper. |
| hearingThreshold | 0.04 | Perceived loudness must be strictly greater than this value. |
| attackRange | 22 m | FSM engagement range, separate from the weapon's 34 m maximum. |
| walkSpeed | 1.55 m/s | Baseline executor walk speed. |
| runSpeed | 4.1 m/s | Baseline executor run speed. |
| searchTime | 14 s | Calm search, including travel. |
| alertSearchTime | 30 s | Cure/Blackboard alarm search, including travel. |
| aimHeight | 1.116 m | Reference player height 1.8 m times chest fraction 0.62. |
| reactionDelay | 0.6 s | Applied by the shared shooter once per engagement. |
| aimError | 0.034 radians | Base shot spread, about 1.95 degrees. |

The brain emits Walk/Run enums, aim/fire booleans and targets. Movement speed,
reaction delay and spread are executor settings. The setup tool copies their
baseline values to the prefab; runtime spawning still applies `DifficultyConfig`.
If changing those four config fields, keep the Security Patrol difficulty row
consistent and rerun setup for manually placed prefabs. Existing asset tuning
is preserved by setup. Sensor distances, cone, hearing settings and heights are
cached when sensing is initialized; restart Play Mode after editing them.

The existing [AgentShooter](../../Assets/Scripts/Agents/AgentShooter.cs) waits
0.6 s before its first shot, checks facing/range, and applies randomized aim
error. It resets engagement when fire becomes false. The patrol brain adds
neither a second reaction timer nor another random aim offset. The baseline
weapon uses range 34 m, bursts of 3, interval 0.12 s and pause 1.05 s.
`ShotMath` scales spread with distance and early engagement; the shared shooter
also applies the heightened aim-error multiplier. Player speed/crouch spread
terms from the prototype are not implemented by that executor.

## Sensing, alerts and lifetime

[PatrolSensing](../../Assets/Scripts/AI/SecurityPatrol/PatrolSensing.cs) owns Unity
queries and the disposable noise listener. The underlying `Perception` remains
a plain C# object taking positions and an injected raycast. Vision checks 3D
range, the horizontal half-cone dot product, then at most one finite ray.
Range/cone boundaries are inclusive. The prefab geometry mask is Default,
Level and Interactable (769), excluding Enemy/Player; triggers are ignored.
Actual occlusion depends on colliders and layers, including closed doors.

`NoiseEvent.loudness` means radius in metres. Hearing uses
`perceived = max(0, 1 - distance / radius)`, requires distance at most 65 m and
perceived loudness strictly above 0.04. For a 10 m radius, 9.5 m yields 0.05 and
is heard; 9.7 m yields 0.03 and is ignored. Invalid/nonpositive radii are ignored.
The strongest pending noise wins, newest breaks ties, and own-root/child sources
are ignored. `FollowOwnerPosition` reads the cached owner's current position
when the event arrives. The sensing host reads and then clears pending noise
every think tick, including while seeing the player.

`Perception` exposes current sight, a last-known-position validity flag and
position, plus pending noise flag/position/loudness. Losing sight retains its
last confirmed position; a sound may update that memory only when not seeing.
The FSM separately retains confirmed player feet positions for chase/search.
`ClearHeardNoise` clears pending sound, `ClearVision` clears current sight, and
`Forget` clears all sensor memory.

| Blackboard situation | Executor/detection effect | Search duration |
| --- | --- | --- |
| Calm | Base settings. | 14 s. |
| generatorDisabled only | Security Patrol/Tactical Guard: speed times 1.25, vision and engagement range times 1.4, aim error times 0.9. | 14 s. |
| cureTaken | Same buffs for all living agent kinds, even without a living Commander. | 30 s. |
| Configured alert broadcast | Its absolute multipliers take precedence over the flag fallback. A positive alertLevel also selects alarm search. | 30 s if cureTaken or alertLevel is positive; otherwise 14 s. |

[AgentRoot](../../Assets/Scripts/Agents/AgentRoot.cs) synchronizes executor buffs
before thinking and during initialization, then notifies `IAlertReceiver`.
Assignments are absolute and identical updates are cached: generator, cure and
broadcast buffs do not stack. The patrol also reads Blackboard flags directly
for its range fallback. Thus 22 m becomes 30.8 m; the cone angle is unchanged.
A local fallback does not synthesize a global broadcast or Commander orders.
Spawner entries assign agent Kind; other manually placed type prefabs need
their correct Kind configured. The Security Patrol setup sets its Kind explicitly.

Disable/death disposes sensing and clears pending transitions/player memory.
Re-enable binds fresh sensing when the brain can think. The existing
`PlayerRespawned` channel resets the FSM to Patrol and clears sensing, retaining
route progress and alert settings. Player replacement, loss or death clears
old chase memory; cached Health is looked up only when the player Transform
changes. Blackboard `playerNeutralized` also prevents sight/fire.

Deliberate reference simplifications: immediate single-ray sight without an
awareness meter, head/chest fallback or close-range cone bypass; distance-only
hearing without wall attenuation or eight-second history; no attack strafing
or urgent/retargeted investigation; fixed-position bounded search instead of
random wandering and repeated alarm searches. There is no unreachable-target
recovery: Investigate can remain travelling indefinitely. Commander directives
and S4 debug overlay/gizmos are not implemented here.

## Prefab setup and Editor checks

Run **Blackout > World > Setup Security Patrol**. The
[setup tool](../../Assets/Editor/WorldBuilder/SecurityPatrolSetup.cs) reuses or
creates the owned config, selects exactly one FSM on the existing
`Assets/Prefabs/Agents/SecurityPatrol.prefab` variant, binds existing Noise and
PlayerRespawned channels, sets Kind and matches executor baselines. It rejects
conflicting brains/assets rather than deleting content. It never saves scenes.
Batch entry `Blackout.Editor.SecurityPatrolSetup.BuildAndVerify` runs setup
twice and checks identical output plus eight protected-file hashes.

Before opening the PR:

1. Inspect the SecurityPatrol prefab: one enabled SecurityPatrolBrain, saved
   Config, Noise and PlayerRespawned fields, correct geometry mask, and AgentRoot
   Kind SecurityPatrol with that brain selected. Confirm movement and shooter
   values match the table; the existing `Visual/PLACEHOLDER_Humanoid` capsule
   still needs a character model/rig and animation clips for finished visuals.
2. Open **Window > General > Test Runner > EditMode** and run the
   `Blackout.AI.SecurityPatrol.Tests` and `Blackout.Agents.Tests` assemblies;
   expect 121 and 267 passes respectively.
3. Play from Bootstrap with Environment and Agents loaded. Watch patrols loop
   their owned routes and pause. Create an audible noise while hidden, then
   expose the player: investigation should yield to pursuit/attack. Check the
   first-shot delay and miss variation; use a wall to stop fire, then wait for
   the last-position search to expire and patrol to resume.
4. Use the existing generator/cure interactions to check faster movement and
   wider detection, including cure heightening without a living Commander.
   Generator-only search remains 14 s; cure search is 30 s and still gives up.
5. Check disable/re-enable, agent death and player respawn for stale chasing,
   firing or duplicate noise callbacks. Keep the Console clear. These live
   Play Mode and visual checks have not been performed by the batch runs.

## Recorded verification

Unity 6000.3.25f1, 2026-10-21: combined EditMode run exited 0 with **388 passed,
0 failed, 0 skipped**, duration **1.4877973 s**. This covers the two named
assemblies; it is not a whole-project test result.

| Patrol fixture | Passed | Main coverage |
| --- | --- | --- |
| SecurityPatrolBrainTests | 12 | Initialization, explicit transitions, reset/death and intent isolation. |
| SecurityPatrolStateBehaviourTests | 13 | Owned routes, arrival/pause, investigate/search completion and automatic edges. |
| SecurityPatrolCombatTests | 19 | Pursuit/attack guards, sight priority, hysteresis, actual shooter delay/reset and perturbed hit. |
| SecurityPatrolSensingTests | 29 | Live sensing adapter, flags, shared executor integration, lifecycle memory and warmed ticks. |
| SecurityPatrolSetupTests | 5 | Saved variant/config/channels/executors/difficulty and actual scene spawner reference. |
| PerceptionVisionTests | 17 | Cone/range boundaries, occlusion memory and absolute range multipliers. |
| PhysicsPerceptionRaycastTests | 3 | Real wall, trigger/layer exclusions and finite ray length. |
| PerceptionHearingTests | 16 | Threshold/radius/cap, strongest noise, sight priority and reset. |
| PerceptionNoiseListenerTests | 7 | Position, own-source filtering, unsubscribe and independent listeners. |

Patrol total: **121**. The **267 Agent Controller tests** include world-flag
eligibility, nonstacking configured alerts, Commander death, late initialization,
spawning and replacement-brain replay. Tests use fake perception/context inputs;
selected integrations also exercise real physics, noise channels and shooting.
The randomized-hit test fixes seed 17 and restores Unity's previous random state.
EditMode lifecycle tests invoke managed callbacks through reflection; they do
not prove Unity's live Play Mode callback ordering.

Allocation assertions pass after warm-up: 128 automatic patrol/combat/sensing
cycles and 128 alerted host think ticks allocate zero managed bytes on the
calling thread; earlier 1,024 vision and 1,024 channel cycles remain covered.
Initialization, route cache rebuilds, physics internals and other executors are
outside those measurements.

Setup verification exited 0: the second prefab/config/config-meta output was
byte-identical. The existing prefab GUID, base/other agent prefabs, placeholder
material, Environment and Agents scenes remained unchanged (eight protected
files). Unity generated four new metadata files. Both setup and test logs had
zero C# compiler errors/warnings; the existing licensing access-token message
persisted without preventing success.

Evidence is outside Git at `%TEMP%/blackout-s3-prefab-setup.log`,
`%TEMP%/blackout-s3-prefab-tests.log` and matching `.xml`. No integrated gameplay,
rendered appearance or real Play Mode lifecycle validation is claimed.

## Five-line viva summary

1. Does: patrols owned waypoints, investigates sound, pursues/attacks visible players, then searches and returns to patrol.
2. Design: cached FSM states consume value snapshots and return intents; separate sensing and executors own Unity queries and actions.
3. Alternative: rejected a second brain-side reaction timer and random aim offset because the shared shooter already applies both.
4. Number: the reference 1.4 alert range multiplier raises 22 m detection/engagement to 30.8 m without stacking or widening the cone.
5. Weakness: investigation has no unreachable-target timeout; distance-only hearing also allows sound through walls.
