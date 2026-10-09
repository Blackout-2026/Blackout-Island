# Core Developer reference (extracted from the web prototype)

Source: `prototype/` (read-only). Numbers here come from the code, not the README, wherever the two
disagree (see section 9). Files read: `README.md`, `screenshots/` (06, 07, 08, 09, 13, 04),
`src/models/generator.js`, `robot.js`, `humanoid.js`, `weapons.js`, `src/materials/materials.js` and
`textures.js` (robot paint, camo, gauge), `src/world/bunker_interior.js` (props, cure chamber),
`bunker_exterior.js` and `beach.js` (outdoor props), `src/physics/interactables.js` (cure case,
medkit), `crate.js`, `doors.js` (door visuals), `src/world/level.js` (agent counts).

Dimensions in sections 2 to 6 were **measured** by running the prototype's own model builders in Node
(three.js r128, materials stubbed, script kept outside the repo) and taking bounding boxes and
triangle counts of the resulting geometry. Anything marked **(guess)** is my judgement, not code.

## 0. Units and coordinate conversion

| Item | Prototype (three.js) | Unity (this project) |
| --- | --- | --- |
| Unit | 1 unit = 1 metre | 1 unit = 1 metre |
| Up | +Y | +Y |
| Handedness | right-handed | left-handed |
| Character facing (model local) | +Z | +Z |

The level reference (`docs/reference/world-builder.md`) converts world positions with
`Unity = (x, y, -z)`. For models I use three rules so a model looks the same as in the screenshots
**(chosen, not in the code)**:

| Model | Prototype local frame | Rule for Unity / Blender |
| --- | --- | --- |
| Generator | origin = top of the plinth, length along X, control face at **+X** | `(x, y, -z)`, the same flip as the level, so the room reads the same. Control face stays at +X. Valve wheel, beacon and cables swap sides in Z. |
| Robot, humanoids | origin = feet on the ground, face +Z | `(-x, y, z)`. The character still faces +Z, but its own left/right stay correct (prototype right arm at -X becomes +X). The robot's weapon pod is on its **right** arm. |
| Weapon viewmodels | camera space: +X right, +Y up, **-Z forward** | `(x, y, -z)`: forward becomes +Z, right stays +X. |

Joint and part names below are the prototype's bone names (`thighL`, `shinR`, ...). The Agent Controller
needs these names for procedural animation (step C3 writes the final list into `docs/art-pipeline.md`).

## 1. Model overview

Triangle counts are the prototype's primitive geometry as built (non-indexed, so a face is 2 triangles
for a box). They show the **detail level to match**, not a budget; step C1 proposes the real budgets.

| Model | Count in level | Role in this project | Prototype tris (measured) | Height x width x depth (m) |
| --- | --- | --- | --- | --- |
| Generator "G-1" | 1 | **Hero model 1** | 8,840 (+1,080 floor cables) | 4.10 x 6.96 x 2.90 (see 2.2) |
| Security robot (Tactical Guard) | 2 (TG-01, TG-02) | **Hero model 2** | 3,216 | 2.38 x 1.07 x 0.64 |
| Security Patrol (humanoid) | 3 | character | 2,892 | 1.89 x 0.53 x 1.10 (with rifle) |
| Hunter (humanoid, ghillie) | 2 | character | 3,112 | 1.90 x 0.57 x 1.15 |
| Commander (humanoid, lab coat) | 1 | character | 2,612 | 1.89 x 0.53 x 0.66 |
| SMG viewmodel + hands | 1 | first person | 1,048 | 0.65 x 0.53 x 0.77 |
| Sniper viewmodel + hands | 1 | first person | 1,700 | 0.65 x 0.55 x 1.29 |
| Knife viewmodel + hand | 1 | first person | 532 | 0.32 x 0.58 x 0.66 |
| Cure case | 1 | pickup | about 250 **(guess)** | 0.28 x 0.42 x 0.24 |
| Medkit | 3 | pickup | about 150 **(guess)** | 0.26 x 0.42 x 0.28 |

Visual style in every screenshot: low-poly, chamfered boxes and cylinders, flat painted colours with a
subtle noise texture, strong emissive accents (cyan, amber, red). It is not photoreal. A Blender model with
clean quads and bevelled edges at roughly the same triangle count will look right.

## 2. Hero model 1: generator "G-1" (3.2 MW diesel generator)

Screenshots: `06_generator_before_blackout`, `07_hero_generator`, `11_generator_after_blackout`.

### 2.1 Silhouette

A long, low locomotive-like machine lying along X. From the left (-X) to the right (+X):

1. A tall, boxy **diesel engine block** (olive green, about 2.7 m long, 1.5 m tall) with a row of 6
   cylinder heads on top and cooling fins along both sides.
2. A **spinning flywheel disc** and a yellow/black **belt guard** at the far -X end.
3. A **round alternator housing** (1.05 m radius, cylinder along X) whose middle section is a
   ribbed glass tube showing a glowing cyan **energy core**. This is the visual centre of the model.
4. A flat **control face** at the +X end with three round gauges, a row of LEDs and a red valve wheel.

On top: a thick rusty **exhaust stack** rising from the middle (it ends at the room's ceiling pipe), a small
amber **beacon** over the engine, an air-filter canister. Along the sides: coolant pipes on the +Z side,
the exhaust runner pipe and copper stubs on the -Z side. Power cables leave the machine on the -Z side and
run across the floor.

### 2.2 Dimensions (front, side, top)

Local origin is the top of the plinth. Machine only, floor cables excluded.

| View | What you see | Extent | Size (m) |
| --- | --- | --- | --- |
| **Front** (looking down -Z at the long side: X by Y) | engine on the left, glass housing right, stack on top | x -3.53 ... +3.43, y 0 ... 4.10 | **6.96 long x 4.10 tall** |
| **Side** (looking down X at the control face: Z by Y) | control panel, 3 gauges, wheel; stack behind it | z -1.45 ... +1.45, y 0 ... 4.10 | **2.90 wide x 4.10 tall** (the round housing is 2.10 wide and 2.10 tall, y 0.20 ... 2.30) |
| **Top** (looking down: X by Z) | skid rectangle with the engine, then the round housing | x -3.53 ... +3.43, z -1.45 ... +1.45 | **6.96 x 2.90** |

Key sub-dimensions:

| Part | Size (m) |
| --- | --- |
| Skid frame | 6.4 x 0.22 x 2.9 (two side rails, deck plate, 5 cross beams) |
| Engine block | 2.7 (x) x 1.5 (y) x 1.9 (z), centred at x -1.7, y 1.0 (spans y 0.25 ... 1.75) |
| Alternator housing | radius 1.05, axis along X at y 1.25, from x 0.2 to 3.0 (2.8 long) |
| Glass section | 1.7 long, radius 1.02 |
| Energy core | radius 0.62, 1.6 long; chrome rod radius 0.16, 1.9 long |
| Exhaust stack | radius 0.30 at the bottom, 0.24 at the top, 1.8 tall, centred x 1.5, spans y 2.3 ... 4.1 |
| Control panel | 0.3 (x) x 1.5 (y) x 1.6 (z), face at x 3.1, centred y 1.35 |
| Flywheel | radius 0.62, 0.14 thick, at x -3.45, y 1.0 |
| Beacon | radius 0.10, 0.18 tall, at (-0.6, 2.55, -0.5) |

The room code puts a box collider of 6.4 x 3.6 x 3.2 m around the generator (x +/-3.2, z +/-1.6). Use the
same as the collider proxy.

### 2.3 Parts list

Positions are prototype local coordinates (`x` along the machine, `y` up, `z` across). Apply the rule
in section 0 when building. "tris" is not given per part; the whole model is 8,840.

| # | Part | Count | Shape and size (m) | Position | Material |
| --- | --- | --- | --- | --- | --- |
| 1 | Side rails | 2 | boxes 6.4 x 0.12 x 0.2 | y 0 ... 0.12, z +/-1.35 | metalDark |
| 2 | Deck plate | 1 | box 6.4 x 0.1 x 2.8 | y 0.12 ... 0.22 | metalDark |
| 3 | Cross beams | 5 | boxes 0.16 x 0.22 x 2.9 | x = -2.8, -1.4, 0, 1.4, 2.8 | metalDark |
| 4 | Engine block | 1 | rounded box 2.7 x 1.5 x 1.9, radius 0.18 | (-1.7, 1.0, 0) | olive paint |
| 5 | Cooling fins | 28 (14 per side) | thin plates 0.04 x 1.1 x 0.16 | x from -3.0 step 0.19, both z faces | metalDark |
| 6 | Cylinder heads | 6 | rounded boxes 0.36 x 0.28 x 0.9 | x -2.75 ... -0.65 step 0.42, y 1.9 | metalDark |
| 7 | Rocker caps | 12 | cylinders r 0.05, h 0.12 | y 2.08, z +/-0.25, above each head | chrome |
| 8 | Intake manifold | 1 | cylinder r 0.11, 2.6 long along X | (-1.7, 1.8, 0.62) | steel |
| 9 | Exhaust runners | 6 | cylinders r 0.06, 0.4 long, tilted 0.9 rad | y 1.75, z -0.65, under each head | copper |
| 10 | Exhaust pipe | 1 | cylinder r 0.14, 2.6 long along X | (-1.7, 1.6, -0.88) | rust |
| 11 | Air filter | 1 | cylinder r 0.32, h 0.6 + lid r 0.34, h 0.06 | (-0.6, 2.05, 0.55) | metalDark, chrome lid |
| 12 | Belt guard | 1 | rounded box 0.3 x 1.4 x 1.2, radius 0.15 | (-3.25, 1.1, 0) | hazard (yellow/black stripes) |
| 13 | Flywheel | 1 | disc r 0.62, 0.14 thick, 6 holes r 0.08 on a 0.38 circle | (-3.45, 1.0, 0) | steel disc, metalDark holes |
| 14 | Coupling | 1 | cylinder r 0.6, 0.6 long along X | (-0.1, 1.15, 0) | metalDark |
| 15 | Housing end sections | 2 | solid cylinders r 1.05, 0.55 long | x 0.2 ... 0.75 and 2.45 ... 3.0, y 1.25 | olive paint |
| 16 | Housing ribs | 6 | tori R 1.05, tube 0.05 | evenly across x 0.75 ... 2.45 | metalDark |
| 17 | Housing bars | 6 | boxes 0.06 x 0.06 x 1.7 around the circumference | x centre 1.6 | metalDark |
| 18 | Support saddles | 2 | boxes 0.3 x 0.38 x 1.8 | x 0.6 and 2.6, y 0.22 ... 0.6 | metalDark |
| 19 | Glass tube | 1 | open cylinder r 1.02, 1.7 long | (1.6, 1.25, 0) | glass (12 % opacity, tint #9fc9d0) |
| 20 | Energy core | 1 | cylinder r 0.62, 1.6 long | (1.6, 1.25, 0) | **emissive core shader** (see 2.5) |
| 21 | Core rod | 1 | cylinder r 0.16, 1.9 long | (1.6, 1.25, 0) | chrome |
| 22 | Exhaust stack | 1 | cylinder r 0.24 / 0.30, 1.8 tall + flange r 0.34, h 0.14 | (1.5, 3.2, 0), flange y 2.35 | rust, metalDark |
| 23 | Control panel | 1 | rounded box 0.3 x 1.5 x 1.6, radius 0.08 | (3.10, 1.35, 0) | metalDark |
| 24 | Gauge faces | 3 | discs r 0.174 with chrome bezel torus (tube 0.025) | x 3.26, y 1.65, z -0.45 / 0 / +0.45 | gauge decal, chrome |
| 25 | Gauge needles | 3 | thin boxes 0.014 x 0.14 x 0.004 on a pivot at the dial centre | same as gauges | red #d02010 |
| 26 | Indicator LEDs | 5 | spheres r 0.03 | x 3.27, y 1.05, z -0.5 ... +0.5 step 0.25 | emissive green (4), amber (1, the 4th) |
| 27 | Valve wheel | 1 | torus R 0.22, tube 0.03 + 3 spokes 0.42 long; stem r 0.05, 0.3 long | (3.40, 0.75, 0.35) | pipeRed, steel stem |
| 28 | Coolant pipes | 2 runs + 2 risers | cylinders r 0.07 and r 0.05 along X, 5.6 long | y 0.5 and 0.75, z 1.15 and 1.2 | pipe (grey-green), pipeYellow |
| 29 | Side valves | 3 | cylinders r 0.1, 0.16 long + red torus handle R 0.12 | x -1.6, 0.9, 2.2; y 0.5, z 1.25 | steel, pipeRed |
| 30 | Power cables | 3 | tubes r 0.05 from the machine to the floor, then 2 to 3 m along it | start (2.6, 0.7, -0.9), (2.4, 0.7, -0.95), (0.8, 0.5, -1.0) | cable (black) |
| 31 | Beacon | 1 | cylinder r 0.10, 0.18 tall + chrome reflector + base r 0.12, 0.06 | (-0.6, 2.55, -0.5) | amber emissive, chrome, metalDark |
| 32 | Label decals | 4 | planes: "DANGER 11 kV" 0.9 x 0.35 on the housing; "G-1 // 3.2 MW" 0.9 x 0.35 on both engine sides; "MAIN CONTROL" 0.7 x 0.18 above the gauges | housing z +1.07; engine z +/-0.97; panel (3.26, 2.0, 0) | decals, see colours below |

### 2.4 Materials and colours

| Material | Colour | Metal / rough (prototype) | Where |
| --- | --- | --- | --- |
| Olive paint | #5f6a4a, metal-plate texture | 0.4 / 1.0 | engine block, housing ends |
| metalDark | #2b2e31 | 0.7 / 0.45 | frame, heads, fins, ribs, saddles, panel |
| Steel | #9aa0a4 | 0.9 / 0.3 | manifold, flywheel, valve stem |
| Chrome | #d9dde0 | 1.0 / 0.12 | rod, bezels, caps |
| Copper | #b06a3c | 0.9 / 0.35 | exhaust runners |
| Rust | rust texture (orange-brown) | 0.3 / 1.0 | exhaust pipe, stack |
| Hazard | yellow/black diagonal stripe texture | 0 / 0.75 | belt guard |
| Pipe / pipeRed / pipeYellow | #5d6a62 / #7a2a20 / #a27b1d | 0.6 / 0.5, 0.4 / 0.55, 0.4 / 0.5 | plumbing, valve wheel |
| Cable | #121212 | 0.1 / 0.6 | cables |
| Glass | #9fc9d0, opacity 0.12, double sided | 0.2 / 0.06 | housing tube |
| Gauge face | cream #d9d4c3 dial, arcs green #2e8b3a, amber #d9a521, red #b3241b | 0 / 0.4 | 3 gauges |
| Label yellow / navy / black | #e8b52a on black text; #1d2633 with #e8e8e0 text; #111 with #d9a521 text | 0 / 0.6 | decals |

Unity URP/Lit: smoothness = 1 - roughness; use the same metalness.

### 2.5 Emissive and animated parts

| Part | Emissive | Running | After `generator:disabled` |
| --- | --- | --- | --- |
| Energy core | cyan-white shader: colour blends (0.05, 0.35, 0.55) to (0.5, 1.4, 1.8), bright rim, vertical bands scrolling; intensity = level x 2.2 | level 0.92 +/- 0.08, a fast flicker | flickers 1.6 s (random 0.15 or 1.1 level, fading) then 0 (dark) |
| Indicator LEDs | green (0.3, 3.0, 0.5) | on | near off (0.02, 0.05, 0.02) |
| Warning LED (4th) | amber (3.0, 1.6, 0.2) | slow blink (4 Hz square) | fast blink (10 Hz), stays on as a fault |
| Beacon | amber #ffa020, intensity 2.5 | rotates 5 rad/s about Y | intensity 0.05, stops |
| Gauges | none | needles hover at 0.35 / 0.47 / 0.59 of the dial with jitter | needles fall to zero (about 2.5 s) |
| Flywheel | none | spins 9 rad/s about X | slows to 0 (about 5 s) |

Other effects on disable: 60 + 50 sparks at the control face and the stack, a smoke plume, then
intermittent spark bursts every 0.4 to 2.0 s at (2.9, 0.6, -1.2) and a steady smoke emitter at
(-1.7, 2.0, 0). The Unity prefab needs empty child transforms for these (step C2 already lists
`InteractPoint`, `SparkVFX`, `HumAudioSource`).

Moving parts (need separate objects with the pivot at the rotation centre): **flywheel** (pivot at its
centre, axis X), **beacon** head (axis Y), **three gauge needles** (pivot at the dial centre, axis along X,
sweep 270 degrees from -135 to +135), **valve wheel** (static in the prototype; spin is optional).

### 2.6 Detail level

High for this game: 8,840 triangles (1,080 more in the floor cables). Everything is chamfered, nothing is a bare box.
The detail that reads in the screenshots at 3 to 8 m: the **fins**, the **6 cylinder heads**, the **ribbed
glass tube with the glowing core**, the **gauges**, the **stack** and the **hazard belt guard**. Items that
could be dropped or baked into the texture if the budget is tight **(guess)**: rocker caps, side valves,
LEDs (can be emissive quads), the cables.

## 3. Hero model 2: security robot (Tactical Guard)

Screenshots: `08_hero_robot`, `09_enemy_fight` (human agents only), `13_lab`.

### 3.1 Silhouette

A **bipedal armoured robot, about 2.1 m to the top of the helmet** (2.38 m with the antenna), broad
shoulders, narrow waist. Key shape points:

- **Digitigrade (reverse-knee) legs**: the thigh slopes forward, the knee bends backward, the shin angles
  down and back, ending in a **clawed three-toe foot** (two toes forward, one heel spur back).
- **Segmented waist**: three stacked rings between pelvis and chest, alternating paint and dark joint.
- **Wide armoured chest** with a raised front plate, yellow hazard bars on the front, a vent grille,
  and a **back power pack** with three glowing cyan cell bars.
- **Head**: a rounded helmet, a dark visor slot with a thin **red emissive eye bar**, a yellow
  roof plate, a whip **antenna** with a red tip, and a small red **scanner dot** that sweeps across the visor.
- **Asymmetric arms**: the left arm ends in a three-finger **claw hand**; the right forearm is replaced
  by a **twin-barrel weapon pod** with an ammo drum on its outer side. Both shoulders have a plate and a
  piston along the upper arm.

### 3.2 Dimensions (front, side, top)

Rest (bind) pose, standing, feet on the floor. Height is to the helmet roof plate (2.13 m) and to the
antenna tip (2.38 m).

| View | Extent | Size (m) |
| --- | --- | --- |
| **Front** (X by Y) | x -0.53 ... +0.53, y 0.01 ... 2.38 | **1.07 wide x 2.38 tall** (2.13 to the helmet) |
| **Side** (Z by Y; +Z is front) | z -0.38 ... +0.26, y 0.01 ... 2.38 | **0.64 deep x 2.38 tall** (back pack at the rear, toes at the front) |
| **Top** (X by Z) | x -0.53 ... +0.53, z -0.38 ... +0.26 | **1.07 x 0.64** |

Joint heights (rest pose, Y above floor) and offsets, for placing pivots:

| Joint | Height | Notes |
| --- | --- | --- |
| Pelvis (root) | 1.05 | centre of the model; lowered 0.04 while walking |
| Spine | 1.19 | 0.14 above the pelvis |
| Chest | 1.49 | 0.30 above the spine |
| Head | 1.93 | 0.44 above the chest, 0.02 forward |
| Shoulder (upperArm L/R) | 1.76 | x = +/-0.37 from the centre line |
| Elbow (foreArm) | 1.42 | 0.34 below the shoulder |
| Wrist (hand) | 1.10 | 0.32 below the elbow |
| Hip (thigh L/R) | 0.99 | x = +/-0.17 |
| Knee (shin) | 0.57 | 0.42 below the hip and **0.13 forward** |
| Ankle (foot) | 0.14 | 0.43 below the knee and **0.15 back** (so the foot sits 0.02 behind the hip) |

Body block sizes (X x Y x Z, metres):

| Part | Size | Centre height |
| --- | --- | --- |
| Pelvis | 0.46 x 0.20 x 0.32, top cap 0.36 x 0.08 x 0.34 | 1.05 |
| Waist rings (3) | 0.34 / 0.32 / 0.30 wide x 0.085 x 0.26 | 1.21, 1.31, 1.41 |
| Chest | 0.62 x 0.44 x 0.40 | 1.63 |
| Chest front plate | 0.50 x 0.22 x 0.06, on the front face | 1.69 |
| Back pack | 0.42 x 0.38 x 0.18, 0.27 behind the chest centre | 1.63 |
| Head (helmet) | 0.27 x 0.22 x 0.27 | 1.99 |
| Visor slot | 0.24 x 0.07 x 0.04, front of the helmet | 2.00 |
| Eye bar | 0.17 x 0.022 x 0.02 | 2.00 |
| Thigh | 0.17 x 0.40 x 0.20, tilted forward 17 degrees | 0.8 |
| Shin | 0.13 x 0.42 x 0.13, tilted 19 degrees | 0.35 |
| Foot | 0.18 x 0.06 x 0.26 plus two 0.05 x 0.04 x 0.12 toes and a 0.05 x 0.04 x 0.10 heel spur | 0.05 |
| Upper arm | tapered limb r 0.065 to 0.055, 0.32 long, shoulder plate 0.22 x 0.14 x 0.26 | 1.76 down to 1.44 |
| Left forearm | 0.13 x 0.30 x 0.13 + claw: palm 0.10 x 0.06 x 0.08, 3 fingers 0.025 x 0.11 x 0.025 | 1.42 down to 1.10 |
| Right forearm (weapon pod) | 0.16 x 0.36 x 0.17 + 2 barrels r 0.022, 0.34 long + drum r 0.075, 0.1 long | 1.42 down to 0.79 |

### 3.3 Parts list by group

Counts are from the code; the whole robot is 3,216 triangles in 2 material slots.

| Group | Parts |
| --- | --- |
| Pelvis | 1 rounded box, 1 cap plate, 2 hip actuator cylinders (r 0.085, 0.1 long, on the sides) |
| Waist | 3 stacked rounded boxes |
| Chest | main plate, front plate, 2 vertical hazard bars (0.06 x 0.32 x 0.03), 1 horizontal hazard bar (0.32 x 0.05 x 0.03), vent grille of 5 slats (0.22 x 0.015 x 0.02), back pack, 3 emissive cell bars (0.06 x 0.24 x 0.02), exhaust stub (r 0.05, 0.18 tall), neck cylinder (r 0.07 / 0.09, 0.12 tall) |
| Head | helmet, visor slot, eye bar (emissive), roof plate (0.20 x 0.035 x 0.24, yellow), antenna (r 0.008, 0.26 tall) with a red tip sphere (r 0.018), scanner dot (r 0.016, separate small mesh) |
| Each arm (x2) | shoulder plate, yellow stripe on it, shoulder ball (r 0.08), upper-arm limb, piston housing (r 0.02, 0.18) and rod (r 0.011, 0.16), elbow cylinder (r 0.07, 0.12 wide) |
| Left forearm and hand | forearm block, palm, 3 claw fingers |
| Right forearm and hand | pod block, yellow strip, ammo drum, 2 barrels, barrel mount block |
| Each leg (x2) | hip ball (r 0.09), thigh armour, yellow front stripe (0.04 x 0.3 x 0.03), knee cylinder (r 0.08, 0.18 wide), dark shin core, front shin plate (0.14 x 0.2 x 0.06), knee piston housing (r 0.028, 0.24) on the thigh and rod (r 0.014, 0.28) on the shin, ankle cylinder (r 0.055, 0.14), foot plate, 2 toes, heel spur |

Sockets the Unity prefab needs: **MuzzlePoint** at the end of the right hand, 0.33 below the wrist pivot
(`handR`, local y -0.33), centred between the two barrels; **WeaponSocket** at the right hand.

### 3.4 Materials and colours

The robot is **one mesh with vertex colours** in two material slots. For Blender, use two or three
materials plus a shared texture instead of vertex colours.

| Slot / colour | Value | Where |
| --- | --- | --- |
| Body paint | white vertex colour x robotPaint texture: grey-blue base #3a3f44 to #4f565c, paint chips #8e9396, grime #23221f; metal 0.55, rough 0.6 | pelvis cap, waist (alternate rings), chest, head shell, shoulders, thighs, forearms, foot plates |
| Accent (hazard yellow-ochre) | #c89a24 | chest bars, head roof plate, shoulder stripes, thigh stripes, forearm strip |
| Joint (dark metal) | #232527 | pelvis, alternate waist rings, power pack, neck, shoulder balls, knee/ankle cylinders, shin core, toes, hand palm |
| Dark | #16181a | vent slats, visor slot, pistons, barrels |
| Steel | #9aa0a4 | hip actuators, upper-arm limbs, piston rods, exhaust stub, antenna |
| Emissive red | (5.0, 0.35, 0.15) | eye bar, antenna tip |
| Emissive cyan | (0.4, 2.6, 3.0) | the 3 power-cell bars on the back |
| Scanner dot | red-orange emissive, changes with state (amber (5, 2.4, 0.2) idle, red (6, 0.4, 0.2) in combat or on alert; the eye bar itself stays red) | small sphere, moves +/-0.07 across the visor |

### 3.5 Emissive and moving parts

Emissive: eye bar, antenna tip, 3 back cells, scanner dot. Everything else is lit normally.

Moving parts (each needs its own mesh with the pivot at the joint, the Agent Controller drives them):

| Part | Pivot | Range seen in the prototype animation |
| --- | --- | --- |
| Pelvis | root, 1.05 | bobs 0.03 to 0.05 while walking, twists +/-0.06 rad; falls 0.8 on death |
| Spine, chest | at their joints | lean forward 0.06 rad running; chest pitch to aim |
| Head | head bone | yaw +/-1.0 rad (look), pitch with aim |
| Upper arms | shoulder | swing +/-0.25 rad walking; **right arm raises to -1.5 rad** to aim the pod |
| Forearms | elbow | left bends to -1.1 rad when aiming; right straightens to -0.08 |
| Thighs | hip | swing +/-0.32 rad walking, 0.54 running |
| Shins | knee | bend up to 0.45 rad |
| Feet | ankle | counter-rotate 0.7 x (thigh + shin) so the sole stays level |
| Pistons | housing on the thigh/upper arm, rod on the shin/forearm | visually telescoping; static in the prototype (the rod simply follows its bone) |
| Scanner dot | head | slides side to side, 2.5 rad/s idle, 9 rad/s aiming |

Timings, gait phase and thresholds belong in `docs/reference/agent-controller.md`.

### 3.6 Detail level

Medium-high: 3,216 triangles, 16 bones, the most complex character in the game. The parts that read in
the screenshot at 5 to 10 m: the **bulky chest and shoulders**, the **reverse-knee legs**, the **glowing
red eye bar**, the **yellow accents** and the **back pack**. The small items (finger claws, antenna,
vent slats, pistons) read only up close and are the first to simplify.

## 4. Humanoid characters (Security Patrol, Hunter, Commander)

Screenshots: `04_security_office` (patrol, far right), `09_enemy_fight` (patrol at the door), `13_lab`
(commander silhouette). All three share one skeleton and one body; they differ in clothing, head gear and
weapon. Counts in the level: Security Patrol x3, Hunter x2, Commander x1.

### 4.1 Shared body and proportions

Standing, feet on the floor, facing +Z. Total height 1.87 m to the top of the head or helmet. Each
character is one mesh with vertex colours (about 2.6k to 3.1k triangles including the weapon).

| Joint | Height (m) | Offset from parent |
| --- | --- | --- |
| Hips (root) | 0.98 | |
| Spine | 1.08 | 0.10 above hips |
| Chest | 1.28 | 0.20 above spine |
| Neck | 1.55 | 0.27 above chest |
| Head | 1.65 | 0.10 above neck |
| Shoulder (upperArm L/R) | 1.48 | x = +/-0.20, 0.20 above chest |
| Elbow (foreArm) | 1.19 | 0.29 below the shoulder |
| Wrist (hand) | 0.93 | 0.26 below the elbow |
| Hip joint (thigh L/R) | 0.92 | x = +/-0.10 |
| Knee (shin) | 0.48 | 0.44 below the hip |
| Ankle (foot) | 0.06 | 0.42 below the knee |
| Weapon socket (gun) | 1.40 | on the chest, at x -0.04, z +0.20 (in front of the chest) |

Body shapes (the same on every human):

| Part | Shape and size (m) |
| --- | --- |
| Hips | rounded box 0.34 x 0.20 x 0.22 |
| Lower torso | rounded box 0.31 x 0.24 x 0.20, centred 1.18 |
| Chest | rounded box 0.38 x 0.32 x 0.24, centred 1.40; 2 shoulder spheres r 0.075 at x +/-0.19 |
| Neck | cylinder r 0.05 / 0.055, 0.12 tall |
| Head | sphere r 0.105 squashed to 0.92 wide x 1.12 tall, centred 1.72; jaw box 0.13 x 0.07 x 0.12; nose box 0.03 x 0.04 x 0.04; 2 ear boxes 0.02 x 0.045 x 0.03 |
| Arm | upper arm tapered limb r 0.058 to 0.048, 0.30 long; elbow sphere r 0.05; forearm tapered limb r 0.048 to 0.036, 0.26 long |
| Hand | box 0.075 x 0.10 x 0.04 plus a thumb box 0.03 x 0.06 x 0.03 |
| Leg | thigh tapered limb r 0.088 to 0.066, 0.45 long; knee sphere r 0.065; shin tapered limb r 0.064 to 0.048, 0.42 long |
| Foot | boot box 0.11 x 0.09 x 0.27 (toe forward) with a shaft cylinder r 0.058 / 0.062, 0.14 tall |

The character is 0.53 to 0.57 m wide. The weapon adds about 0.7 m in front (1.10 to 1.15 m total depth with
a rifle, 0.66 m for the commander's pistol).

Moving parts (pivots at the joints above; the Agent Controller drives them):
`hips`, `spine`, `chest`, `neck`, `head`, `upperArmL/R`, `foreArmL/R`, `handL/R`, `thighL/R`, `shinL/R`,
`footL/R`, `gun`. That is 16 bones. Ranges seen in the prototype animation: thigh swing about +/-0.42 rad
walking, knee bend up to 0.55 to 0.75 rad, crouch lowers the hips 0.28 m, neck yaw +/-0.9 rad, the weapon
bone moves from low-ready to shouldered, and the death fall rotates the hips -1.35 rad (drops 0.82 m).

Material for all three: vertex colours multiplied by a fine fabric detail texture, roughness 0.85,
metalness 0.05. For Blender, use one material per clothing colour or one atlas.

### 4.2 Security Patrol

Silhouette: a guard in a **dark navy uniform with a tactical vest and a rounded helmet with a dark visor**,
carrying an **assault rifle**. This is the standard enemy in the compound and the security office.

| Part | Shape and size (m) | Colour |
| --- | --- | --- |
| Uniform (torso, sleeves, legs) | the shared body | navy #2b3546 |
| Vest | rounded box 0.41 x 0.30 x 0.27 over the chest | #2a2d28 |
| Pouches | 3 rounded boxes 0.09 x 0.11 x 0.06 across the front | #1a1b1d |
| Radio | box 0.06 x 0.10 x 0.04, on the shoulder side (x +0.16) | #1a1b1d |
| Belt | cylinder r 0.18, 0.06 tall, at the hips | #1a1b1d |
| Helmet | sphere r 0.128 squashed to 0.9 tall, plus a brim cylinder r 0.135 / 0.14, 0.03 tall | #262b31 |
| Visor | rounded box 0.19 x 0.06 x 0.06, front of the helmet | #0b0d10 |
| Knee pads | 2 rounded boxes 0.10 x 0.10 x 0.05 | #1a1b1d |
| Gloves / boots / skin | | #1c1d1e / #141210 / #b08368 |
| Rifle | see 4.5 | #2a2c2f and #161718 |

No emissive parts. The top of the helmet is at 1.87 m.

### 4.3 Hunter

Silhouette: a hooded **camouflaged sniper in a ghillie suit**, shaggy outline with hanging leaf strips, carrying a
**scoped rifle**. It lives in the forest, so the outline must break up against trees.

| Part | Shape and size (m) | Colour |
| --- | --- | --- |
| Suit (torso, sleeves, legs) | the shared body, white vertex colour so the camo texture shows | camo texture: base #3d4127, patches #5c5a37, #2a2a1c, #6b5a3d |
| Hood | sphere r 0.13 (1.05 tall), set slightly back | #d9d9c8 (tints the camo light) |
| Face wrap | rounded box 0.15 x 0.08 x 0.12 | #4c5536 |
| Ghillie strips | **54** thin boxes (6 groups of 9): 0.05 to 0.09 wide, 0.12 to 0.32 long, 0.008 thick, hanging from the head, chest, spine, both upper arms and hips, tilted randomly | leaf greens #55643a, #6b6644, #3b4a2a (cycling) |
| Gloves / boots | | #4c5536 / #2a2620 |
| Skin | | #6e5040 (darker, face painted) |
| Rifle | see 4.5, with a scope tube r 0.024, 0.2 long above the receiver | receiver, handguard and stock #4c5536; barrel, magazine, grip and scope #161718 |

No emissive parts. The strips use a fixed random seed in the prototype, so the pattern is the same every run;
do the same (hand-placed or a fixed seed).

### 4.4 Commander

Silhouette: an **unarmoured scientist-administrator**: a long white lab coat, dark trousers, glasses,
a headset and a **small pistol**. Easy to tell apart from the soldiers: no helmet, no vest, light colours.

| Part | Shape and size (m) | Colour |
| --- | --- | --- |
| Coat (torso, sleeves) | the shared body | white #e9ebe7 |
| Coat tails | open cylinder r 0.20 / 0.25, 0.5 tall, covering 317 degrees (open at the front), hanging from the hips | #e9ebe7 |
| Shirt | box 0.10 x 0.22 x 0.02 on the chest front | #3c4b5c |
| Tie | box 0.035 x 0.18 x 0.01 | #7a1d1d |
| Badge | box 0.06 x 0.07 x 0.01, left chest | #1d2633 |
| Trousers | the shared legs | #24272c |
| Hair | sphere r 0.11, 0.75 tall, on top of the head | #2a2018 |
| Glasses | bar 0.13 x 0.025 x 0.01 across the eyes; a half torus r 0.115 as the temples | #111111 / #1a1b1d |
| Headset | 2 ear cylinders r 0.035, 0.03 thick; boom cylinder r 0.005, 0.12 long | #1a1b1d |
| Hands / boots / skin | | skin #b08368 (bare hands) / #1a1b1d / #b08368 |
| Pistol | slide box 0.035 x 0.05 x 0.17; grip box 0.032 x 0.09 x 0.045, tilted | #1a1b1d |

The pistol muzzle is 0.22 m ahead of the gun socket. The commander holds the pistol low and points it forward
when aiming (right arm raised about 1.45 rad).

### 4.5 Rifle (Security Patrol and Hunter)

About 1.03 m long from the stock to the muzzle, attached to the `gun` bone, forward = +Z:
receiver box 0.06 x 0.09 x 0.42; barrel cylinder r 0.016, 0.40 long (muzzle at z +0.74 from the socket);
handguard box 0.05 x 0.05 x 0.22; magazine box 0.045 x 0.16 x 0.07 tilted 0.25 rad; pistol grip box
0.045 x 0.10 x 0.05 tilted -0.3 rad; stock box 0.05 x 0.08 x 0.22; a small front sight box 0.02 x 0.03 x 0.05
(replaced by the scope on the Hunter's). A **MuzzlePoint** empty at the barrel tip is needed.

### 4.6 Detail level

Low to medium. Boxes, spheres and cylinders with 8 to 14 sides, about 2.6k to 3.1k triangles with the
weapon. No fingers (one block hand), no face detail beyond jaw, nose and ears. The characters are small on
screen (10 to 30 m away in most fights), so silhouette and colour matter more than detail.

## 5. First-person weapon viewmodels

Screenshots: the SMG is in the lower right of every screenshot; `07_hero_generator` shows it closest.
In-game names: **MP-9 SMG**, **R-700 RIFLE** (the sniper), **COMBAT KNIFE**. Each model includes the player's
**gloved hands and dark sleeves** (they are part of the model). Weapon stats are in
`docs/reference/systems-engineer.md`.

Camera space in the prototype: +X right, +Y up, **-Z forward** (see section 0 for the Unity flip). The weapon
holder is scaled by 0.85 in game, so the sizes below are before that scale. Positions are where the weapon
sits relative to the camera when held.

### 5.1 SMG (MP-9)

Held position (0.13, -0.125, -0.45), turned -0.04 rad. Total 0.65 x 0.53 x 0.77 m with the hands
(the gun alone is about 0.56 m long). Muzzle at z -0.33 in the gun's own frame.

| Part | Size (m) | Material |
| --- | --- | --- |
| Receiver | rounded box 0.05 x 0.068 x 0.27 | metal #2e3135 (metal 0.85, rough 0.38) |
| Barrel shroud / barrel / muzzle tip | cylinders r 0.02 x 0.13, r 0.012 x 0.06, r 0.016 x 0.03, in line | polymer, metal, metal |
| Magazine | box 0.032 x 0.16 x 0.048, tilted 0.12 rad forward | polymer #1b1d1f (rough 0.6) |
| Pistol grip | box 0.034 x 0.095 x 0.044, tilted -0.3 rad | polymer |
| Foregrip | box 0.028 x 0.05 x 0.03 | polymer |
| Top rail | box 0.03 x 0.012 x 0.22 | metal |
| Red-dot sight | body 0.04 x 0.035 x 0.05; lens plane 0.032 x 0.028; dot sphere r 0.0035 | metal; dark blue-green lens (0.05, 0.12, 0.16); **emissive red dot** (8, 0.4, 0.2) |
| Charging handle | box 0.012 x 0.012 x 0.03 | steel #b8bec2 (metal 1.0, rough 0.18) |
| Folded stock | 2 wire rods 0.008 x 0.008 x 0.18 along the right side + a small plate | metal, polymer |
| Hands | two gloves (rounded box 0.055 x 0.035 x 0.10, finger box, wrist cylinder r 0.032 / 0.036, 0.12 long) on the grip and foregrip, each with a sleeve cylinder r 0.042 / 0.05, 0.35 to 0.40 long reaching down and back | glove #2b2c2a, sleeve #3a4030 (rough 0.9 to 1.0, fabric texture) |

### 5.2 Sniper rifle (R-700)

Held position (0.12, -0.12, -0.46), turned -0.04 rad. Total 0.65 x 0.55 x 1.29 m with the hands; about
1.23 m from butt to muzzle. Muzzle at z -0.81 in the gun's frame.

| Part | Size (m) | Material |
| --- | --- | --- |
| Receiver | rounded box 0.05 x 0.06 x 0.30 | metal |
| Barrel | tapered cylinder r 0.013 to 0.016, 0.62 long, at z -0.45; muzzle brake cylinder r 0.02 x 0.06 at z -0.77 | metal |
| Stock | body rounded box 0.055 x 0.075 x 0.48; butt rounded box 0.05 x 0.11 x 0.18 at z +0.33; cheek rest box 0.04 x 0.03 x 0.12 | tan #6b604a (rough 0.7), polymer |
| Magazine | box 0.03 x 0.08 x 0.06 | polymer |
| Scope | tube cylinder r 0.024 x 0.24 at y +0.075; objective bell r 0.034 / 0.024 x 0.06 at z -0.17; eyepiece r 0.024 / 0.03 x 0.05 at z +0.12; front lens disc r 0.031; 2 mounting rings 0.03 x 0.035 x 0.02; turret cylinder r 0.012 x 0.04 | metal, lens (0.05, 0.12, 0.16) |
| **Bolt handle** (moves) | shaft cylinder r 0.006 x 0.06 + ball r 0.012, at (0.03, 0.01, 0.08) | steel |
| Folded bipod | 2 rods 0.008 x 0.008 x 0.2 under the barrel | metal |
| Hands | same construction as the SMG, positioned on the grip and under the stock | glove, sleeve |

Moving part: the **bolt handle** (own object, pivot at its root) cycles back and forth after each shot.
The scope view in the prototype is a screen overlay, not a 3D lens model.

### 5.3 Combat knife

Held position (0.17, -0.18, -0.33), rotated (0.25, 0.25, -0.40) rad. Total 0.32 x 0.58 x 0.66 m with the hand
and sleeve. It has no muzzle.

| Part | Size (m) | Material |
| --- | --- | --- |
| Grip | cylinder r 0.016 / 0.018, 0.12 long + 4 thin rings r 0.019 x 0.006 | polymer |
| Guard | box 0.07 x 0.012 x 0.014 | metal |
| Blade | extruded tapered profile 0.205 long, 0.032 tall at the base, 0.004 thick with a small bevel; the tip drops to the lower edge | polished steel #b8bec2 (metal 1.0, rough 0.18) |
| Spine ridge | box 0.005 x 0.006 x 0.17 along the top of the blade | metal |
| Hand | glove and sleeve cylinder r 0.042 / 0.05, 0.42 long | glove, sleeve |

Moving part: the whole **hand** (hand + blade) swings in a slash, 0.34 s per swing.

### 5.4 Muzzle flash

Three crossed additive quads, each 0.22 x 0.22 m (the third at 0.7 scale, in the horizontal plane), a flash
texture tinted bright warm white (3.5, 2.6, 1.6), double sided, no depth write, hidden until a shot. It is
placed at each gun's muzzle. A short point light is added in the viewmodel scene by the weapon system.
This is an effect, not a mesh to model; it is listed here so the prefab has a `MuzzleFlash` child.

### 5.5 Detail level

Low-poly: 23 / 26 / 12 parts and about 1,050 / 1,700 / 530 triangles. The weapon fills the lower right of the
screen all the time, so **edge quality and silhouette** matter here more than anywhere else. The hands are
simple blocks; they only need to read as gloved hands holding the gun.

## 6. Cure case, containment chamber and medkit

Screenshots: `13_lab` (chamber with its glowing ring), `08_hero_robot` (chamber at the far left, cure case on
the pedestal).

### 6.1 Cure case (the objective)

A small **floating glass case with three glowing cyan vials**, above the chamber pedestal. It hovers
1.45 m above the lab floor (about 0.06 m over the pedestal plate), slowly turns and bobs. It disappears
when the player walks into it.

| Part | Size (m) | Material |
| --- | --- | --- |
| Base plate | rounded box 0.42 x 0.08 x 0.24 | steel |
| Lid plate | rounded box 0.42 x 0.04 x 0.24, at y +0.26 | steel |
| Posts | 2 cylinders r 0.012, 0.22 tall, at x +/-0.19 | chrome |
| Vials | 3 cylinders r 0.03, 0.17 tall, at x -0.10, 0, +0.10 | **emissive cyan** (0.4, 2.6, 2.8) |
| Vial caps | 3 cylinders r 0.034, 0.03 tall, at y +0.235 | plasticDark #23272b |
| Glass | box 0.40 x 0.20 x 0.22, at y +0.14 | glass |

Overall about 0.42 wide x 0.28 tall x 0.24 deep. Moving: the whole group **rotates about 0.6 rad/s (Y)** and bobs
+/-0.02 m at 1.6 rad/s; the vial glow pulses between about (0.4, 1.7, 2.2) and (0.4, 2.7, 3.0). A cyan point
light (#47d1d6) sits 1.7 m above the floor.

### 6.2 Containment chamber

The centrepiece of the lab, 3.8 x 3.8 m on the floor, centred at the cure position (prototype (0, -135),
Unity (0, 135) with the level flip). It is open toward the door (+Z in the prototype), so the player walks in
from the corridor side.

| Part | Size (m) | Notes / material |
| --- | --- | --- |
| Floor plate | box 3.8 x 0.35 x 3.8 | steel, a low raised platform |
| Glass wall | open cylinder R 1.7, 2.9 tall (y 0.35 ... 3.25), with a **1.25 rad (about 72 degree) opening** facing the door | glass (12 % opacity), a real gap, no door |
| Top collar | cylinder r 1.85, 0.30 tall, at y 3.4 | steel |
| Support column | cylinder r 0.12, 0.5 tall, from the collar to the ceiling (y 3.55 ... 4.0) | steel |
| Glow ring | torus R 1.65, tube 0.04, at y 3.22, flat | **emissive cyan** #47d1d6, intensity 2.5 |
| Pedestal | tapered cylinder r 0.36 (bottom) to 0.28 (top), 1.0 tall (y 0.35 ... 1.35) | labWhite #dfe5e6 (metal 0.05, rough 0.35) |
| Pedestal plate | cylinder r 0.34, 0.05 tall, at y 1.37 | steel |

Effects around it (not modelled): a cyan spotlight from the collar (angle about 0.55 rad), a soft cyan light
cone r 1.4 x 2.9 m from the collar to the floor (additive), and the cyan point light above. The glass wall has
16 small cylinder colliders around the arc except the opening.

Detail level: low-medium, simple primitives with one strong emissive element (the ring) and the light cone doing
the visual work. Triangle estimate 600 to 800 **(guess)**.

### 6.3 Medkit (health pickup)

Three in the bunker (security office, corridor end, generator catwalk). A white box with a **red cross** on its
sides and a carry handle on top. It floats 0.35 m above the floor, turns 1.2 rad/s, bobs +/-0.05 m, and has a
soft green point light (#60ff90).

| Part | Size (m) | Material |
| --- | --- | --- |
| Case | rounded box 0.42 x 0.26 x 0.28, radius 0.05 | off-white #e8e8e2 with a red cross #c41e1e decal (rough 0.5) |
| Handle | half torus R 0.07, tube 0.015, on top at y +0.13 | plasticDark #23272b |
