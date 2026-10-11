# Systems Engineer — reference extracted from the web prototype

Source: `prototype/src/player/player.js`, `prototype/src/core/input.js`, `prototype/src/core/engine.js`,
`prototype/src/core/namespace.js` and the `noise` emitters across `prototype/src`. Written at the start of
step Y2 because step Y0 had not been done yet. It covers the **player** only. The weapon, door, crate,
mission and HUD sections get added in Y3–Y6, each one when its step starts.

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
