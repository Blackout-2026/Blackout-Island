# Agent Controller — reference extracted from the web prototype

Source: `prototype/src/{agents,nav,ui/debug_overlay.js,models/humanoid.js,models/robot.js}` and `prototype/README.md`.
Step A0 of `docs/UNITY_BUILD_PLAYBOOK.md`. Numbers are copied from the code. Anything **[GUESS]** is
my interpretation or a Unity-side proposal, not a prototype value.

**Units.** The prototype uses three.js world units and treats **1 unit = 1 m** (player height ~1.8,
nav cell = 1 m). Angles are radians. Time is seconds. The prototype's Z axis is the level's long axis;
the bunker runs towards negative Z.

---

## 1. Architecture recap (what the Unity version must keep)

Update order per agent per frame: `perception (10 Hz) -> think (10 Hz) -> controller (every frame) -> combat (every frame)`.

- Think tick is 0.1 s, started with a random 0–0.1 s offset per agent so agents are staggered.
  When a tick fires late, perception and think receive `0.1 - thinkT` as their dt.
- Brains write only an **intent**: `{ move: Vector3|null, speed: walk|run|sneak, look: Vector3|null, aim: bool, fire: bool, crouch: bool }`.
  The prototype intent has `crouch` and a `sneak` speed that the playbook's `AgentIntent` (Stop/Walk/Run, aim, fire) does not.
  **[Flag for Unity]** `AgentIntent` needs a `crouch` flag and a `Sneak` speed, or the Hunter loses its ambush and sneak tactics. These are Agent Controller contracts, so I can add them in A1.
- The **AgentController** (shared) turns an intent into A* paths, steering, collision, doors, facing, and animation. Identical for all four agent types.
- Agents never call each other. They share knowledge through the **Blackboard** and through events.

---

## 2. Navigation

### 2.1 Nav grid (`nav/navgrid.js`)

| Item | Value |
| --- | --- |
| Grid origin / size | X0 = -62, Z0 = -146, W = 124, H = 252 cells, CELL = 1 m (covers forest, compound, ramp, bunker) |
| Per-cell data | `walk` (agent can stand), `height` (floor y), `nearWall` (next to obstacle), `sound` (sound-blocking wall), `lockBlock` (locked door), `doorSound` (closed door blocks sound) |
| Agent clearance used when building | 0.42 m radius margin (agent capsule itself is 0.38, guard 0.45) |
| Not walkable | below y = -0.45 (water), terrain normal.y < 0.72 (~44° slope), the hill behind the compound, no floor (solid rock, also blocks sound) |
| Step between cells | floor height difference <= 0.6 m (ramps pass, cliffs do not) |
| Runtime updates | `nav:changed` (crate pushed) re-evaluates a rectangle and bumps `Nav.version`; `door:lockchanged/opened/closed` update lock and sound-blocking cells |

**Unity mapping [GUESS]:** NavMeshSurface with agent radius 0.4, height 1.8, step 0.4, max slope 40 (already in W4). The crate is a `NavMeshObstacle` with carving. A `Nav.version` change becomes "obstacle moved, replan".

### 2.2 A* (`nav/astar.js`)

- 8-connected grid, octile heuristic, binary heap, typed arrays reused with generation stamps.
- Diagonals may not cut corners.
- Cell cost = move cost × **1.6 if the destination cell is `nearWall`** (paths keep off walls).
- **String-pull smoothing**: keep a waypoint only if the straight segment to the next candidate leaves walkable space (sampled with side offsets for agent radius). The exact target is used as the last point when walkable.
- **Request queue**: one pending request per agent (a new one replaces the old). Per frame: process at least 1, at most 4 requests, stopping once 3.5 ms is used.
- Unity equivalent: `NavMesh.CalculatePath` returns the corner array. The "budget" idea (max N path requests per frame) is still worth keeping.

### 2.3 Path management in the controller (`agents/movement.js`)

| Behaviour | Value |
| --- | --- |
| Short hop shortcut | goal < 6 m away, walkable, clear line: single-point path, no A* |
| Replan triggers | goal moved more than **1.6 m** (run) or **0.9 m** (walk/sneak); repath timer; `Nav.version` changed |
| Repath timer | 2.5 s + random 0–1 s |
| Waypoint advance | when within **0.55 m** of the current waypoint (can skip several per frame) |
| Arrived | on the last waypoint within **0.4 m**; or no path, `failed > 0` and within 0.8 m of the target |
| Arrival slow-down | on the last leg, speed × `min(1, d/1.4 + 0.15)` |
| Path failure | `failed++` on an empty path; reset to 0 on success. Brains treat `failed > 1` (or > 2 for the Hunter) as "give up" |

---

## 3. Movement

### 3.1 Speeds, acceleration, turn rate (m/s, rad/s)

| Agent | walk | run | sneak | turnRate | radius | Source |
| --- | --- | --- | --- | --- | --- | --- |
| Controller defaults | 1.5 | 4.0 | 1.0 | 5 | 0.38 | `movement.js` |
| Security Patrol | 1.55 | 4.1 | (default 1.0) | 5 | 0.38 | `security_patrol_fsm.js` |
| Hunter | 1.6 | 4.5 | 1.15 | 6 | 0.38 | `hunter_bt.js` |
| Tactical Guard (robot) | 1.4 | 3.5 | (default 1.0) | 4 | **0.45** | `tactical_guard_utility.js` |
| Commander | 1.3 | 3.4 | (default 1.0) | 5 | 0.38 | `commander_goap.js` |

- **Acceleration** `accel = 7` m/s² (all agents use the default). Deceleration is **1.6×** faster when the desired speed is below the current speed. Velocity change per frame is clamped to `accel * dt`, so there is no sliding or snapping.
- **Turn rate** is multiplied by **1.4** while `aim` is true.
- **Heightened** agents move at **×1.25** speed (see section 8).
- Facing: toward the `look` target if set, else toward the velocity when speed > 0.25 m/s, else keep yaw.
- **Head yaw** (used by vision): the head turns toward the look target up to ±0.9 rad relative to the body, damped at rate 6.
- Ground: y follows `groundAt` with damping 18 (smooth stairs and ramp).

### 3.2 Separation and stuck handling

| Item | Value |
| --- | --- |
| Agent–agent separation | for any living agent within **1.3 m** (and within 1.5 m vertically): push away with strength `(1.3 - d) * 2.4` added to the desired velocity |
| Agent–player separation | within **1.0 m**: push strength `(1.0 - d) * 3` |
| Player pushes back | `pushPlayer()`: the player cannot overlap a living agent (min distance = player radius + agent radius) |
| Wall collision | cylinder resolve (radius, height 1.8); velocity component into the wall is removed (slide) |
| Stuck detection | while it wants to move at > 0.4 m/s and has not arrived: every **1.4 s**, if it moved < **0.35 m**, then nudge velocity by random ±1.25 m/s on x and z, clear the path, set repath timer to 0, increment `stuckCount` |
| Stuck reset | when not trying to move, the timer and reference position reset |
| Doors | if moving > 0.3 m/s and a door is within **2.4 m**, unlocked and still closed (`target < 0.5`), and it lies ahead (dot product > 0), then `door.requestOpen(agentName)` |

---

## 4. Perception (`agents/perception.js`) — shared by all four AIs

### 4.1 Vision

| Item | Value |
| --- | --- |
| Eye position | agent pos + `eyeHeight` (default 1.62, Hunter 1.6, Guard 1.95) |
| Cone | full angle `fov` around **head yaw**; also always sees within **2.2 m** regardless of cone |
| Line of sight | ray eye → player eye (head); else eye → player chest (`pos.y + height * 0.55`). Walls, trunks, doors block; chain-link fences do not |
| Awareness meter | `+= 2.2 * max(0.15, 1.25 - dist/range) * visibilityFactor * (detected ? 3 : 1) * dt`, capped at 1.5 |
| Detected | awareness >= 1; sets `lastSeenPos`, `lastSeenTime` and writes a sighting to the Blackboard |
| Decay when unseen | `-0.35/s` (or `-0.25/s` once detected) |
| Lose detection | detected for no longer than 6 s after `lastSeenTime`, then `detected = false` |
| Visibility factor | crouch ×0.55, sprint ×1.25, flashlight ×1.6, outdoors without flashlight ×0.8 (multiplied together) |
| Range multiplier | ×1.4 when heightened |
| Player dead | awareness decays 1/s, `detected = false` |

### 4.2 Hearing (noise propagation)

Every `noise` event has `{pos, radius, type, source}`. For each living listener within `radius` (and not the source):

```
path  = 1 - pathDistance / radius                      (Dijkstra flood on nav grid; walls and closed doors block)
wall  = (1 - straightDistance / radius) * 0.35^walls   (only if path <= 0.05; walls counted by ray, max 3)
loudness = max(path, wall) * listener.hearing
```
Heard if `loudness > 0.04`; stored as `{pos, loudness, type, time, source}`; memory 8 s; brains query `recentNoise(types, maxAge)` which returns the **loudest** match in the window.

| Agent | hearing multiplier |
| --- | --- |
| Security Patrol | 1.0 |
| Hunter | **1.7** |
| Tactical Guard | 0.9 |
| Commander | 0.9 |

Noise sources and radii (m). Emitters belong to other roles but the Agent Controller consumes them:

| Event type | Radius | Emitter |
| --- | --- | --- |
| `footstep` (walking) | 4.5 | player (crouched = silent, 0) |
| `sprint` | 13 | player |
| `jump` | 7 | player |
| `land` (landing speed > 6) | 9 | player |
| `gunfire` SMG | 42 | player weapon |
| `gunfire` sniper | 65 | player weapon |
| `melee` | 3 | player weapon |
| `door` | 14 (bunker door 22) | doors; also when agents open them |
| `crate` | 11 | crate push |
| `generator` | 60 | generator interaction |
| `alarm` | 18 | cure pickup |
| `gunfire` by an agent | `weapon.noise`, default **40** | agent shooting |

---

## 5. Per-agent stats

| | Security Patrol ×3 | Hunter ×2 | Tactical Guard ×2 | Commander ×1 |
| --- | --- | --- | --- | --- |
| Architecture | FSM | Behaviour tree | Utility AI | GOAP |
| HP | 80 | 90 | 100 | 70 |
| Vision range / FOV (rad) | 22 / 1.95 | 25 / 2.0 | 22 / 2.1 | 18 / 2.0 |
| Hearing multiplier | 1.0 | 1.7 | 0.9 | 0.9 |
| Eye height | 1.62 | 1.6 | 1.95 | 1.62 |
| Weapon damage | 7 | 12 | 8 | 5 |
| Burst count / interval / pause (s) | 3 / 0.12 / 1.05 | 1 / 0.1 / 1.35 | 4 / 0.1 / 1.15 | 2 / 0.25 / 1.2 |
| Spread (rad) | 0.034 | 0.024 | 0.032 | 0.05 |
| Weapon range (m) | 34 | 45 | 34 | 22 |
| **Reaction delay (s)** | 0.6 | 0.7 | 0.5 | 0.8 |
| Tracer colour | default 0xffb060 | 0xffc070 | 0xff7040 | 0xffd090 |
| Hitbox | r 0.3 / head r 0.14 | same | **r 0.4 / head r 0.17** | same as humanoid |
| Body | humanoid (patrol uniform) | humanoid (ghillie camo) | robot, bipedal | humanoid (lab coat, headset) |

Defaults for any weapon field not given: damage 7, burst 3, interval 0.11, pause 0.9, spread 0.035, range 30, reaction 0.55, tracer on, noise 40.

Hitbox heights: body capsule from `y+0.3` to `y+1.35` (humanoid) or `y+1.65` (robot) minus `crouchBlend*0.4`; head sphere at `y+1.63` (humanoid) or `y+1.98` (robot), minus crouch.

### 5.1 Shooting (shared, `agent_base.js`)

Agent fires only if **all** hold: intent.fire, player alive, `canSee`; time engaged >= `reaction`; `aimBlend >= 0.8`; distance <= `weapon.range`; facing the player (forward · direction >= 0.85); shot timer <= 0.

- Engagement timer starts when the agent first sees the player with `fire` set; resets when line of sight is lost.
- **Burst logic:** a burst of `burst` shots spaced `interval`, then a pause `pause * (0.8 to 1.2)`.
- **Aim error** (the "aim error" parameter):

```
err = spread * (1 + dist/28) * (1 + playerHorizSpeed/4.5 * 0.9) * (engaged < 1.2 s ? 1.6 : 1) * (player crouched ? 1.15 : 1)
if heightened: err *= 0.9
```
  The shot direction is perturbed by a random angle with radius `err * sqrt(-2 ln u) * 0.7` (Rayleigh-style). Target point is player chest (`pos.y + height * 0.62`).
- Ray starts 0.15 m in front of the muzzle (avoids the shooter's own wall corner); max distance `range + 10`. Player hit: `player.takeDamage(damage, agentPos)`.
- Near-miss: `bullet:whiz` event when the ray end is < 6 m from the player's eye.
- Every shot emits a `gunfire` noise at the **agent's position** with radius `weapon.noise` (40), plus tracer, muzzle flash, light flash.
- Muzzle position: the rig's `muzzle` object (hand bone, 0.33 m below it).

### 5.2 Damage, death

- `takeDamage(amount, fromPos)`: HP down, `hurtTime` = now, hit-flash 0.15 s (red emissive), hit-react impulse `rig.hit = 1` (decays at 4/s humanoid, 3.5/s robot), the attacker position becomes `lastSeenPos` (awareness set to 1, detected) and is written to the Blackboard.
- `die()`: alive = false, Blackboard `casualties++` and `casualtiesSinceRegroup++`, its directive is cleared, `enemy:killed` event; guards spawn a 30-spark burst at chest height.
- **Death timeline:** collapse animation plays (see section 9), at **3.2 s** after death the material goes transparent and fades over **1.6 s**, then the rig is hidden.

---

## 6. The four AIs

All think at 10 Hz. Positions are in prototype coordinates; the W-owned layout doc supersedes them for Unity.

### 6.1 Security Patrol — Finite State Machine (`security_patrol_fsm.js`)

`ATTACK_RANGE = 22` m (× range multiplier 1.4 when heightened). "Detected" = awareness >= 1.

| State | Intent on enter | Transitions out (in priority order) |
| --- | --- | --- |
| **Patrol** | walk, no aim/fire | detected → **Attack** (if in attack range with LOS) else **Pursue** · new directive → (see below) · canSee and awareness > 0.35 → **Investigate** at the player's position ("glimpse") · noise loudness > 0.1 within the last 1.5 s and newer than the last handled → **Investigate** (urgent + run if gunfire) · Blackboard `alert` → **Search** at last known position, radius 10 |
| **Investigate** | walk (run + aim if urgent) towards target | detected → Attack / Pursue · directive · a new gunfire noise (last 1 s) retargets and sets run · on arrival (< 1.5 m, or arrived, or `failed > 1`): stand and look around (sinusoid, radius 5), after **3.5 s** → **Search** if alert else **Patrol** |
| **Pursue** | run, to last seen / directive position | while it sees and detects the player: target = player position; in attack range → **Attack** · else target = `lastSeenPos` · `converge` directive overrides target if not seen within 2 s · `reached` and not seeing, or (lost > 2.5 s and in state > 4 s) → **Search** (radius 9) |
| **Attack** | walk, aim, fire, no move | `fire = canSee` · no LOS for **1.0 s** → **Pursue** to last seen · distance > `22 * rangeMul * 1.15` → **Pursue** · strafes: every 1.6 + rand(0–1.4) s pick a point 1–2.5 m to the side (only if walkable); clear when within 0.4 m |
| **Search** | walk, aim; random walkable points within `radius` of the centre | detected and seeing → Attack / Pursue · directive · at each point (< 1.2 m, arrived or failed) look around for 1.6 s then pick the next point · duration **14 s**, or **30 s** if alert · on timeout: clear directive, → **Search** again if alert (around last known position, radius 12) else **Patrol** |

**Patrol route:** walk waypoints in order and loop; pause **2.2 s** at each waypoint while the head sweeps (`sin(t*0.7 + idx)*4`, `cos(...)*4`, look height +1.5); waypoint reached within 0.9 m. Routes:
- Patrol-1 (compound / security area): (-14,-36) (14,-36) (16,-46) (10,-55) (-10,-55) (-16,-46)
- Patrol-2 (security office): (3,-74) (-4,-74) (-4,-79.5) (3,-79.5)
- Patrol-3 (corridor): (0,-88) (0,-102) (-0.5,-120) (0,-102) (ping-pong)

**Directives as transitions** (only a directive newer than the last handled counts):
`converge` → Pursue(pos) · `search` → Search(pos, radius or 10) · `regroup` → Investigate(pos).

Attack-range check: `canSee && distToPlayer < 22 * rangeMul`.
Debug text: `"<name> [FSM] <state>"` and the last transition reason. Last 4 transitions are kept.

### 6.2 Hunter — Behaviour Tree (`hunter_bt.js`)

Reactive root Selector, re-evaluated every tick. A running Sequence re-checks its Condition children before the running child each tick (aborts when the premise fails). A Selector resets the previously running child when a different child takes over. Node types: Selector, Sequence, Condition, Action, Wait. Status: SUCCESS / FAILURE / RUNNING.

```
Root ?
├─ Engage            [SeesPlayer: detected && seenRecently(0.6)]
│    └─ EngageMode ?  ( Shoot: [InRange: dist < 32*rangeMul && canSee] AimAndFire   |   CloseIn )
├─ HuntLastSeen      [SawRecently: seenRecently(10) && !canSee && lastSeenPos] StalkToLastSeen
├─ ReactToGunfire    [HeardGunfire: recentNoise(gunfire|generator|alarm, 6 s) loudness > 0.06]
│    └─ GunfireTactic ? ( CloseFire: [GunfireClose: noise < 16 m] TakeCover   |   FlankGunfire )
├─ ReactToFootsteps  [HeardFootsteps: recentNoise(footstep|sprint|jump|land|door|crate|melee, 5 s) loudness > 0.04]
│    SneakToNoise → Ambush
├─ FollowOrders      [HasDirective] MoveToDirective
└─ Prowl             PickProwlPoint → WalkTo → LookAround
```

| Leaf | Behaviour |
| --- | --- |
| AimAndFire | stand still, aim, fire, look at player chest; crouch if distance > 14 m; RUNNING while canSee, else FAILURE |
| CloseIn | run towards player (arrive within 6 m), aim, no fire |
| StalkToLastSeen | run to `lastSeenPos` (arrive 1.8 m), aim; on success forget the sighting |
| TakeCover | nearest tree trunk within 12 m, the cover point is 0.9 m behind the trunk relative to the threat; run there, crouch, aim at the noise, hold **2.5 s** |
| FlankGunfire | phase 0: run to a flank point; phase 1: walk (4 m arrive) towards the noise, looking at it |
| SneakToNoise | crouch + sneak speed to the noise (arrive 3.0 m); retargets on newer footsteps |
| Ambush | stand, crouch, aim, look at the noise for **4 s**, then clear the noise list |
| MoveToDirective | run (walk for regroup) to the directive position (search: random point within radius, keeps searching; converge: clears the directive on arrival; arrive 2.5 m) |
| PickProwlPoint | next of 6 prowl points (advance 1 or 2 at random) |
| WalkTo / LookAround | walk (arrive 2 m); look-around 2–4 s with head sweep |
| moveTo helper | SUCCESS within `arriveDist` (default 1.4) or controller arrived; FAILURE if `failed > 2` or after **25 s** |

**Flank point:** 12 m to the side of the noise (random side, tries both), plus 4 m back toward the hunter, snapped to a random walkable point within 3 m. Fallback: random point within 6 m of itself.

**Prowl points:**
- Hunter-1 spawn (-24, 54): (-24,54) (-30,30) (-18,12) (-26,-8) (-14,70) (-36,44)
- Hunter-2 spawn (26, 20): (26,20) (30,44) (20,64) (32,2) (18,-14) (36,30)

**Camouflage:** ghillie model only. The prototype has **no** separate camouflage visibility multiplier on the Hunter; the player's own crouch/sprint factors handle visibility. **[Flag]** the playbook's H3 invents a camouflage multiplier on the player's detection (Systems Engineer). Not in the prototype.

Debug text: `"<name> [BT] <state>"` and the active branch path, e.g. `ReactToGunfire > FlankGunfire`.

### 6.3 Tactical Guard — Utility AI (`tactical_guard_utility.js`)

Scores 4 actions each tick (10 Hz); highest wins; `+inertia` added to the current action's score.

**Considerations (all 0..1):**

| Name | Definition |
| --- | --- |
| dist `d` | metres to the player |
| visible | 1 if detected and canSee |
| knows | 1 if `seenRecently(10)` or Blackboard sighting age < 10 s |
| hurt | 1 if hit within 3 s |
| aimedAt | 1 if visible and the player's camera forward · direction to the guard > 0.97 |
| threat | `clamp01(visible*0.4 + hurt*0.5 + aimedAt*0.25)` |
| health | `hp / maxHp` |
| cover `q` | only evaluated when visible or hurt: among `coverPoints` within **14 m**, those not visible to the player's eye; `q = 1 - d/14`, best one wins |
| post `postD` | `clamp01(distance to post / 12)` |
| inCover | 1 if within 1.0 m of its chosen cover point |
| rangeCurve | `1 - smoothstep(18, 36, d)` |

**Scores:**

```
Attack      = visible ? (base + range*rangeCurve) * (1 - healthKeep + healthKeep*health) * (1 - underFire*threat*(1-health)) : 0
MoveToCover = sqrt(q) * clamp01(0.2 + threat*cover.threat) * (1.6 - health*cover.lowHealth) * (inCover ? 1 - inCoverPenalty : 1)
Reposition  = (knows && !visible) ? knows + nearPost*(1 - postD) : (visible && d > 30) ? longRange : 0
HoldPost    = base + farFromPost*postD - (knows ? knowsPenalty : 0)
```

**WEIGHTS table (data, not code):**

| Group | Values |
| --- | --- |
| attack | base 0.35, range 0.65, healthKeep 0.4, underFire 0.6 |
| cover | threat 1.0, lowHealth 1.0, inCoverPenalty 0.7 |
| reposition | knows 0.45, nearPost 0.3, longRange 0.4 |
| hold | base 0.25, farFromPost 0.4, knowsPenalty 0.15 |
| inertia | 0.1 |

**Action execution:**
- **Attack:** stop, aim, fire, look at the player's chest, stand.
- **MoveToCover:** run to the chosen cover point, aim, fire if canSee, crouch when at the cover (within 1.0 m).
- **Reposition:** run to a point near the last known position but within **15 m** of the post (30 m when alert): `post + (lkp - post)` clamped to that length, snapped to a random walkable point within 3 m; new point when within 1.2 m or `failed > 1`.
- **HoldPost:** walk to the post (arrive 1.2 m); at the post the head scans (sin/cos of `scanT*0.5`, radius 6, height +1.6).
- **Directive:** moves the guarded `post` to the directive position (once per directive); `converge` etc. all treated the same.
- Eyes: red (6, 0.4, 0.2) in combat or alert, else amber (5, 2.4, 0.2).
- Cover is **not claimed** between guards in the prototype; two guards can pick the same point. **[Flag]** the playbook's T2 asks for claiming. That is a Unity addition (Core Developer).

**Posts / spawns:** TG-01 (-8, -100), TG-02 (2.5/3, -129).

**Cover points (19):** (-14.8,-101.2) (-7.2,-101.2) (-14.8,-110.8) (-7.2,-110.8) (-17.5,-99) (-4.5,-113.5) (-18,-110) (-11,-109.4) (-15.5,-108.6) (-16.8,-106) (-9,-97.5) (-13,-97.5) (-6.5,-130.5) (6.5,-130.5) (-6.5,-137) (6.5,-137) (-1.2,-92.5) (1.2,-110) (-1.2,-117).

Debug text: `"<name> [Utility] <action>"` plus `A/C/R/H` scores; the F1 overlay also shows bars and the input factors.

### 6.4 Commander — GOAP + blackboard (`commander_goap.js`)

**World state** (booleans, read from the Blackboard each tick):

| Key | Definition |
| --- | --- |
| `cureStolen` | Blackboard `cureStolen` (set by the `cure:taken` event) |
| `alertActive` | Blackboard `alert` |
| `playerLocated` | `playerLKP` exists and sighting age < **6 s** |
| `squadReady` | >= 2 other living agents **and** `casualtiesSinceRegroup < 2` |
| `playerNeutralized` | player is dead |

**Goals:** `PlayerLocated = {playerLocated: true}` (chosen while the player's position is unknown); `PlayerNeutralized = {playerNeutralized: true}` (chosen once located, or when the player is dead).

**Actions:**

| Action | Preconditions | Effects | Cost |
| --- | --- | --- | --- |
| BroadcastAlert | cureStolen, !alertActive | alertActive | 1 |
| OrderRegroup | alertActive, !squadReady | squadReady | 2 |
| OrderSearchZone | alertActive, !playerLocated | playerLocated | 3 |
| OrderConverge | alertActive, playerLocated, squadReady | playerNeutralized | 2 |

**Planner:** A* over boolean world states. Heuristic = number of unmet goal keys. Node = `{state, g, f, plan}`; open list re-sorted by `f` each iteration; closed set keyed by the sorted state string; plan length capped at **5**, iteration cap **200**; returns an action-name array or null.

**Replan when:** the goal changed, the plan is exhausted, or the current action's preconditions no longer hold. An action returning `failed` clears the plan.

**Action execution:**
- **BroadcastAlert:** waits 0.6 s (keying the headset), then sets Blackboard `alert`, `alertReason = "cure stolen"`, `alertTime`; makes a sighting if none exists (player position); issues `converge` to every other living agent at the last known position; emits `alert:broadcast`.
- **OrderConverge:** requires playerLocated else `failed`; re-issues `converge` to all units every **1.0 s** at the last known position; done when the player is neutralized.
- **OrderSearchZone:** done when playerLocated. At start and every **8 s**, send each unit a `search` directive on a ring of radius **7 m** around the last known position (angles evenly spaced), search radius 9. Fails after **24 s**.
- **OrderRegroup:** rally = midpoint between last known position and the Commander. Issues `regroup` once; done when >= 2 units are within **10 m** of the rally or after **12 s**; resets `casualtiesSinceRegroup` to 0.
- When there is nothing to do (no cure stolen, no alert) the state is `Monitor` (goal "none (monitoring)").
- **Last known position fallback:** the cure chamber position, if no sighting exists.
- **Own body** (runs every tick, independent of the plan): sees the player → aim and fire, look at chest, retreat to (-6, -140.5) at run speed if hurt in the last 4 s; otherwise hold the console post (6, -138) at walk speed and look toward (6.4, -142).
- **Death:** no more directives (nothing runs). Existing directives remain on the Blackboard. In the prototype the alert is then raised by an **automated lab system** 1.5 s after `cure:taken` if the Commander is dead and no alert is active (all units get `converge` to the player position, `by: 'system'`).

Spawn: Commander at (6, -138), facing yaw π.

Debug text: `"Commander [GOAP] goal: <goal>"` and the plan with the current step in `[ ]`. The overlay also keeps a log of the last 4 plans (`<time>s <goal>: A → B`).

**Playbook mismatch [FLAG]:** G2 lists world-state keys `playerLocated, cureTaken, generatorDisabled, agentsAlive, squadAssembled, playerNeutralized`; the prototype uses `cureStolen, alertActive, playerLocated, squadReady, playerNeutralized`. I suggest keeping the prototype's keys; they are what the cost values were tuned for. The playbook's own examples are guidance, not a contract.

---

## 7. Blackboard (`agents/blackboard.js`)

**Data fields:**

| Field | Type | Meaning |
| --- | --- | --- |
| `alert` | bool | alarm active |
| `alertReason` | string | `"cure stolen"` or `"lab sensors"` |
| `alertTime` | float | time the alert started (-1 = none) |
| `cureStolen` | bool | set on `cure:taken` |
| `generatorDown` | bool | set on `generator:disabled` |
| `playerLKP` | Vector3 / null | last known player position |
| `playerLastSeen` | float | time of the last sighting (-1e9 = never) |
| `lastSeenBy` | string | agent name, with `" (heard)"` if from noise |
| `casualties` | int | agents killed |
| `casualtiesSinceRegroup` | int | killed since the last Commander regroup |
| `rally` | Vector3 / null | rally point |

**Directives:** one per agent name: `{ type: converge|search|regroup, pos, radius?, issuedAt, by }`. Issuing sets `issuedAt = now` and appends to a 20-entry history. Brains compare `issuedAt` with the last one they handled so a directive acts once. `clearDirective(name)` deletes it (also on death).

**Methods:** `reportSighting(agent, pos)` (copies position, updates time and observer), `reportNoise(agent, pos)` (only fills a gap, i.e. when the last sighting is more than 4 s old, and stamps it 2 s old), `sightingAge()`, `issue`, `directiveFor`, `clearDirective`, `dump()` for the overlay.

**Mapping to playbook contracts:** `Directive{Converge, SearchZone, Regroup, position, radius}` matches `type, pos, radius`. The `Blackboard` contract's `registered agents and their state strings` is new (the prototype reads `BI.Agents.list`).

---

## 8. Alert buff (heightened state)

`agent.heightened = true` gives:

| Effect | Multiplier |
| --- | --- |
| Movement speed (all speeds) | **×1.25** |
| Vision range (`rangeMul`) | **×1.4** (also the Patrol's attack range, Hunter's engage range, 22 → 30.8 m) |
| Shot aim error | **×0.9** |
| Guard reposition limit | 15 m → 30 m (when Blackboard `alert`) |
| Search duration | 14 s → 30 s (when Blackboard `alert`) |

| Trigger | Who is heightened |
| --- | --- |
| `generator:disabled` | Security Patrols and Tactical Guards only; Blackboard `generatorDown = true` |
| `alert:broadcast` | every living agent (Hunters and Commander included) |
| `player:respawned` | no change, but all agents `forget()` the player (awareness 0, noise list cleared, engage timer reset) and `playerLastSeen` resets to never |

The spec's "+25% speed, +40% vision" matches the README.

---

## 9. Animation

### 9.1 Drivers

The controller calls `rig.pose(state, dt)` every frame with:
`{ speed, dist (distance moved this frame), aim (0..1), aimPitch, crouch (0..1), lookYaw, sneak }`.
- `aimBlend` damps toward `intent.aim` at rate **7**; `crouchBlend` toward `intent.crouch` at rate **5**; `lookYaw` at rate 6 (clamped ±0.9 humanoid, ±1.0 robot).
- `aimPitch = atan2(look.y - (pos.y + 1.4), max(0.5, horizontalDistanceToLook))`.
- **Gait phase** advances by `dist / strideLength * π`, so feet match ground speed. Stride length: humanoid 0.95 (walk) / 1.35 (speed > 3); robot 1.1 / 1.5.
- Blend weights: humanoid `walk = clamp01(speed/1.6)`, `run = clamp01((speed-2.2)/2.5)`; robot `walk = clamp01(speed/1.5)`, `run = clamp01((speed-2.3)/2.2)`.

### 9.2 Animation states (humanoid)

| State | How it is produced |
| --- | --- |
| Idle | all weights 0, relaxed arms |
| Walk / Run | thigh swing `0.42*walk + 0.38*run` rad, knee `0.55*walk + 0.75*run`, hip bob `0.025*walk + 0.05*run` m, arm counter-swing `0.35*walk + 0.4*run`, spine lean `0.12*run` |
| Sneak | lean +0.2 |
| Crouch | hips down 0.28 m, thigh -0.55, knee +1.0, lean +0.18 (all × crouch blend) |
| Aim | arms lerp to aim pose (upperArmL x -1.25, upperArmR x -1.05), forearms to -0.55 / -0.75, gun raised, chest and neck follow `aimPitch` |
| Hit-react | spine pitched by `0.45 * rig.hit` (and roll toward the hit side), `hit` decays 4/s |
| Death | see below |
| Commander variant | different arm aim pose (sidearm held low in the left hand: upperArmL x -0.2, upperArmR x -1.45) and a different gun position |

**Humanoid death (time since death `t`):** `t1 = clamp01(t/0.35)` knees buckle (thigh -1.2/-1.0, shin 1.9/1.7, hips down 0.5·t1); `t2 = smoothstep(0.25, 1.0, t)` falls backward (hips pitch -1.35, hips down a further 0.32, arms thrown up, head back). Then fade-out starts at 3.2 s (1.6 s long).

**Robot death:** `t1 = clamp01(t/0.4)`, `t2 = smoothstep(0.3, 1.1, t)`; thighs/shins buckle, pelvis down 0.45 + 0.35, pelvis pitch **+1.2** (falls forward), head +0.5; the eye dot turns dark red.

**Robot specifics:** leg swing `0.32*walk + 0.22*run`, shin lift `0.45*(walk+run)`, pelvis stomp `0.03*walk + 0.05*run`, no crouch. Scanner dot sweeps across the visor at 2.5 rad/s (idle) or 9 rad/s (aiming). Eye colour set by the brain.

### 9.3 Bones and joints (rig names the Unity version needs)

- **Humanoid:** `hips, spine, chest, neck, head, upperArmL/R, foreArmL/R, handL/R, thighL/R, shinL/R, footL/R, gun`. Hips rest height 0.98 m.
- **Robot:** `pelvis (rest y 1.05), spine, chest, head, upperArmL/R, foreArmL/R, handL/R, thighL/R, shinL/R, footL/R`; muzzle empty on `handR` (0.33 m below). Robot has no neck and no gun bone.
- The playbook's A3 says the joint parts and pivots are described by the Core Developer in `docs/art-pipeline.md` (C3). The names above are what the prototype uses, so proposing the same names reduces rework. **[GUESS]**

---

## 10. Debug overlay (`ui/debug_overlay.js`)

Toggle F1 or backtick. Shows:
- **In world:** per-agent vision cone on the floor (colour: green calm, amber suspicious, red combat, grey dead), current path, a floating label `name [arch] state`, and expanding rings for each noise event (radius = propagation radius).
- **State colours:** combat if `inCombat()` or state matches `Attack|Pursue|Engage|Converge`; suspicious if `Investigate|Search|Gunfire|Footsteps|Reposition|Cover|HuntLastSeen|Alert|Order`; else calm.
- **Panel:** FPS, frame ms, draw calls, triangles; the Blackboard dump; the Commander's goal, plan and plan log; the guard utility scores with bars and factors; each agent's state and BT branch; event log.
- `inCombat()` = detected, or seen within 3 s, or hurt within 4 s.

---

## 11. Agent spawn table (from `world/level.js`)

| Agent | Count | Spawn(s) (x, z) |
| --- | --- | --- |
| Hunter | 2 | (-24, 54), (26, 20) — forest |
| Security Patrol | 3 | (-14,-36) compound, (3,-74) security office, (0,-88) corridor |
| Tactical Guard | 2 | (-8,-100) and (3,-129) — generator room, lab |
| Commander | 1 | (6,-138) — lab console |

Agents spawn on the ground at that position (terrain height for Hunters; physics ground for the rest). `BUNKER_Y` is the bunker floor height defined in `level.js`; it is for the World Builder's layout doc, not duplicated here.

---

## 12. Items the prototype fakes or Unity must solve differently

1. **Grid A* and the sound flood** → NavMesh paths. For the sound flood, the playbook's H2 uses straight-line distance with a per-wall penalty, which is a simplification of the prototype's flood. Either is defensible, but hearing around corners will differ. **[Decision for Hunter owner]**
2. **Capsule collision (`resolveCylinder`) and ground snapping** → NavMeshAgent or a CharacterController-style mover on the NavMesh (A2 says custom `PathFollower` with `SamplePosition` clamp).
3. **Procedural bone animation for humanoids** → the playbook plans an Animator with Speed/Aiming/Hit/Dead from free clips (A3). The prototype's blend weights above give the numbers for the blend tree thresholds (walk at ~1.5–1.6 m/s, run from ~2.2 m/s).
4. **Hitscan ray and aim error** can be ported directly (`Physics.Raycast`).
5. **Tree cover for the Hunter** uses the prototype's tree list; Unity needs `Terrain.terrainData.treeInstances` or Marker-based cover.
6. **Noise events** carry `radius`; the playbook's `NoiseEvent` has `loudness` + type instead. Mapping radius ↔ loudness needs agreeing with the Systems Engineer. **[FLAG]** The prototype's values for radius per event are in section 4.2.
7. **`AgentIntent` lacks `crouch` and `Sneak`**, see section 1.

## 13. Guessed or unverified items

- Unity mappings (NavMesh values, bone names for the Unity models, blend-tree thresholds) are proposals, not prototype values.
- I did not read `physics/*`, `weapons/*`, `player/*` beyond the grep lines for noise radii and the player's visibility fields (`crouched`, `sprinting`, `flashOn`, `zone`).
- The patrol routes and guard posts are copied as written; the real world layout comes from the World Builder (`docs/level-layout.md`) and may shift.
- `Level.BUNKER_Y` and the cure chamber coordinates were not extracted, since they belong to the World Builder reference.
- I did not run the prototype (no browser); every value is read from source.
