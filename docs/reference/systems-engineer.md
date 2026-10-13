# Systems Engineer — reference extracted from the web prototype

Source: `prototype/src/player/player.js`, `prototype/src/core/input.js`, `prototype/src/core/engine.js`,
`prototype/src/core/namespace.js` and the `noise` emitters across `prototype/src`. Written at the start of
step Y2 because step Y0 had not been done yet. Sections 1–6 cover the **player** (Y2), section 7 the
**weapons** (Y3, from `weapons/weapon_system.js`, `models/weapons.js` and `world/effects.js`), and sections
8–9 **death, respawn and the HUD** (Y4, from `player/player.js`, `game/mission.js`, `ui/hud.js` and
`ui/style.css`). The door, crate and mission sections get added in Y5–Y6, each one when its step starts.

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
