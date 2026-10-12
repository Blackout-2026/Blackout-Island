# Systems Engineer — reference extracted from the web prototype

Source: `prototype/src/player/player.js`, `prototype/src/core/input.js`, `prototype/src/core/engine.js`,
`prototype/src/core/namespace.js` and the `noise` emitters across `prototype/src`. Written at the start of
step Y2 because step Y0 had not been done yet. Sections 1–6 cover the **player** (Y2) and section 7 the
**weapons** (Y3, from `weapons/weapon_system.js`, `models/weapons.js` and `world/effects.js`). The door,
crate, mission and HUD sections get added in Y4–Y6, each one when its step starts.

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
| Damage shake | `shake += amount · 0.03` (max 1), decays 2.5/s, ±shake² · 0.04 rad of 47/39 Hz jitter. Belongs to Y4 (damage) |

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
- **Not ported in Y3:** headshot and backstab multipliers (no head hitbox or "in combat" contract yet),
  sniper zoom (a "Could"), surface-specific impacts, decals, shell casings, the viewmodel camera, sway,
  sprint pose and the bolt and swing animations. Weapon audio is a "Should" for later.
