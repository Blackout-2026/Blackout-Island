# Security Patrol: FSM, tuning and debugging (S1-S4)

Owner: World Builder (Security Patrol AI), under section 2 of the
[build playbook](../UNITY_BUILD_PLAYBOOK.md). Behaviour and numbers come from the
[Agent Controller reference](../reference/agent-controller.md), sections 3, 5,
6.1 and 8; waypoint positions follow the [level layout](../level-layout.md).
The user reported Agent Controller approval for the S3 executor and prefab edits.

The five-state FSM connects sensing, owned waypoints, combat intents and world
alerts on the existing Security Patrol prefab. S4 adds shared debug text and
selected-agent Scene-view gizmos, and retains the S3 tuning baseline. The
existing humanoid capsule is still a visual placeholder.

## Why an FSM fits this role

A patrol has five distinct phases with explicit entry and exit conditions:
walk a route, investigate a sound, pursue a sighting, attack, then search before
returning to the route. Only one phase controls its intent at a time. An FSM
makes that sequence easy to explain: each state owns its target/timers, one
table defines legal edges, and the brain evaluates guards in a fixed order.
A visible threat interrupts routine work without a planner or score comparison.

Five cached state objects and a 13-edge table are enough for this scope. Tests
can inject one perception snapshot and inspect both the returned intent and
the applied transition reason. The design uses ordinary C# rather than an AI
framework; the same decision code runs without a model or animation rig.

| Approach | Fit for this patrol | Where it would improve on this FSM |
| --- | --- | --- |
| FSM, chosen | One exclusive phase, explicit guards and small state-local memory match the route/response loop. | As more tactics and interruptions are added, the edge table and central priority checks grow; shared conditions can become repetitive. |
| Behaviour tree | A selector and running sequences could express the loop, but would also need status, reset and interruption rules for this small set of phases. | Reusable subtrees and reactive priority suit a Hunter with nested flank, cover and ambush sequences. This flat FSM has no subtree composition. |
| Utility AI | The current patrol uses hard conditions such as sight, range and timeouts; it does not need a continuous preference score for each phase. | Scored tradeoffs suit a guard choosing cover, pressure or holding a post. This FSM cannot smoothly rank several acceptable tactics; extra guards would be required. |
| GOAP | The patrol reacts to observations and follows a fixed loop; it has no multi-action goal to search for. Symbolic preconditions, effects and costs add work here. | A planner can find action sequences for a Commander coordinating a squad as world facts change. This FSM only follows explicitly authored edges and cannot discover a new plan. |

The FSM is not intrinsically faster or better than those alternatives. Its
advantage here is that five mutually exclusive phases stay readable. If the
patrol gained independent cover, reload, flanking and order-following tasks,
a hierarchical FSM or behaviour tree would be worth reconsidering. Movement
and weapon timing already run in separate executors; they do not require
parallel branches inside this brain.

## Decision and execution boundary

[SecurityPatrolBrain](../../Assets/Scripts/AI/SecurityPatrol/SecurityPatrolBrain.cs)
implements `IAgentBrain`, `IAlertReceiver` and `IDebugInfo`. Its runtime assembly,
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

| From | Trigger | To | Automatic guard |
| --- | --- | --- | --- |
| Patrol | HeardNoise | Investigate | No sight; a pending audible noise supplies the target. |
| Patrol | DetectedPlayer | Pursue | Sight, with distance greater than effective attackRange. |
| Patrol | CanAttack | Attack | Sight, within effective attackRange. |
| Investigate | DetectedPlayer | Pursue | Sight, with distance greater than effective attackRange. |
| Investigate | CanAttack | Attack | Sight, within effective attackRange. |
| Investigate | InvestigationComplete | Patrol | No sight; completed 3.5 s looking within 1.5 m of the noise; no cure/Blackboard alarm. |
| Investigate | InvestigationAlerted | Search | Same investigation completion, with cureTaken or positive alertLevel. |
| Pursue | CanAttack | Attack | Sight, within effective attackRange. |
| Pursue | LostPlayer | Search | No sight; within 1.5 m of last seen position, or lost for more than 2.5 s with at least 4 s spent pursuing. |
| Attack | CannotAttack | Pursue | No sight for at least 1 s, or a visible target beyond effective attackRange times 1.15. |
| Search | DetectedPlayer | Pursue | Sight, with distance greater than effective attackRange. |
| Search | CanAttack | Attack | Sight, within effective attackRange. |
| Search | SearchComplete | Patrol | No sight; time in Search reaches the selected 14/30 s duration, including travel. |

These guards describe automatic decisions. The explicit
`TryRequestTransition` seam checks whether an edge is legal; it does not
reevaluate that trigger's sensory guard. An explicit transition into Attack
still cannot fire without the tick's confirmed sight and engagement range.

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
has all 14 fields from
[SecurityPatrolConfig](../../Assets/Scripts/AI/SecurityPatrol/SecurityPatrolConfig.cs)
listed below. S4 changes no gameplay values. These justifications explain the
reference baseline and explicit Unity choices; they are not measured playtest
results. Distances are metres, speeds metres/second, and durations seconds.

| Field | Default | Justification |
| --- | --- | --- |
| eyeHeight | 1.62 m | Reference humanoid sensor height places the ray near the head rather than at the floor; patrol body/root positions remain feet positions. |
| playerSightHeight | 1.62 m | Unity choice: use the same sight height for the single eye-to-eye test. It provides one consistent target point; it does not implement a second chest ray. |
| visionRange | 22 m | Reference Patrol sight range preserves its local security role and stays below the weapon's 34 m maximum. |
| visionAngle | 111.726776 degrees | Reference full FOV 1.95 radians converted to Unity degrees. Perception compares against half this angle; the alert changes range, not FOV. |
| hearingRange | 65 m | Unity listener cap covers the largest listed reference noise radius, the sniper. The event's own radius still controls falloff. |
| hearingThreshold | 0.04 | Reference shared audibility threshold, strictly exceeded to ignore the faint end of falloff. Unity reacts directly to audible noise; the prototype Patrol's separate 0.1/age gate is omitted. |
| attackRange | 22 m | Reference FSM engagement distance matches baseline vision. Even heightened, 22 times 1.4 = 30.8 m remains within the baseline weapon's 34 m limit. |
| walkSpeed | 1.55 m/s | Reference Patrol pace keeps routine patrol/investigation slower than pursuit; applied by the mover through the Walk intent. |
| runSpeed | 4.1 m/s | Reference Patrol chase speed distinguishes pursuit from routine walking; applied by the mover through Run. |
| searchTime | 14 s | Reference calm search duration gives the player a bounded hiding window. The Unity timer includes travel so Search always finishes. |
| alertSearchTime | 30 s | Reference alarm duration makes cure/Blackboard searches persist longer. Generator-only heightening retains 14 s, separating heightened senses from an active alarm. |
| aimHeight | 1.116 m | Existing reference mapping uses a 1.8 m player height times chest fraction 0.62, keeping aim below the sight ray. The asset's 1.1159999 is float serialization of this value. |
| reactionDelay | 0.6 s | Reference Patrol first-shot delay gives a response window. The shared shooter applies it once; a second brain timer would double the intended delay. |
| aimError | 0.034 radians | Reference base spread, about 1.95 degrees, avoids perfect hitscan accuracy. The shooter applies distance/early-engagement and alert modifiers once. |

State constants remain in readable state/guard code rather than additional
config fields: 0.9 m arrival and 2.2 s pause come from the reference patrol
loop; 1.5 m arrival and 3.5 s looking from investigation; 1.2 m arrival from
search. Pursuit's lost-sight >2.5 s / state >=4 s gate and Attack's 1 s grace /
1.15 range hysteresis also follow reference section 6.1. The hysteresis holds
Attack through the outer 15% band to reduce state oscillation, while fire is
withheld immediately outside the inner attack range. These are intent/state
thresholds; the mover owns pathfinding and physical stopping distance.

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
recovery: Investigate can remain travelling indefinitely. Hearing also omits
the prototype Patrol's separate 0.1 loudness/1.5 s reaction gate. Commander
directives are not handled by this brain.

## Debug text and Scene-view gizmos

`IDebugInfo.GetDebugText()` returns state and the last applied transition, for
example:

```text
Security Patrol [FSM] Attack | last transition: Patrol -> Attack (CanAttack)
```

The agent's unique name remains the Blackboard roster key. The existing
`AgentRoot` reads `IDebugInfo` at its default 0.5 s state-publish interval and
stores the text through `AgentRegistry`. This implements the input contract
for the Agent Controller's shared overlay; F1 overlay presentation belongs to
their A6 step. The
[debug tests](../../Assets/Scripts/AI/SecurityPatrol/Tests/SecurityPatrolDebugTests.cs)
exercise the real host-to-Blackboard publication.

The 13 table entries build their strings once during static initialization.
The brain changes its cached string only when the queued transition is applied;
queuing or rejecting a request leaves the current reason intact. Initialization,
player-memory resets, disable and death have explicit reset reasons. Death
clears the FSM to Patrol, so the diagnostic says Patrol with an agent-died
reason; health remains the authoritative death status. There is one last
reason, not a history. `CannotAttack` summarizes its combined LOS/range guard.

[SecurityPatrolGizmos](../../Assets/Editor/WorldBuilder/SecurityPatrolGizmos.cs)
draws when a patrol root is selected and Scene-view Gizmos are enabled. It
never initializes a brain, executes an intent, or saves content. Its assembly
is Editor-only; the runtime assembly still references only `Blackout.Shared`.

| Drawing | Meaning |
| --- | --- |
| Green sector (`#40ff80`) | Calm/Patrol, or a configured preview before initialization. |
| Amber sector (`#ffb030`) | Investigate or Search. |
| Red sector (`#ff3020`) | Pursue or Attack. |
| Grey sector (`#555555`) | Disabled, or an initialized agent with dead Health. |
| Cyan loop (`#47d1d6`) | Owned waypoints in ordinal Id order, including the closing link; each point has its marker Id label. |

The wire sector is a horizontal slice at the patrol's sensing eye, using its
horizontal body forward, full FOV and effective range. Base 22 m becomes
30.8 m when heightened; FOV stays about 111.73 degrees. It is an unoccluded
geometric bound: actual sight still uses a 3D range check and a blocking ray.
The eye-level drawing differs from the prototype's floor projection so its
origin agrees with the implemented ray.

In Play Mode, read-only diagnostics use the actual context Self, cached
sensor settings and FSM route, even if a config asset is edited live. Before
initialization, the tool previews configured vision and exact-owner Waypoint
markers in the same editor stage, allowing markers in additive scenes while
excluding other stages. Preview searches are cached; hierarchy, Undo/property
and Play Mode changes invalidate the cache. Code that edits markers without
those notifications must call `InvalidatePreviewCache()`.

Waypoint rings have visual radius 0.18 m and sit 0.1 m above the marker to
avoid surface overlap. They are not arrival tolerances. Straight links show
logical route order; the mover calculates its own navigable paths. These
diagnostics do not visualize NavMesh corners or wall-clipped visibility.

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
   expect 150 and 267 passes respectively, including 22 debug and seven gizmo cases.
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
6. Enable **Scene > Gizmos**, select each `Patrol-1`, `Patrol-2`, `Patrol-3`
   root, and view the sector from above. Check the ordered cyan labels/loop
   against the layout, including Patrol-3's repeated return point. Confirm
   green/amber/red state colors, grey after disable/death, and 22 -> 30.8 m
   heightening without FOV growth. Configured prefab previews need a matching
   agent name to find that agent's waypoint markers.
7. With the shared overlay available, check the patrol's state/reason after
   noise, sight, LOS loss and search timeout, allowing its 0.5 s publish cadence.
   The real publication path is covered by the EditMode debug test; the visual
   F1 panel itself is an Agent Controller integration check.

## Recorded verification

Unity 6000.3.25f1, 2026-10-21: latest combined EditMode run exited 0 with **417 passed,
0 failed, 0 skipped**, duration **1.7439349 s**. This covers the two named
assemblies; it is not a whole-project test result.

| Patrol fixture | Passed | Main coverage |
| --- | --- | --- |
| SecurityPatrolBrainTests | 12 | Initialization, explicit transitions, reset/death and intent isolation. |
| SecurityPatrolStateBehaviourTests | 13 | Owned routes, arrival/pause, investigate/search completion and automatic edges. |
| SecurityPatrolCombatTests | 19 | Pursuit/attack guards, sight priority, hysteresis, actual shooter delay/reset and perturbed hit. |
| SecurityPatrolSensingTests | 29 | Live sensing adapter, flags, shared executor integration, lifecycle memory and warmed ticks. |
| SecurityPatrolSetupTests | 5 | Saved variant/config/channels/executors/difficulty and actual scene spawner reference. |
| SecurityPatrolDebugTests | 22 | All 13 edges, applied/pending/rejected reasons, automatic guards, lifecycle resets, actual host publication and warmed reads. |
| SecurityPatrolGizmoTests | 7 | Cone agreement with perception, cached tuning/alert range, preview ownership/order/cache, runtime route, invalid origins/range and warmed reads. |
| PerceptionVisionTests | 17 | Cone/range boundaries, occlusion memory and absolute range multipliers. |
| PhysicsPerceptionRaycastTests | 3 | Real wall, trigger/layer exclusions and finite ray length. |
| PerceptionHearingTests | 16 | Threshold/radius/cap, strongest noise, sight priority and reset. |
| PerceptionNoiseListenerTests | 7 | Position, own-source filtering, unsubscribe and independent listeners. |

Patrol total: **150**. The **267 Agent Controller tests** include world-flag
eligibility, nonstacking configured alerts, Commander death, late initialization,
spawning and replacement-brain replay. Tests use fake perception/context inputs;
selected integrations also exercise real physics, noise channels and shooting.
The randomized-hit test fixes seed 17 and restores Unity's previous random state.
EditMode lifecycle tests invoke managed callbacks through reflection; they do
not prove Unity's live Play Mode callback ordering.

Allocation assertions pass after warm-up: 128 automatic patrol/combat/sensing
cycles and 128 alerted host think ticks allocate zero managed bytes on the
calling thread; earlier 1,024 vision and 1,024 channel cycles remain covered.
S4 also measures 128 warmed transition/debug-read cycles and 128 cached
gizmo-data reads at zero managed bytes. Initialization, route/preview cache
rebuilds, rendered Handles, physics internals and other executors are outside
those measurements.

Earlier S3 setup verification exited 0: the second prefab/config/config-meta output was
byte-identical. The existing prefab GUID, base/other agent prefabs, placeholder
material, Environment and Agents scenes remained unchanged (eight protected
files). Unity generated four new metadata files. Both setup and test logs had
zero C# compiler errors/warnings; the existing licensing access-token message
persisted without preventing success.

S4 batch runs generated three new metadata files in total (one debug-test
file, the gizmo tool and its test file). Both S4 runs had zero C# compiler
errors/warnings; the earlier debug run passed 410/410 in 1.6440833 s, before
the seven gizmo tests were added. The initial sandbox launch stalled on Unity
licensing before import; the approved retry completed. No scenes, prefabs or
config values were changed by S4.

Evidence is outside Git at
`%TEMP%/blackout-s4-gizmo-tests-20261021-010338.log` and matching `.xml`, and
`%TEMP%/blackout-s4-debug-tests-approved-20261021-005100.log` and matching `.xml`.
Earlier setup evidence remains `%TEMP%/blackout-s3-prefab-setup.log` and
`%TEMP%/blackout-s3-prefab-tests.log`/`.xml`. No integrated gameplay,
rendered Scene-view appearance or real Play Mode lifecycle validation is claimed.

## Five-line viva summary

1. Does: exposes patrol state/reasons and selected vision/waypoint gizmos alongside the five-state patrol response loop.
2. Design: an FSM makes five exclusive phases and 13 explicit edges readable; cached diagnostics preserve the intent/execution boundary.
3. Alternative: rejected a behaviour tree for this fixed loop because reusable nested tactics would not yet justify its status/reset rules.
4. Number: the reference 1.4 alert multiplier raises 22 m detection/engagement to 30.8 m without stacking or widening the cone.
5. Weakness: investigation has no unreachable-target timeout; more interacting tactics would grow the flat FSM's guard and edge count.
