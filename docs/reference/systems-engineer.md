# Systems Engineer — reference extracted from the web prototype

Source: `prototype/src/player/player.js`, `prototype/src/core/input.js`, `prototype/src/core/engine.js`,
`prototype/src/core/namespace.js` and the `noise` emitters across `prototype/src`. Written at the start of
step Y2 because step Y0 had not been done yet. Sections 1–6 cover the **player** (Y2), section 7 the
**weapons** (Y3, from `weapons/weapon_system.js`, `models/weapons.js` and `world/effects.js`), sections
8–9 **death, respawn and the HUD** (Y4, from `player/player.js`, `game/mission.js`, `ui/hud.js` and
`ui/style.css`), and section 10 the **interactables** (Y5, from `physics/doors.js`, `physics/interactables.js`
and `physics/crate.js`), section 11 the **mission flow** (Y6, from `game/mission.js` and `ui/menus.js`), and
section 12 **physics hygiene** (Y7, from `physics/collision.js` and `weapons/weapon_system.js`).

Numbers are copied from the code. Anything marked **[GUESS]** is my interpretation or a Unity-side
choice, not a prototype value.

**Units.** 1 three.js unit = 1 m, angles in radians, time in seconds. The prototype's world Z is
mirrored in Unity (prototype start z = +120, Unity PlayerStart z = −121, see `docs/level-layout.md`).
Player code never uses world coordinates, so this does not matter here.

---

## 1. Movement (`player/player.js`)

| Item | Value | Notes |
| --- | --- | --- |
| Walk speed | 4.3 m/s | `WALK` |
| Sprint speed | 7.0 m/s | `SPRINT`. Only while Shift is held, moving forward (`f > 0`), on the ground and not crouched |
| Crouch speed | 2.1 m/s | `CROUCH`. Out of scope for Y2 (crouch is a "Could" in the playbook) |
| Ground acceleration | 38 m/s² with input, 30 m/s² without (friction) | velocity moves towards the target by at most `accel * dt` |
| Air acceleration | 7 m/s² | same rule as on the ground, so there is little air control |
| Gravity | 17 m/s² | higher than 9.81 so jumps feel snappy |
| Jump velocity | 5.6 m/s | apex = 5.6² / (2·17) ≈ 0.92 m. Space while crouched stands up instead |
| Standing height | 1.75 m | crouch height 1.15 m (not used in Y2) |
| Capsule radius | 0.35 m | |
| Eye height | height − 0.12 = 1.63 m | |
| Step height | 0.45 m | the header says "steps up to 0.45 m" |
| Ground snap | 0.35 m | snaps down onto stairs and ramps only if the player was grounded and is not rising |
| Max climbable slope | rise / run > 1.15 is blocked → atan(1.15) ≈ **49°** | terrain only in the prototype |
| Kill height | y < −20 → die | |
| Sub-steps | travel / 0.2 m, 1 to 6 steps | not needed: CharacterController sweeps |

## 2. Look and cursor (`player/player.js`, `core/input.js`, `core/engine.js`)

| Item | Value | Notes |
| --- | --- | --- |
| Mouse sensitivity | 0.0021 rad per pixel × setting (default 1.0, menu range 0.2–3) | = **0.12° per pixel** |
| Pitch clamp | ±1.52 rad = **±87°** | |
| Camera FOV | 72° vertical, near 0.08, far 700 | the viewmodel camera is 58° (Y3) |
| Mouse spike filter | ignore any single delta > 400 px | browser pointer-lock workaround, not needed in Unity |
| Cursor | pointer lock on click, Esc releases it and opens the pause menu | |
| Strafe roll | −clamp(sideways velocity / 7.0, −1, 1) × 0.012 rad (≈ 0.7°), damped at rate 6 | |

## 3. Camera feel

`U.damp(a, b, λ, dt) = a + (b − a)(1 − e^(−λ·dt))` (frame-rate independent smoothing) is used everywhere below.

| Effect | Formula / values |
| --- | --- |
| Head bob phase | `bob += (moved / stride) · π` every grounded frame with horizontal speed > 0.6 m/s, so one footstep is half a sine period |
| Head bob amplitude | target = clamp(hSpeed / 4.3, 0, 1.6) when grounded, else 0. Damped at rate 8 |
| Bob offsets | y = \|sin(bob)\| · 0.05 · amp − 0.025 · amp, x (sideways) = cos(bob) · 0.025 · amp |
| Landing trigger | landed with downward speed > 2.5 m/s |
| Landing kick | `landVel -= min(2.8, landedSpeed · 0.32)` |
| Landing spring | `landVel += (−landOffset · 120 − landVel · 14) · dt`, `landOffset += landVel · dt` |
| Landing applied to camera | y += landOffset · 0.12 m, pitch += landOffset · 0.02 rad |
| Step smoothing | camera y follows the body y, damped at rate 18, snaps if more than 1 m behind |
| Eye height smoothing | damped at rate 16 (only matters for crouch) |
| Damage shake | `shake += amount · 0.03` (max 1), decays 2.5/s, ±shake² · 0.04 rad jitter as sin(47 t) / cos(39 t) (rad/s, about 7.5 / 6.2 Hz). Y4 |

## 4. Footsteps and noise

| Item | Value |
| --- | --- |
| Footstep needs | grounded and horizontal speed > 0.6 m/s |
| Stride (distance per step) | walk 0.78 m, sprint 1.05 m, crouch 0.62 m |
| Footstep noise radius | walk 4.5 m, sprint 13 m, crouch 0 (silent) |
| Jump noise | radius 7, type `jump` |
| Landing noise | radius 9 if landed faster than 6 m/s, type `land` |
| Fall damage | (landedSpeed − 13) · 8 if landed faster than 13 m/s (Y4, needs Health on the player) |

**Unity mapping: noise scaled by speed [GUESS].** The playbook asks for footstep noise "scaled by speed".
The prototype uses a fixed value per state. The Unity version uses a piecewise-linear curve through the
prototype's points: (0 m/s, 0 m), (4.3, 4.5), (7.0, 13), clamped above 7. Walking slowly is quieter,
and the walk and sprint values match the prototype exactly. `NoiseType` has no Jump or Land value, so
those are published as `Footstep` with radius 7 and 9.

### Noise radii from the other systems (for the Hunter, H2)

| Source | Radius (m) | Prototype file |
| --- | --- | --- |
| Player weapon / melee | `d.noise` per weapon (Y3 adds the values) | `weapons/weapon_system.js` |
| Agent gunfire | `w.noise` per agent weapon | `agents/agent_base.js` |
| Bunker door / other doors | 22 / 14 | `physics/doors.js` |
| Generator disabled | 60 | `physics/interactables.js` |
| Cure alarm | 18 (type `alarm`) | `physics/interactables.js` |
| Crate pushed | 11 (type `crate`) | `physics/crate.js` |

## 5. Health (player side)

100 HP, no regeneration, 1.5 s invulnerability after respawn, respawn with full health at the current
phase's checkpoint, generator and cure state kept. `Health` (Y1) already ports these numbers, and Y4
wires them to the player.

## 6. Not ported in Y2

Crouch, flashlight (F), the pause menu and sensitivity slider, fall damage, damage shake, and per-surface
footstep sounds. The motor raises C# events (`Landed`, `Footstep`) so audio (and Y4's fall damage) can be
added later without changing it.

---

## 7. Weapons (`weapons/weapon_system.js`)

One data table drives all three weapons; behaviour differs only by data, which maps directly onto one
`WeaponData` ScriptableObject per weapon. Angles below are converted to degrees for the Inspector
(the prototype uses radians; spread is the offset at 1 m, so `tan(angle)`, which equals the angle in
radians at these sizes).

| Field | MP-9 SMG (slot 1) | R-700 Rifle (slot 2) | Combat Knife (slot 3) |
| --- | --- | --- | --- |
| Type | hitscan | hitscan | melee |
| Damage | 15 | 60 | 40 |
| Fire rate | 720 rpm → **0.083 s** cooldown | 52 rpm → **1.154 s** | 125 rpm → **0.48 s** |
| Trigger | automatic (hold) | semi (one shot per click) | automatic (hold keeps swinging) |
| Range | 90 m | 320 m | 2.3 m (+0.4 m reach tolerance) |
| Base spread | 0.024 rad = **1.38°** | 0 | – |
| Extra spread when moving | 0.02 rad = 1.15° × (speed / 4.3, max 1) | 0 | – |
| Bloom per shot / max | 0.004 rad = 0.23° / 0.022 rad = 1.26° | 0 | – |
| Damage falloff | full to 10 m, smoothstep down to ×0.45 at 32 m | none (starts at 999 m) | – |
| Headshot multiplier | 1.6 | 2.5 | – |
| Recoil view kick (pitch, yaw ±) | 0.0085 rad = 0.49°, 0.005 rad = 0.29° | 0.055 rad = 3.15°, 0.01 rad = 0.57° | 0 |
| Viewmodel kick (back, rotate) | 0.035 m, 0.04 rad = 2.3° | 0.11 m, 0.16 rad = 9.2° | 0 |
| Noise radius | 42 m | 65 m | 3 m |
| Tracer | every 2nd shot | every shot | – |
| Muzzle flash scale | 0.75 | 1.2 | – |
| Melee cone | – | – | cos 0.62 → **51.7°** half-angle |
| Backstab multiplier | – | – | 2.5 (behind and not in combat) |
| Other | – | zoom to 22° FOV, sensitivity ×0.35, 0.95 s bolt animation | swing 0.34 s, the hit lands **0.11 s** after the swing starts |

### Rules shared by all weapons

| Item | Value / formula |
| --- | --- |
| Cooldown | `cooldown = 60 / rpm` when fired, counts down by dt, can fire again at ≤ 0. One cooldown for the whole system (switching does not reset it) |
| Firing blocked | while switching, while dead, and guns (not the knife) while sprinting |
| Damage falloff | `damage × lerp(1, falloffMin, smoothstep(falloffStart, falloffEnd, distance))` |
| Spread | `spread + moveSpread × clamp01(speed / 4.3) + bloom + 0.02 rad if airborne` (crouch −35 %, not ported) |
| Spread direction | random point in a disc: angle `a` uniform in 0–2π, radius `√u × spread`; `dir += right·cos a·r + up·sin a·r`, normalised |
| Bloom | `+bloomPerShot` per shot up to `bloomMax`, recovers 0.06 rad/s = **3.44°/s** |
| Switching | keys 1/2/3; the wheel cycles (wheel down = next). Lower 0.18 s, swap model, raise 0.22 s (**0.4 s** total). A new request while lowering replaces the pending one |
| Switch pose | at fully lowered: viewmodel 0.3 m down and pitched 0.7 rad = 40° down, eased with smoothstep |
| View recoil | kick added to the camera pitch/yaw (yaw random ±), then damped back to 0 at rate 7 (it is an offset, the aim returns) |
| Viewmodel recoil | two springs: back `stiffness 180, damping 18` (ζ ≈ 0.67), rotate `160, 16` (ζ ≈ 0.63); each shot adds `kick × 30` to the spring's velocity; applied × 0.6 |
| Melee | nearest living target whose centre is within range + 0.4 m, inside the cone, with line of sight; no target → a raycast for an impact spark on the wall |
| Noise | every shot / swing publishes a noise at the player: type `gunfire` (Unity `Gunshot`) or `melee` |
| Hit marker | `hit:marker { kill, head, melee }` after damage is applied |

### Effects (`world/effects.js`, `models/weapons.js`)

| Item | Value |
| --- | --- |
| Tracers | pool of **24**, a stretched box 0.018 m thick from the muzzle to the hit, fades out over **0.06 s**, colour (4.0, 3.2, 1.8) HDR |
| Muzzle flash | 3 crossed additive quads 0.22 m, random roll, visible **0.05 s**, scale = flash × (0.8–1.2) |
| World flash light | **one** shared point light for every shooter (keeps the light count fixed), range 14 m, intensity 3.2 × flash, **0.06 s** |
| Impacts | sparks + dust by surface (metal, wood, grass, sand, concrete), 3–9 particles, plus a scorch decal (pool of 48) |
| Viewmodel positions | SMG (0.13, −0.125, −0.45), muzzle 0.33 m in front; rifle (0.12, −0.12, −0.46), muzzle 0.81 m in front; knife hand (0.17, −0.18, −0.33). Holder scaled 0.85. Three.js cameras look down −Z, so Unity uses **+Z** for "in front" |
| Viewmodel camera | separate 58° camera so guns never clip into walls (not ported, see below) |

### Unity mapping [GUESS]

- **Pellets.** The playbook's `WeaponData` lists pellets; no prototype weapon fires more than one, so all
  three use 1. Damage is per pellet.
- **Hits.** Unity raycasts against colliders and finds the target with `GetComponentInParent<IDamageable>()`,
  instead of the prototype's hand-written capsule tests. The ray ignores the Player layer and triggers.
- **Melee target centre.** The prototype aims at agent position + 1.1 m; Unity uses the collider's bounds centre.
- **Knife feedback.** The swing animation is not ported, so the knife's `kickRotate` is 12° (prototype 0): each
  swing nods the knife viewmodel so a swing is visible. It does not move the camera.
- **Viewmodels** are placeholder primitives at the prototype's positions, built by `Blackout/Systems/Build Player
  Weapons` with materials in `Assets/Prefabs/Player/Weapons`.
- **Not ported in Y3:** headshot and backstab multipliers (no head hitbox or "in combat" contract yet),
  sniper zoom (a "Could"), surface-specific impacts, decals, shell casings, the viewmodel camera, sway,
  sprint pose and the bolt and swing animations. Weapon audio is a "Should" for later.

---

## 8. Death and respawn (`player/player.js`, `game/mission.js`)

| Item | Value |
| --- | --- |
| Health | 100, clamped at 0, no regeneration. Damage is ignored while dead or invulnerable |
| Death | `player:died` once. Input stops; the HUD shows "YOU DIED — RESPAWNING AT <checkpoint>" (warning toast) |
| Fall-over camera | over **0.8 s**: eye height eases down to **0.25 m**, roll to **1.1 rad = 63°**, pitch to −0.2 rad (looks down 11.5°) |
| Respawn delay | **2.2 s** after death (`mission.js`, `respawnAt`). No game-over screen |
| Respawn | full health, **1.5 s** invulnerability, teleport to the current checkpoint facing its yaw, pitch 0, `player:respawned`. Generator and cure state are kept (the mission is not reset) |
| Kill height | y < −20 m → die |
| Fall damage | `(landedSpeed − 13) × 8` if landed faster than 13 m/s (so a 15 m/s landing costs 16 HP) |
| Damage shake | `shake += amount × 0.03` (max 1), decays 2.5/s; camera jitters by `±shake² × 0.04 rad` (2.3°) as `sin(47 t)` on pitch and `cos(39 t)` on yaw (47 and 39 rad/s, about 7.5 and 6.2 Hz) |
| Checkpoints | beach → forest → bunker entrance → generator room as the mission advances; after the cure is taken the escape checkpoints (escapeEntrance, escapeForest) take over so a death near the boat does not send the player back underground |

**Unity mapping [GUESS].** The prototype moves the checkpoint when the mission phase changes (zones). Until the
MissionDirector exists (Y6), a `Checkpoint` marker becomes the current one when the player comes within
**6 m** of it. Markers whose id starts with `Checkpoint_Escape` only count after `CureTaken`; the others only
before it. Before any checkpoint is reached, the player respawns where they spawned. Y6 can set the
checkpoint directly instead.

## 9. HUD (`ui/hud.js`, `ui/style.css`)

Colours: text `rgba(226,232,226,0.92)`, dim text `rgba(200,210,205,0.55)`, accent `#d9a521`, danger
`#ff3b2a`, good `#47d1d6`, panel `rgba(8,11,14,0.62)`. Font: Bahnschrift/DIN-style condensed sans,
letter-spaced capitals. Margins 34 px from the screen edges, 30 px from top and bottom.

| Element | Position and look | Behaviour |
| --- | --- | --- |
| Health | bottom-left, 300 px wide: "HEALTH" label (12 px, dim), value right-aligned (26 px), 8 px track | fill = health %; a red **ghost** bar trails it, damped at rate **2.5** (never below the fill); fill turns red below **35** |
| Weapon slots | bottom-right, 104 × 58 px panels 8 px apart, key number top-left in accent, name bottom-right (10 px, dim) | inactive at 55 % opacity; active is opaque, 66 px tall, with a 2 px accent underline |
| Crosshair | screen centre | SMG: four 2 × 8 px lines + 3 px dot, gap `6 + spread × 520` px; rifle: dot; knife: 22 px ring + dot. Hidden while dead |
| Hit marker | centre, four 10 × 2 px lines at 45° | visible **0.16 s** (white), **0.35 s** and red on a kill |
| Objective | top-left, max 420 px: phase label (12 px accent, letter-spaced) over the objective (20 px) | text from `objective:changed`; flashes accent for 1.2 s when it changes |
| Checkpoint | top-right: "CHECKPOINT" (12 px dim) over the name (14 px) | a toast "CHECKPOINT — NAME" when one is reached |
| Interaction prompt | centred at 60 % of the height, panel, boxed key + text (17 px), optional hold ring | shown while an interactable is targeted and the player is alive |
| Alert banner | top-centre at 92 px: "SECURITY ALERT" (26 px bold, letter-spaced) + "THE CURE HAS BEEN STOLEN — ALL UNITS CONVERGING", red band fading at the sides | on `alert:broadcast` (the Commander, when the cure is taken): pulses (1 s, opacity 0.55) for **7 s**, then shrinks to a small permanent "SECURITY ALERT" |
| Toast | centre at 28 % of the height, 15 px | **3.2 s**; warning colour for death, good colour for "CURE ACQUIRED" |
| Damage direction | arcs of radius 90 px around the centre, pointing at the attacker | each hit adds an arc that fades over **1.1 s** |
| Damage flash | full-screen red vignette (transparent inside 45 %) | `+amount / 25` per hit (max 1), fades 1.4/s; plus a low-health pulse `clamp01((40 − h) / 40) × (0.75 + 0.25 sin 6t)`, full while dead; opacity = min(0.9, flash + pulse × 0.5) |

**Unity mapping [GUESS].**
- **Text** uses the legacy uGUI `Text` with Unity's built-in font. TextMeshPro would need its Essential Resources
  imported into `Assets/TextMesh Pro`, which no role owns.
- **Alert** shows on the `CureTaken` channel, because the Commander's broadcast has no Unity channel yet.
- **Crosshair** gap is fixed per weapon (it does not open with live spread). Weapon icons are names only.
- **Not ported:** compass and objective distance, flashlight label, sniper scope overlay, objective slide-in animation.

---

## 10. Interactables (`physics/doors.js`, `physics/interactables.js`, `physics/crate.js`)

### Picking what to interact with

| Item | Value |
| --- | --- |
| Prototype rule | every interactable within its radius (doors 2.6 m, generator 2.4 m) and in front (`facing ≥ 0.35`) is scored `facing × 2 − distance × 0.3`; the best one shows its prompt |
| Press | E on the best one (doors). A locked door shows its text with no key |
| Hold | hold E on the same target: the timer adds dt; releasing or changing target resets it; at the hold time `use()` runs once (generator: **2.0 s**). The HUD ring shows `timer / hold` |
| Hold feedback | a 1500 Hz tick at random, about 6 per second, while holding (`interact:holdtick`) |
| While dead | no prompt, no interaction |

### Doors

| Door | Prototype size | Kind | Speed (open fraction / s) | Noise (m) | Start |
| --- | --- | --- | --- | --- | --- |
| Bunker | 3.0 × 3.4 m, two leaves | slide | 0.45 | 22 | unlocked |
| Security | 2.4 × 2.8 m | slide | 1.1 | 14 | unlocked |
| Generator room | 2.4 × 2.8 m | hinge, swings 1.75 rad = 100° into the room | 1.6 | 14 | unlocked |
| Laboratory | 2.6 × 2.8 m | slide, maglock lamp | 1.1 | 14 | **locked** |

- The open fraction moves linearly towards 0 or 1; the panel follows `smoothstep(open)` and slides `width + 0.05 m`
  sideways. The collider stops blocking once `open > 0.8` (hinged: `> 0.35`).
- Prompts: "Open <label>", "Close <label>", "Locked", or "Locked — magnetic lock powered" for the lab.
- Every open or close publishes a `door` noise at the door (and `door:opened` / `door:closed`).
- **Agents** open unlocked doors with `requestOpen(agent)`: when moving faster than 0.3 m/s with a closed door within
  2.4 m ahead (agent-controller.md 3.2). Locked doors block the agents' navigation.
- **Maglock:** 2.6 s after `generator:disabled`, the lab door unlocks and opens itself ("MAGLOCK RELEASED —
  LABORATORY DOOR OPEN"). Lamp: red while locked, green after.

### Generator, cure and health

| Item | Value |
| --- | --- |
| Generator console | hold E for **2.0 s** at the control panel ("Disable generator", key "Hold E"). Then `generator:disabled` and a **60 m** `generator` noise. Only while the generator is running |
| Cure | walk-in: within **1.05 m** horizontally and 1.5 m vertically of the case → `cure:taken` and an **18 m** `alarm` noise; the case disappears. Idle: rotates 0.6 rad/s, bobs ±0.02 m, cyan vials pulse |
| Health pickup | walk over: within **1.1 m** horizontally (1.6 m vertically), **only when hurt**: **+35 HP**, toast "+N HEALTH", the kit disappears. At full health it stays ("HEALTH FULL — medkit left for later", once per visit). Idle: rotates 1.2 rad/s, bobs ±0.05 m, small green light |

### Barricade crate

| Item | Value |
| --- | --- |
| Size | 1.5 m cube in the corridor (Unity crate space (0.7, 2.5, 95), docs/level-layout.md) |
| Push | walking into it pushes along the dominant axis only; target speed `min(playerSpeed, 4.5) × 0.42` (max 1.9 m/s), approached at damp rate **6**, for 0.12 s after each contact |
| Friction | when not pushed it slows by **5.5 m/s²** until it stops |
| Noise | while moving faster than 0.05 m/s, an **11 m** `crate` scrape every **0.45 s** |
| Navigation | after it moves, the nav grid is re-marked so agents path around it |

### Unity mapping [GUESS]

- **Picking** is a camera raycast of **3 m** (the playbook), not the prototype's scoring. The first collider hit decides,
  so walls block interaction. Each door also has a **trigger box filling its gap** (1.4 × 2.3 × 0.4 m), because the panel's
  collider switches off once the door is open: without it an open door could not be aimed at and closed. Only the
  interaction ray hits triggers; movement, agents, bullets and the knife all ignore them.
- **Placement:** prefabs are instanced at the DoorSocket, GeneratorSocket, CureSocket and HealthPickup markers into
  `Assets/Scenes/Interactables.unity`, which the Systems Engineer owns; the Environment scene is only read. Every Unity
  door gap is **1.4 × 2.3 m** (level-layout.md), so all doors use one panel size. **All doors slide**, D-Generator included.
  D-Bunker keeps the slow speed and 22 m noise.
- **Navigation:** a door carves the NavMesh only while **locked**. Closed, unlocked doors stay walkable so agents can
  reach them and call `IDoor.RequestOpen`.
- **Noise types:** `Alarm` (cure) and `Crate` (scrape) are appended to `NoiseType`.
- **Crate:** a Rigidbody (mass and linear damping) with all rotation frozen, pushed by the player's
  `OnControllerColliderHit`, with a carving NavMeshObstacle that re-carves when it stops.
- **Not ported:** the maglock toast, door and pickup sounds (UnityEvent hooks instead), and the crate tilt and dust.

## 11. Mission flow (`game/mission.js`, `ui/menus.js`)

### Phases and objectives

The prototype checks the player's position every frame (after 20 other updates) and only ever moves forward.

| Prototype phase | Entered when | Objective text | HUD label | Checkpoint set |
| --- | --- | --- | --- | --- |
| `beach` | start | Reach the bunker in the north of the island | BEACH | beach |
| `forest` | left the beach (crossed the beach's inland edge) | Follow the trail and get into the bunker | FOREST | forest |
| `entrance` | inside the compound (from beach or forest) | Open the bunker door [E], then Go down the ramp to level B1 once `door:opened` for the bunker door | BUNKER ENTRANCE | entrance |
| `bunker` | underground (`player.zone === 'bunker'`) | Find the generator room | LEVEL B1 | (kept) |
| `generator` | inside the generator room (from entrance or bunker) | Disable the generator — hold [E] at the control console | GENERATOR ROOM | generator |
| (same phase) | `generator:disabled` | Take the cure from the laboratory | GENERATOR ROOM | (kept) |
| `escape` | `cure:taken` | Escape — get back to the boat! | ESCAPE | (kept, then the escape ones below) |

- **Events:** `objective:changed { text, phase, label }` whenever the text changes, `phase:changed` and `checkpoint`
  when the phase changes. Toasts: "POWER FAILURE — EMERGENCY LIGHTING ACTIVE" on the blackout, "CURE ACQUIRED" on the cure.
- **Escape checkpoints:** while the checkpoint is still `generator`, being outside inside the compound moves it to
  `escapeEntrance`; then being out of the compound and past the gate moves it to `escapeForest`.
- **Win:** after `cure:taken`, within the boat zone's radius (**6 m**, horizontal) and above y 0.5 →
  `mission:complete { time, kills, deaths }`, once.
- **Counters:** `kills` +1 on every `enemy:killed`, `deaths` +1 on every `player:died`, time from the mission start.

### Mission Complete screen (`menus.js`)

The game pauses and the pointer is released. Title **MISSION COMPLETE**, subtitle **THE CURE IS OFF THE ISLAND**,
then a panel (dark, accent line on top) with a two-column stats grid: TIME `m:ss` (rounded seconds), KILLS
`kills / total agents`, DEATHS. Labels 15 px, dim, letter-spaced; values 20 px. One **RESTART** button, which reloads
the page (the whole mission starts again).

### Unity mapping [GUESS]

- **Phases** follow the playbook: **Beach → Forest → BunkerEntrance → Corridor → GeneratorRoom → Lab → Escape →
  Complete**. `Corridor` is the prototype's `bunker` phase (underground, before the generator room). `Lab` is the
  prototype's "generator disabled" objective, made its own phase. Phases only move forward; a debug jump is the only
  way back, and it keeps the generator and cure state.
- **Zones** come from the zone table in `docs/level-layout.md`, as boxes in `MissionZones`:
  - Forest: z > −92 (the beach's inland edge).
  - Compound: x −22 … 22, z 28 … 62, y > 7.
  - Underground: the SecurityArea, Corridor, GeneratorRoom and Laboratory boxes, floor 2.5, y 1.5 … 9.
  - Generator room: x −19.9 … −1.9, z 96 … 116, y 1.5 … 9.
  - Boat zone: 6 m around the `BoatExit` marker, horizontal, y > 0.5.
- **Transitions:** the prototype's zone tests, plus `GeneratorDisabled` moves any earlier phase to Lab, and
  `CureTaken` moves any earlier phase to Escape. Complete needs Escape (so the cure) and the boat zone.
- **Objectives** are the prototype strings, raised on `ObjectiveChanged` only when the text changes. The
  BunkerEntrance text switches once `Door_D-Bunker` is open. The HUD shows the text only, without a phase label.
- **Checkpoints:** the director tells `CheckpointTracker` which marker to use, and the 6 m distance rule (section 8)
  switches off:
  - Beach → `Checkpoint_Beach`
  - Forest → `Checkpoint_Forest`
  - BunkerEntrance and Corridor → `Checkpoint_BunkerEntrance`
  - GeneratorRoom and Lab → `Checkpoint_GeneratorRoom`
  - Escape → `Checkpoint_EscapeEntrance`, once back in the compound
  - Escape → `Checkpoint_EscapeForest`, once out of the compound with z < 26
- **Kills** count the player's own kills (`WeaponController.HitConfirmed(killed: true)`). `Shared` has no
  "agent killed" channel, so KILLS shows the count only, without "/ total". **Deaths** count `PlayerDied`.
  **Time** is from the moment the scenes have loaded.
- **Complete:** `MissionComplete` is raised once; player input stops, the cursor is released and `Time.timeScale` is
  0. Restart sets the time scale back to 1 and reloads `Bootstrap.unity`.
- **Bootstrap:** `Bootstrap.unity` holds the Player (inactive until the level has loaded), the MissionDirector, the
  Mission Complete screen and the UI EventSystem. It loads Environment, Interactables and Agents additively, skips a
  scene that is not in Build Settings (Agents.unity was added in Y7), makes **Environment the active scene** (fog,
  skybox and ambient light), and then places the player at `PlayerStart`. A black LOADING screen covers the wait.
- **Debug jumps** (`Blackout/Debug/Mission`, Play mode): the player goes to the phase's start and the mission
  jumps there. The starts are:
  - Beach, Forest, BunkerEntrance and GeneratorRoom: the phase's checkpoint.
  - Corridor: 2 m past `DoorSocket_D-Security`.
  - Lab: 2 m past `DoorSocket_D-Lab`.
  - Escape: 2 m in front of `CureSocket`.
  - Complete: `BoatExit`.
  Jumping to Lab or later turns the generator off, and to Escape or later takes the cure. Both go through the real
  objects, so the blackout, the lab door and the alert happen as in play.
- **Not ported:** the start screen and pause menu, phase-change and power-failure toasts, the compass.

---

## 12. Physics hygiene (`physics/collision.js`, `weapons/weapon_system.js`)

### What the prototype does

- **No physics engine.** Static colliders are boxes, vertical cylinders and one ramp, stored in a 2D spatial hash with
  **4 m cells**. A raycast walks the cells along the ray (2D DDA, at most 400 cells), so bullets and line-of-sight
  checks only test colliders near the ray. Terrain is tested analytically.
- **Per-collider flags instead of layers:** `sight` (blocks line of sight), `bullets` (stops bullets) and `wall` (stops
  sound). A raycast has a `mode` (`bullet`, `sight` or `sound`) and skips colliders without that flag.
  - The dock railings have `bullets: false` and `sight: false`: bullets and eyes pass through them.
  - The boat's fence panel has `sight: false` only: it stops bullets but not eyes.
- **Hitscan** (`Combat.hitscan`): first the world ray (`mode: 'bullet'`), then:
  - The **player's** shots test every living agent's hitbox: a capsule of radius **0.3 m** and a head sphere of
    **0.14 m** (0.17 m for the big agents). The agent closer than the world hit wins.
  - An **agent's** shots test only the player's capsule (radius **0.34 m**, from 0.25 m over the feet to 0.2 m under the
    head). Agents never hit each other: there is no friendly fire.
- Nothing in the prototype is simulated with forces except the crate (section 10) and the death fall.

### Unity mapping

**Layers (P0, verified in Y7).** The playbook's six layers are defined at the indices teammates already use.
`Blackout.Shared.PhysicsLayers` holds them as constants, and EditMode tests fail if `ProjectSettings` stops matching.

| Layer | Index | At the start of Y7 | After Y7 | Meant for |
| --- | --- | --- | --- | --- |
| Default | 0 | All of Environment.unity's colliders (337 at runtime) | **no colliders**; renderers without colliders stay here | — |
| Ignore Raycast | 2 | nothing | the **18 dock railings** | colliders no query should see |
| UI | 5 | the HUD and the Bootstrap canvas (no colliders) | same | — |
| Player | 6 | the Player prefab (one CharacterController) | same | the player |
| Enemy | 7 | AgentBase and its four variants (one capsule each) | same | agent bodies |
| Level | 8 | nothing | **319 colliders**: the level (318) and the generator's body | static level geometry |
| Interactable | 9 | doors, generator console, cure, health pickups, crate | same | things the player uses |
| Hit | 10 | nothing | nothing yet | hitboxes that only queries see (heads) |
| Ragdoll | 11 | nothing | nothing yet | ragdoll limbs |

**Collision matrix (P0, verified in Y7).** As found, it matched the playbook exactly:

- Enemy × Enemy is off, so agents never push each other (the NavMesh avoidance handles crowds).
- Ragdoll collides only with Level, so a falling body never pushes the player, agents, doors or another ragdoll.
- Every other pair is on.

That was 11 of the 66 pairs between the 11 named layers. After Y7, Hit collides with nothing (finding 2), so
**21 of 66** are off.

**Findings.** Reported to the team first (playbook Y7). The team then agreed, and all four were changed in Y7:

1. **Level was empty.** The World Builder's colliders were all on Default. Ragdoll collides with Level only, so a
   ragdoll would have fallen through the terrain and the bunker floors. The Tactical Guard's sight check counts only
   Level as blocking, so **guards could see through every wall**.
   *Changed:* every solid collider in Environment.unity moved to Level (318), and so did the nested generator body in
   `Generator.prefab`. Ragdoll × Default stays off.
2. **Hit collided with everything.** A head hitbox on Hit would push and be pushed by the player, the doors and the
   other agents' bodies. *Changed:* Hit collides with nothing; rays still hit it.
3. **Agent shots could hit agents.** `AgentShooter.hitMask` was everything, and the prototype has no friendly fire.
   *Changed:* it is `PhysicsLayers.AgentShots`, both as the default in `AgentShooter.cs` and in `AgentBase.prefab`.
4. **The dock railings stopped bullets.** In the prototype they have `bullets: false` and `sight: false`.
   *Changed:* the 18 `Dock_Rail_*` colliders are on Ignore Raycast. The player still bumps into them, while bullets
   and guards' eyes pass through.

The scene and prefab changes are made by **`Blackout/Systems/Assign Physics Layers`**:
- It moves only objects still on Default, and it skips triggers and objects with a Rigidbody, so a layer set by hand
  is kept.
- A second run changes nothing.
- **Re-run it after a World Builder tool rebuilds part of the level**, because new objects are created on Default.
- The Stress Test's PlayMode test fails if any collider is left on Default, and its report names each one.

**Weapon masks [GUESS].** The weapons now list the layers they hit instead of leaving some out:

- `PlayerShots` = Default, Level, Enemy, Interactable, Hit. These are the world, the agents, the doors and the crate,
  and the hitboxes. It leaves out Player (the shooter), Ragdoll (dead bodies), UI, Water, TransparentFX, Ignore Raycast
  and the 20 unnamed layers. Before Y7 the mask was "everything except Player, Ignore Raycast and Ragdoll", which
  also let UI, Water, TransparentFX and any future layer stop bullets.
- `AgentShots` = Default, Level, Player, Interactable, used by `AgentShooter` (finding 3).
- All weapon queries pass `QueryTriggerInteraction.Ignore`, so the triggers (pickups, doorway, cure) never stop
  bullets, whatever "Queries Hit Triggers" says in the project settings.
- The knife's overlap sphere and line-of-sight check use the same mask as the guns.
- The player's ground probe (`PlayerMotor`'s 0.35 m snap) uses `PhysicsLayers.CollisionMask(Player)` minus Player:
  the layers the capsule collides with, read from the matrix. Before, it used "everything except Player", so after
  Hit stopped colliding it could snap the player onto a head hitbox or a ragdoll the capsule passes through.

**Physics settings (`DynamicsManager`, `TimeManager`), checked and kept:**

| Setting | Value | Why it stays |
| --- | --- | --- |
| Fixed Timestep | 0.02 s (50 Hz) | Only the crate and door bodies are simulated; the player and agents move in `Update` |
| Maximum Allowed Timestep | 0.333 s | Unity default; a hitch runs at most 16 physics steps |
| Solver iterations / velocity iterations | 6 / 1 | The crate is the only stacked body |
| Auto Sync Transforms | off | Unity 6 default; queries see transforms as of the last simulation or `SyncTransforms` |
| Reuse Collision Callbacks | on | No allocation per collision callback |
| Queries Hit Triggers | on | Every query in our code says what it wants explicitly |
| Contact pairs mode / broadphase | default / Sweep and Prune | Almost every collider is static and never moves, so Sweep and Prune has little to re-sort |

**Stress Test (`Blackout/Debug/Stress Test`, Play mode in Bootstrap) [GUESS].** A scripted run, not in the prototype:

- The player is made invulnerable for the run.
- At each phase start (the Y6 debug jumps, Beach to Escape) it puts a target dummy in front of the player. The dummy
  is a 1.8 m capsule on Enemy, with a head box on Hit and a Health.
- It fires 30 SMG shots, 5 rifle shots and 5 knife swings through the real WeaponController. It ends with the jump
  to Complete.
- **Missed hits:**
  - Every pellet is checked against the dummy's colliders alone. A pellet whose ray passes through the dummy, but whose
    weapon hit nothing or something farther away, is a missed hit (the mask left the dummy's layer out).
  - A pellet stopped by the player's own collider is counted separately.
  - A knife swing at a dummy 1.2 m ahead must land.
- **Null references:** every exception or error logged during the run is counted, null references by name.
- **Numbers:**
  - colliders and rigidbodies per layer in the loaded scenes
  - pellets per layer they hit
  - microseconds per raycast with `PlayerShots` and with every layer (2,000 rays from each start)
  - bytes allocated by the firing calls
- **Output:** a summary in the Console and a report file in `Application.temporaryCachePath`. A PlayMode test runs the
  same script and fails on any null reference, exception or missed hit.
