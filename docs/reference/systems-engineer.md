# Systems Engineer — reference extracted from the web prototype

Source: `prototype/src/player/player.js`, `prototype/src/core/input.js`, `prototype/src/core/engine.js`,
`prototype/src/core/namespace.js` and the `noise` emitters across `prototype/src`. Written at the start of
step Y2 because step Y0 had not been done yet. Sections 1–6 cover the **player** (Y2), section 7 the
**weapons** (Y3, from `weapons/weapon_system.js`, `models/weapons.js` and `world/effects.js`), sections
8–9 **death, respawn and the HUD** (Y4, from `player/player.js`, `game/mission.js`, `ui/hud.js` and
`ui/style.css`), and section 10 the **interactables** (Y5, from `physics/doors.js`, `physics/interactables.js`
and `physics/crate.js`), section 11 the **mission flow** (Y6, from `game/mission.js` and `ui/menus.js`), and
section 12 **physics hygiene** (Y7, from `physics/collision.js` and `weapons/weapon_system.js`). Section 13 starts the
**Hunter** AI with its behaviour tree core (H1, from `agents/hunter_bt.js`), section 14 covers its **hearing**
(H2, from `agents/perception.js` and `physics/collision.js`), section 15 its **tree** (H3, from
`agents/hunter_bt.js` and `agents/perception.js`), and section 16 its **debug view** (H4, from `ui/debug_overlay.js`).
Section 17 covers the **audio events** (Y0, from `audio/audio.js`), the last part of the Y0 extraction.

**Y0 checklist** (playbook step Y0, done after the fact; every item is now covered):

| Y0 asks for | Section |
| --- | --- |
| Player speeds, jump, sprint, crouch | 1 |
| Head bob | 3 |
| Each weapon's damage, range, cooldown, spread, falloff, switch time, recoil | 7 |
| Health numbers | 5, 8 |
| Interaction ranges and hold times; door and crate behaviour | 10 |
| Mission phases with objective strings | 11 |
| Respawn rules | 8 |
| Noise event types and loudness values | 4, 14 |
| HUD elements | 9 |
| Audio events | 17 |

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
| Player weapon / melee | SMG 42, rifle 65, knife 3 (`d.noise`, section 7) | `weapons/weapon_system.js` |
| Agent gunfire | 40 for every agent type (the `agent_base.js` default; no type overrides it) | `agents/agent_base.js` |
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
| `entrance` | inside the compound (from beach or forest) | Open the bunker door [E]; Go down the ramp to level B1 once `door:opened` for the bunker door (at once if it is already open) | BUNKER ENTRANCE | entrance |
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

---

## 13. Hunter behaviour tree core (`agents/hunter_bt.js`)

### What the prototype does

- **A small tree library written by hand** at the top of `hunter_bt.js`, used only by the Hunter. Every node returns
  `SUCCESS`, `FAILURE` or `RUNNING`, and has a `reset()`.
- **Ticked at 10 Hz from the root** (the agent's `think()`), not every frame. The 10 Hz rate is the brain's job (H3).
- **Selector** (`?`): ticks its children in order and returns the first result that is not `FAILURE`.
  - It is **reactive**: every tick starts again from the first child, so a higher-priority branch interrupts a lower
    one that is still `RUNNING`.
  - When a different child wins than the one that was running, the one that was running is **reset**. If every child
    fails, the running one is reset too.
- **Sequence** (`→`): ticks its children in order and **keeps its place** while a child is `RUNNING`.
  - **Reactive guards:** each tick, the `Condition` children *before* the running child are checked again. If one now
    fails, the Sequence resets and fails, so a running action stops as soon as its premise is false.
  - On `FAILURE` of any child, or when the last child succeeds, it resets (back to the first child).
- **Condition:** a predicate on the agent, `true` → `SUCCESS`, `false` → `FAILURE`. Never `RUNNING`.
- **Action:** a function `(agent, mem) → status`. `mem` is a per-node scratch object, cleared on `reset()`, plus an
  optional `onReset` callback. Actions set fields on the agent's intent; they never move the agent themselves.
- **Wait(min, max):** an Action that returns `RUNNING` until a random time in `[min, max]` s has passed.
- **Blackboard:** `this.bb = {}` on each Hunter, "the tree's private scratch memory". Conditions write what they found
  (`bb.noise`, `bb.step`, `bb.prowl`) and later actions in the same branch read it.
- **Debug:** every node pushes `name:status` into `btTrace` each tick; the overlay shows the running path, for example
  `Root > ReactToFootsteps > SneakToNoise` (H4).
- There is **no Inverter and no Cooldown** in the prototype. The playbook asks for both.

The tree itself (H3, for context):

```
Root ?
├─ → Engage            [SeesPlayer] ? ( → [InRange] AimAndFire | CloseIn )
├─ → HuntLastSeen      [SawRecently] StalkToLastSeen
├─ → ReactToGunfire    [HeardGunfire] ? ( → [GunfireClose] TakeCover | FlankGunfire )
├─ → ReactToFootsteps  [HeardFootsteps] SneakToNoise  Ambush(4 s)
├─ → FollowOrders      [HasDirective] MoveToDirective
└─ → Prowl             PickProwlPoint  WalkTo  LookAround(2–4 s)
```

### Unity mapping

All in `Assets/Scripts/AI/Hunter`, assembly `Blackout.AI.Hunter` (references `Blackout.Shared` only), namespace
`Blackout.AI.Hunter`. No packages.

| Prototype | Unity | Notes |
| --- | --- | --- |
| `SUCCESS` / `FAILURE` / `RUNNING` | `NodeStatus` enum | |
| `Node.run` / `tick` / `reset` | `Node.Tick(BtContext)`, `Node.Reset()` | `Tick` also stores `LastStatus`, for the H4 debug text without a per-tick trace list |
| `Selector` | `Selector` | Same rules: reactive, resets the interrupted child |
| `Sequence` | `Sequence` | Same rules: keeps its place, re-checks the guards before the running child. A guard is a `Condition` or an `Inverter` of one (`Node.IsGuard`), so "not seen" checks are re-checked too [GUESS] |
| `Condition` | `Condition` | `Func<BtContext, bool>` |
| `Action` | `ActionNode` | `Func<BtContext, NodeStatus>` plus an optional reset callback. Named `ActionNode` because `Action` is `System.Action` |
| (none) | `Inverter` [GUESS] | Swaps `SUCCESS` and `FAILURE`; `RUNNING` passes through |
| (none) | `Cooldown` [GUESS] | See below |
| `this.bb = {}` | `LocalBlackboard` | Typed keys; see below |
| `BI.Engine.time` | `BtContext.Time` | The tree's own clock, advanced by `BehaviourTree.Tick(deltaTime)` |
| `think()` calling `tree.run` | `BehaviourTree.Tick(deltaTime)` | The 10 Hz throttle is added by `HunterBrain` in H3 |

**Cooldown [GUESS].** A decorator with a duration in seconds:

- While cooling down it returns `FAILURE` and does not tick its child, so a Selector moves on to the next branch.
- The cooldown starts when the child **finishes** (`SUCCESS` or `FAILURE`), not while it is `RUNNING`.
- `Reset()` resets the child but **keeps the cooldown**. Otherwise a Selector interrupting the branch would let it fire
  again straight away, which is what a cooldown exists to stop.
- Intended use in H3: stop the Hunter re-running a short reaction (for example the 4 s ambush) on every new footstep.

**LocalBlackboard [GUESS].** Per-agent values shared by the nodes of one tree. It is *not* the squad `Blackboard` in
`Blackout.Shared`, which all agents share.

- Keys are typed objects, `BlackboardKey<T>`, created once when the tree is built. A wrong type is a compile error, not
  a runtime cast.
- Values live in one dictionary per value type, so storing a `float` or a `Vector3` does not box. After the first write
  of a key, writing it again allocates nothing.
- `TryGet` returns `false` for a key that was never set or was removed, so a condition can tell "no noise heard" from
  "noise at the origin".

**Clock.** Nodes read `BtContext.Time`, never `Time.time`. EditMode tests can then step the tree by exact amounts,
and the Hunter's timers pause with the tree.

**Allocation.** Children are stored in arrays and the delegates are created when the tree is built, so a tick of a
built tree allocates nothing. An EditMode test measures this.

---

## 14. Hunter hearing and sound propagation (`agents/perception.js`, `physics/collision.js`)

### What the prototype does

- **Every noise has a radius** in metres (the `NoiseEvent.loudness` field in Unity): the distance at which it can just
  be heard in open space. Noises are emitted at the source's feet (player, shooter) or at the object (door,
  generator).
- **For each agent within the radius** (and not the noise's own source), `BI.Noise.emit` works out a perceived
  loudness two ways and keeps the larger:
  - **Path:** `1 − pathDistance / radius`, where `pathDistance` comes from a Dijkstra flood over the nav grid that goes
    around walls and through open doors only. Sound bends around corners this way.
  - **Through walls** (only if the path value is ≤ 0.05):
    `(1 − straightDistance / radius) × 0.35 ^ wallsCrossed`.
  - The result is multiplied by the agent's **hearing**: Hunter **1.7**, Security Patrol 1.0, Tactical Guard 0.9,
    Commander 0.9.
- **Threshold:** a perceived loudness of **0.04** or less is not heard.
- **Walls crossed** (`Physics.wallsBetween`): a ray from **1.2 m** above the noise to **1.6 m** above the agent's feet.
  Each hit counts one wall; the ray continues **0.3 m** past it, ignoring that collider. At most **3** walls are
  counted. The terrain is ignored, and colliders flagged `wall: false` (tables, catwalks, the low platform rims, a
  pole) do not count. Everything else does, closed doors included.
- **Memory:** each agent keeps a list of heard noises `{position, loudness, type, time}` and drops any older than
  **8 s**. `recentNoise(types, maxAge)` returns the loudest noise of the given types heard in the last `maxAge`
  seconds. The Hunter's tree asks for gunfire in the last 6 s (loudness > 0.06) and footsteps in the last 5 s
  (> 0.04). The stored loudness never fades; old noises just disappear.

### Noise radii in Unity today

| Source | Radius (m) | Set in |
| --- | --- | --- |
| Walk / sprint footstep | 0–4.5 / 13 (speed curve, section 4) | `PlayerMovementSettings` |
| Jump / landing | 7 / 9 | `PlayerMotor` |
| SMG / rifle / knife | 42 / 65 / 3 | `Assets/ScriptableObjects/Weapons` |
| Agent gunfire | 40 | `AgentWeaponConfig` (Agent Controller) |
| Door | 14 | `Door.prefab` |
| Generator disabled | 60 | `Generator.prefab` |
| Cure alarm | 18 | `CurePickup.prefab` |
| Crate scrape | 11 | `Crate.prefab` |

### Unity mapping

The playbook asks for distance falloff plus a reduction per wall counted with raycasts. That is the prototype's
through-walls formula, used for every noise:

```
perceived = max(0, 1 − distance / radius) × wallFactor ^ walls × hearing
          = max(0, 1 − d / r)             × 0.35 ^ walls       × 1.7
```

| Constant | Value | Source | Effect |
| --- | --- | --- | --- |
| `hearing` | 1.7 | prototype Hunter | a noise right next to the Hunter is perceived at 1.7 |
| `wallFactor` | 0.35 | prototype | each wall keeps 35 % of the sound |
| `maxWalls` | 3 | prototype | a 4th wall changes nothing, so 3 raycast hits at most |
| `threshold` | 0.04 | prototype | quieter than this is not heard |
| `sourceHeight` / `listenerHeight` | 1.2 / 1.6 m | prototype | ray ends: above the noise / the Hunter's ears |
| `memorySeconds` | 8 | prototype forget time | how long the remembered noise takes to fade to 0 [GUESS] |

How far a noise is heard (perceived > 0.04) for the Hunter, as a fraction of its radius:

| Walls | Heard up to | Sprint (13 m) | SMG (42 m) | Generator (60 m) |
| --- | --- | --- | --- | --- |
| 0 | 0.98 r | 12.7 m | 41 m | 59 m |
| 1 | 0.93 r | 12.1 m | 39 m | 56 m |
| 2 | 0.81 r | 10.5 m | 34 m | 48 m |
| 3 or more | 0.45 r | 5.9 m | 19 m | 27 m |

**Wall counting [GUESS].** One `Physics.RaycastNonAlloc` from the source point to the ear point into a preallocated
buffer. Unity returns one hit per collider, so each collider counts once (the prototype's "continue 0.3 m past it").

- Layers: **Level** (the bunker and the props, after Y7) and **Interactable** (doors, so a closed door muffles and an
  open one, slid into the wall, does not). Triggers are ignored.
- `TerrainCollider` hits are skipped, as in the prototype, so a hill between the two is not a wall.
- Capped at `maxWalls`.
- Unlike the prototype, low props on Level (desks, tables) would count if the ray crossed them; at 1.2–1.6 m the ray
  passes over them.

**Not ported: the path flood.** Sound bending around corners and through open doorways is not modelled; a noise
heard around a corner is muffled as if it came through the wall. The playbook asks for the raycast model, and the
flood needs a grid over the whole level.

**One remembered noise [GUESS].** The playbook asks the Hunter to keep "the strongest recent noise with a timestamp
and decay it", instead of the prototype's list:

- The remembered loudness fades linearly to 0 over `memorySeconds` (8 s, the prototype's forget time):
  `current = perceived × max(0, 1 − age / 8)`.
- A new noise replaces it if its perceived loudness is **at least the current faded value**. A sprinting player's
  steps keep refreshing the position; a distant generator does not mask a footstep that is louder *to the Hunter*.
- The noise's own source (the Hunter's own gunfire) is ignored.
- **Exposed** for H3: `PerceivedLoudness` (faded, now), `NoiseDirection` (flat unit vector from the Hunter to the
  noise), `TimeSinceNoise` (infinity if nothing heard), plus `NoisePosition`, `NoiseType` and `HasNoise`.

**Cost [GUESS].** The raycast is skipped when the open-space value is already ≤ the threshold or cannot beat the
current memory, so far-away noises cost a distance check only.

---

## 15. The Hunter's tree (`agents/hunter_bt.js`, `agents/perception.js`)

### What the prototype does

The tree is in section 13. Its leaves and numbers:

| Leaf | Behaviour |
| --- | --- |
| Engage | `[SeesPlayer: detected and seen in the last 0.6 s]` then `Shoot` or `CloseIn` |
| Shoot / AimAndFire | `[InRange: distance < 32 m × rangeMul and can see]`: stand, aim, fire, look at the player's chest; crouch beyond 14 m. Running while it can see |
| CloseIn | run to the player (arrive 6 m), aim, do not fire |
| HuntLastSeen / StalkToLastSeen | `[seen in the last 10 s, cannot see now]`: run to the last seen position (arrive 1.8 m), aim; then forget the sighting |
| ReactToGunfire | `[gunfire, generator or alarm in the last 6 s, loudness > 0.06]`: within 16 m take cover (a tree within 12 m, 0.9 m behind it from the threat), crouch, aim at the noise, hold 2.5 s; else run to a flank point 12 m to the side, then walk in (arrive 4 m) |
| ReactToFootsteps | `[footstep, sprint, jump, land, door, crate or melee in the last 5 s, loudness > 0.04]`: crouch and sneak (1.15 m/s) to the noise (arrive 3 m), then ambush: stand, aim at it for 4 s, then forget the noises |
| FollowOrders | `[has a Commander directive]`: converge and search run, regroup walks (arrive 2.5 m) |
| Prowl | next of its 6 forest waypoints (advance 1 or 2 at random), walk (arrive 2 m), look around 2–4 s |
| Every move | fails after 25 s or when the path fails 3 times |

**Vision** (Hunter: full cone **2.0 rad** (115°), range **25 m**, eyes at **1.6 m**):

- Sees the player if within range and inside the cone (or within **2.2 m**, any direction), and a ray from its eyes
  reaches the player's head or chest (`height × 0.55`) without hitting a wall, trunk or door.
- **Awareness** fills while it sees the player:
  `+= 2.2 × max(0.15, 1.25 − distance / range) × visibility × (detected ? 3 : 1) × dt`, capped at **1.5**.
  **Detected** at 1. Unseen, it falls **0.35/s** (**0.25/s** once detected). Detection is lost **6 s** after the
  player was last seen. Each detection writes a sighting to the squad Blackboard.
- **Visibility** of the player: crouching ×0.55, sprinting ×1.25, flashlight on ×1.6, outdoors without a flashlight
  ×0.8, multiplied together.
- **Alert** (Commander): vision range ×1.4.
- **Camouflage:** only the Hunter's ghillie model. There is no camouflage number.

### Unity mapping

**The brief's six branches, in its order, plus the Commander's orders** (`docs/viva/commander.md` asks every brain
to implement `IDirectiveReceiver` and `IAlertReceiver`):

```
Root ?
├─ → Attack            [SeesPlayer] [InRange] AimAndFire
├─ → Pursue            [SeesPlayer] CloseIn
├─ → Investigate       [HeardPreyNoise] MoveToNoise  Ambush
├─ → CautiousApproach  [HeardGunfire] ? ( → [GunfireClose] TakeCover | AdvanceByCover )
├─ → Search            [SawRecently] GoToLastSeen  LookAround(3 s)
├─ → FollowOrders      [HasOrder] MoveToOrder
└─ → Roam              NextWaypoint  WalkTo  LookAround(2–4 s)
```

- The brief puts **Investigate and CautiousApproach before Search**: a fresh noise outranks an old sighting. The
  prototype hunts the last sighting first. I follow the brief.
- **Attack and Pursue** are the prototype's `Shoot` and `CloseIn`, split into two branches as the brief names them.
- **Reaction delay and aim error** are already in the Agent Controller's `AgentShooter` (A4): the Hunter's
  `reactionDelay` 0.7 s and `spread` 0.024 come from `DifficultyConfig`. The brain only sets `aim`, `fire` and the
  look target, as every brain does; it does not repeat them.

**Different reactions by noise type [GUESS where marked].** The H2 hearing remembers one noise and its type:

| Heard | Branch | Move | Then |
| --- | --- | --- | --- |
| Gunshot, Generator, Alarm (loudness > 0.06, last 6 s) | CautiousApproach | within 16 m: run to cover, hold 2.5 s aiming at the noise; else **walk** from cover to cover towards it [GUESS] | marks the noise handled |
| Footstep, Door, Crate, Melee (> 0.04, last 5 s) | Investigate | **walk** to it (the prototype sneaks; Unity has no sneak speed) | ambush 4 s, aiming at it |
| Sprint (> 0.04, last 5 s) | Investigate | **run** to it: the prey is fleeing [GUESS] | ambush 2 s [GUESS] |

- **Cover for the advance [GUESS]:** `CoverPoint` markers and the terrain's trees, rocks and bushes, read once at
  start. Bushes are terrain *details* (W6), not trees: every 0.78 m detail cell holding one is a cover position, read
  once per terrain for both Hunters. On the island: 657 trees and rocks and 371 bush cells (376 bushes).
  The next cover is one within 15 m of the Hunter that is at least 4 m nearer the noise; the Hunter waits 1.5 s
  there, then moves on. With no such cover it walks straight in. Within 16 m of the noise the take-cover rule wins,
  which ends the advance. This replaces the prototype's flank point, so the brief's "advance using cover/bushes,
  slower" is literal.
- **Handled noises:** instead of clearing the hearing, the tree records the time of the noise it finished with,
  and ignores noises heard before it.

**Camouflage [GUESS: the playbook's own rule].** When the player **stands still** (speed under **0.3 m/s**), the
Hunter's awareness fills **× 0.5** as fast (`HunterConfig.camouflageMultiplier`). Read as: a motionless player
blends into the forest, which is what the ghillie-wearing Hunter itself relies on.

**Visibility in Unity [GUESS].** The player has no crouch or flashlight yet (Y2), and the Hunter only knows the
player's Transform. Visibility comes from the player's speed, measured between brain ticks: still → camouflage,
sprinting (over **5.5 m/s**) → ×1.25, otherwise ×1.

**Not portable now:** crouching and the sneak speed (`AgentIntent` has neither; flagged by the Agent Controller in
A0), the outdoor ×0.8 and flashlight ×1.6 factors, and the flank point.

**Architecture.**

- `HunterBrain` (on the Hunter, beside `HearingSystem`) senses once per think tick (10 Hz, from `AgentRoot`), writes
  the facts to the tree's `LocalBlackboard`, ticks the tree, and returns the `AgentIntent` the leaves wrote.
- **The tree reads only the blackboard.** Tests fill a blackboard by hand (the brief's "fake blackboard") and check
  the intent each branch returns, with no scene, no physics and no player.
- `HunterVision` is the awareness meter as plain C#; line of sight is one `Physics.Linecast` against Level and
  Interactable (walls and doors), behind an interface so tests fake it.
- Sightings go to the squad Blackboard (`lastKnownPlayerPos`, `lastKnownPlayerTime`), as in the prototype.
- Orders: Converge runs to the point and completes on arrival; SearchZone runs to 3 points inside the radius, then
  completes [GUESS]; Regroup walks and completes on arrival. A higher branch interrupts an order without clearing it.

**Wiring (with the Agent Controller's permission).** `Blackout/Systems/Build Hunter Config` creates
`HearingConfig.asset` and `HunterConfig.asset` in `Assets/Scripts/AI/Hunter/Config`, and adds a `HearingSystem`
(HearingConfig, `Noise.asset`) and a `HunterBrain` (HunterConfig, that HearingSystem) to the root of
`Assets/Prefabs/Agents/Hunter.prefab`, a variant of `AgentBase`. `AgentRoot` finds the brain, so the spawner no longer
gives Hunters its placeholder walker. The tool only adds what is missing and fills empty fields; run twice, the second
run changes nothing.

- `AgentBase`'s `AgentShooter` has no gunfire channel assigned, so agents' shots make no noise yet: the Hunter hears
  the player's gunfire, not other agents'. Assigning `Noise.asset` there is the Agent Controller's change.

---

## 16. Hunter debug view (`ui/debug_overlay.js`)

### What the prototype does

The F1 overlay draws, for every agent:

- **A vision cone on the floor**, the agent's field of view and range, coloured by state: calm **#40FF80** (green),
  suspicious **#FFB030** (orange: investigate, search, gunfire, footsteps, cover, orders), combat **#FF3020** (red:
  attack, pursue, engage). Grey when dead.
- **A label** above the head: `name [BT] state` and, for the Hunter, its **active branch**: the running path built from
  the tree trace each tick, for example `Root > ReactToFootsteps > SneakToNoise`.
- **Noise rings:** every noise of the last **2 s** grows to its propagation radius (in a third of that time) and fades.
  Gunfire **#FF5030**, generator and alarm **#FF30FF**, everything else (footsteps) **#FFD040**.
- A side panel with the Blackboard and an event log.

### Unity mapping

**Debug text (`IDebugInfo`).** `HunterBrain.GetDebugText()`, which `AgentRoot` publishes to the squad Blackboard every
0.5 s (the Commander's debug text lists it):

```
Hunter [BT] Investigate > Ambush
eyes: sees no, detected no, awareness 0.42, last seen 3.1 s ago
ears: Sprint 0.31 heard 2.1 s ago through 1 wall
order: none
```

- Line 1 is the running path, root excluded, so it stays short in the Commander's list: from each composite the
  child that is Running (a Selector's winner, a Sequence's current child). A branch that just finished shows only its
  name.
- Rebuilt at most once per brain tick and only when asked, like the Tactical Guard's.

**Gizmos (Scene view, agent selected) [GUESS: prototype colours, Unity shapes].**

| Gizmo | Shows | Drawn by |
| --- | --- | --- |
| Vision cone | 115° fan of the sight range (× the alert), coloured calm / suspicious / combat as above | `HunterBrain` |
| Current target | a line to the move target (dark blue walking, light blue running) and a red line to the look target while aiming | `HunterBrain` |
| Last sighting | a yellow cube where the player was last seen, while it is still searchable | `HunterBrain` |
| Route | the Hunter's waypoints, joined in route order (cyan) | `HunterBrain` |
| Noise radii | each noise heard in the last 8 s: a ring of its radius in the prototype's colour, and a second ring at the distance it could be heard through the walls counted; a line to the Hunter, green if it was remembered | `HearingSystem` |

- `HearingSystem` keeps the last **8** noises it was told about (position, radius, type, time, perceived loudness,
  walls, remembered or not) in a fixed array, so recording one allocates nothing.
- Drawing goes through a small `IGizmoPainter`: in the Editor it is Unity's `Gizmos` (which only work inside
  `OnDrawGizmos*`), and EditMode tests pass a recording painter, so every line of the drawing code runs in a test
  and what it would draw (lines, colours, boxes) is checked.
- Gizmos are drawn when the Hunter is selected (`OnDrawGizmosSelected`), so two Hunters do not clutter the scene.

---

## 17. Audio events (`audio/audio.js`)

### What the prototype does

**Everything is synthesised with WebAudio; there are no sound files.** A sound is either filtered noise (white or
brown, through a low-, high- or band-pass filter) or an oscillator tone (sine, triangle, sawtooth or square), each with
a short attack and an exponential decay. Below, "noise 1300 Hz band 0.09 s" means band-passed noise at 1300 Hz lasting
0.09 s; "tone 140 → 55 Hz" is a pitch sweep. Gains are linear (1 = full scale).

**Mixing graph.**

| Part | Values |
| --- | --- |
| Buses | one-shots → `sfx`; ambience → `amb` (gain 0.9); both → `master` (the volume setting, default **0.8**) → compressor → speakers |
| Compressor | threshold **−14 dB**, ratio **4** |
| Reverb | generated impulse **2.4 s**, decay exponent **2.6**; the `sfx` bus sends to it with wet gain **0.4 × interior** |
| Interior factor | `Level.interiorFactor`: **1** in the bunker, rising from 0 to 1 through the entrance (prototype z −48.5 → −62, the lobby and ramp), **0** outside |
| Positional sounds | equal-power panner, inverse distance model, reference distance **3 m** unless a sound sets its own (1.5–6 m), roll-off **1.1**, max distance **120 m**. The listener follows the camera |

**Ambience** (continuous, updated every frame).

| Layer | Sound | Level |
| --- | --- | --- |
| Waves | brown noise, low-pass 520 Hz, with a slow swell `0.6 + 0.4·sin(0.55 t)·sin(0.21 t + 1)` | `(0.05 + 0.32 × shore) × swell × (1 − interior)`; shore rises from 0 to 1 as the camera's prototype z goes from 40 to 100 (towards the beach) |
| Wind | white noise, band-pass 500–800 Hz (centre drifts `500 + 300·sin(0.17 t)`) | `0.045 × (1 − interior) × (0.7 + 0.3·sin(0.3 t))` |
| Forest insects | three chirps of a 3800–5400 Hz tone (0.035 s, 0.07 s apart), at a random spot within 15 m, every 0.15–1.05 s | gain 0.025, reference distance 6 m; only outdoors (interior < 0.2) and in the forest (prototype z < 85) |
| Bunker room tone | brown noise, low-pass 180 Hz | `0.06 × interior` |
| Generator hum | positional at the generator (+1.5 m): 50 Hz sine (0.5), 100 Hz sawtooth (0.12), 150 Hz sine (0.15), 300 Hz triangle (0.03), low-pass 420 Hz, 0.4 Hz wobble | `0.55 × smoothstep(0.3, 1, interior)`; reference 4 m, roll-off 1.3 |
| Alarm siren | square wave through a 900 Hz band-pass, alternating **880 / 620 Hz** at 1.6 changes per second | silent until `lights:emergency`, then `0.05 + 0.07 × interior` |

**One-shots: every sound subscribes to a game event, so no system calls audio directly.**

| Event | Sound |
| --- | --- |
| `player:footstep` | the surface's footstep (below) × **1.4** when sprinting, × **0.45** crouching, × 1 walking |
| `player:land` | the surface's footstep × **1.6** and a tone 70 → 40 Hz, 0.15 s, gain 0.3 |
| `player:jump` | noise 500 Hz band 0.12 s, gain 0.05 |
| `weapon:fired` | the weapon's gun sound (below), not positional |
| `weapon:bolt` | rifle bolt, 35 % into the 0.95 s bolt cycle (0.33 s): two clicks, 2500 Hz then 1800 Hz 0.22 s later, gain 0.15 |
| `weapon:switching` / `weapon:switched` | noise 1400 Hz band 0.08 s / 2600 Hz band 0.05 s, gain 0.08 |
| `enemy:fired` | the agent type's gun sound, positional at its muzzle |
| `hit:marker` | tone 1900 Hz (2600 Hz on a head hit) 0.05 s, gain 0.08; on a kill also a triangle 520 → 380 Hz, 0.18 s |
| `enemy:damaged` | robot (guard): a 3500 Hz band of noise and a 640 Hz triangle; humanoid: a sawtooth 150–190 → 110 Hz grunt through a 750 Hz band. Positional at 1.5 m |
| `enemy:killed` | robot: sawtooth 420 → 40 Hz over 1.4 s and six sparks; humanoid: sawtooth 170 → 70 Hz, 0.55 s, then a 60 → 40 Hz thud |
| `player:damaged` | tone 90 → 50 Hz 0.2 s (gain 0.35) and low-pass noise 600 Hz 0.15 s |
| `player:died` | triangle 220 → 40 Hz over 1.6 s |
| `bullet:whiz` | high-pass noise 2500 → 900 Hz, 0.12 s, at the end of an agent's shot that missed the player (reference 2 m) |
| `pickup:health` | three rising tones 660, 880, 1320 Hz, 0.07 s apart |
| `door:opened` / `door:closed` | hinged door: a creaking sawtooth 260 → 340 Hz (opening) or → 200 Hz (closing) and a 90 Hz thud; sliding door: a brown-noise rumble of **1.0 s** (**2.2 s** and louder for the bunker door) ending in a 70 → 50 Hz clunk |
| `door:locked` | square 110 Hz buzz through a 600 Hz low-pass, 0.3 s |
| `interact:holdtick` | tone 1500 Hz 0.02 s, gain 0.03, about 6 times a second while holding |
| `crate:scrape` | brown noise, low-pass 380 Hz, 0.5 s, gain 0.2, positional |
| `generator:disabled` | the hum's oscillators fall to 15 % of their pitch over **3.5 s** and fade; a sawtooth 300 → 30 Hz over 3.2 s and a 2000 → 100 Hz noise sweep over 2.5 s |
| `lights:emergency` | starts the alarm siren |
| `alert:broadcast` | radio squelch (1800 Hz band 0.35 s) then four square beeps 880 / 660 / 880 / 660 Hz |
| `mission:complete` | four triangle notes 523, 659, 784, 1046 Hz (C major), 0.15 s apart |
| `fx:lightpop`, `fx:spark`, `fx:casing`, `fx:impact` | a bulb pop; four tiny 4000 Hz crackles; a casing tink (3200–4700 Hz); a ping on metal only (1800–2400 Hz) |
| `ui:click`, `player:flashlight` | a 1200 Hz click; a 3000 Hz click |

**Footsteps by surface** (gain × the footstep multiplier above).

| Surface | Sound |
| --- | --- |
| Sand | low-pass noise 900 Hz, 0.16 s, gain 0.12 |
| Grass, foliage | high-pass 2500 Hz (0.07) plus 700 Hz band (0.06) |
| Dirt, gravel, rock | 1600 Hz band (0.12) plus a 90 → 60 Hz thump |
| Metal | 3000 Hz band plus ringing tones 880–960 Hz and 1370 Hz and a 110 → 70 Hz thump |
| Wood | tone 150 → 90 Hz (0.14) plus 1200 Hz band |
| Concrete (default) | 2200 Hz band (0.12) plus a 95 → 60 Hz thump |

**Guns** (`weapon.sound` for the player, the agent type for agents).

| Gun | Sound |
| --- | --- |
| SMG (`smg`) | 1300 Hz band 0.09 s (0.5), a 4000 Hz crack (0.25) and a 140 → 55 Hz thump (0.45) |
| Rifle (`sniper`) | low-pass sweep 4000 → 250 Hz over 0.55 s (0.8), crack, 90 → 35 Hz boom, and a 1.2 s tail |
| Knife (`knife`) | a 700 → 3200 Hz whoosh, 0.22 s |
| Security Patrol | 950 Hz band and a 120 → 50 Hz thump |
| Hunter | low-pass sweep 3000 → 400 Hz over 0.35 s and a 100 → 40 Hz boom |
| Tactical Guard (robot) | a 260 → 90 Hz square zap through 1800 Hz, and a band of noise |
| Other agents | 1500 Hz band and a 160 → 80 Hz thump |

### Unity mapping [GUESS]

Unity has **no audio code yet** (no `AudioSource` in any script); audio was a "Should". The prototype's rule carries
over directly: an audio component listens to events and nothing calls it. Which events exist today:

| Prototype event | In Unity now |
| --- | --- |
| `player:footstep`, `player:land`, `player:jump` | `PlayerMotor.Footstep(MovementState)`, `Landed(speed)`, `Jumped`. The surface is not known yet |
| `weapon:fired` | no event; a player shot raises a `Gunshot` noise on `Noise.asset` with the player as source |
| `weapon:switched` | `WeaponController.WeaponChanged` |
| `hit:marker` | `WeaponController.HitConfirmed(killed)` (no head-hit flag) |
| `player:damaged`, `player:died` | the player's `Health.OnDamaged`; `PlayerHealth.Died` and `PlayerDied.asset` |
| `enemy:damaged`, `enemy:killed` | each agent's `Health.OnDamaged` and `OnDied` |
| `enemy:fired` | `AgentShooter` raises a `Gunshot` noise only if its gunfire channel is set (not yet on `AgentBase`) |
| `crate:scrape`, doors opening | `Crate` and `Door` noises on `Noise.asset` |
| `generator:disabled`, `mission:complete` | `GeneratorDisabled.asset`, `MissionComplete.asset` |
| `alert:broadcast` | `Blackboard.AlertChanged` |
| `lights:emergency`, `fx:lightpop` | the World Builder's `PowerStateLighting` (their events) |
| `weapon:bolt`, `weapon:switching`, `bullet:whiz`, `pickup:health`, `door:closed`, `door:locked`, `interact:holdtick`, `fx:*`, `ui:click`, `player:flashlight` | **missing**: no event today |

- Synthesis can be kept by generating clips in an Editor tool (`AudioClip.Create` from the same recipes), or replaced
  by recorded clips; either way the numbers above are the brief.
- Positional sounds map to `AudioSource.spatialBlend = 1` with a logarithmic roll-off, minimum distance = the
  prototype's reference distance, maximum 120 m. The interior factor maps to an `AudioMixer` snapshot blend or a
  reverb zone over the bunker.
